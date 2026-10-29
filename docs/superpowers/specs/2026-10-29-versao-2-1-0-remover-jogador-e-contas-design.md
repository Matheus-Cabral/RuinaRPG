# 2.1.0 — Remover jogador da campanha; desativar e excluir conta de jogador

**Status:** desenho conversado com o usuário em 2026-10-24 (decisões do backlog) e 2026-10-29. Constrói sobre o
cemitério de fichas já entregue (`2026-10-24-versao-2-1-0-cemiterio-design.md`, branch `release/2.1.0`).

## Intenção

Na mesa, jogadores saem: de uma campanha, por um tempo, ou de vez. O GM precisa tirá-los do jogo sem perder a
história dos personagens. As fichas vão para o cemitério (visíveis ao GM e ao dono, editáveis por ninguém) e o
que era do jogador volta ao GM.

## Decisões do usuário

1. **Remover jogador da campanha** (GM): o jogador deixa de ser membro daquela campanha.
   - As **Fichas de Personagem** dele nessa campanha vão para o cemitério (motivo `RemovidoDaCampanha`, que o
     cemitério já calcula a partir do vínculo de membro).
   - As **fichas de NPC/Criatura concedidas** a ele **voltam ao GM** e o GM **pode alterar a posse** delas.
   - As **Notas Secretas** endereçadas a ele **ficam**.
   - Ele **sai dos Encontros ativos** (em preparação ou em andamento).
2. **Desativar conta** — o jogador sai **temporariamente**:
   - sai de todas as campanhas (como em 1) e as fichas vão para o cemitério;
   - ao **reativar**, a conta volta a entrar, e o **GM o adiciona de novo a cada campanha**; as fichas saem do
     cemitério sozinhas ao voltar a ser membro (se não estiverem marcadas como mortas).
3. **Excluir conta** — o jogador sai **de vez**: todas as fichas vão para o cemitério e lá ficam. O dono pode ser
   só nominal ("o que for mais conveniente em termos de arquitetura") — escolha abaixo.
4. **Listas:** jogador com conta desativada ou excluída **não aparece** na lista de adicionar membro à campanha. Na
   lista de jogadores do GM, quem está **desativado aparece separado**; quem foi **excluído não aparece em lugar
   nenhum**. A ficha de uma conta excluída continua no cemitério, **marcada como de conta excluída**.

## Comportamento

### Remover jogador da campanha

- Na aba **Membros** da campanha, cada membro ganha a ação **Remover da campanha** (ícone MudBlazor, nunca emoji),
  com confirmação que diz o que vai acontecer (fichas ao cemitério, concessões de volta ao GM, saída dos encontros
  ativos).
- Efeitos, numa única transação:
  1. apaga o vínculo `CampaignMembers` (as Fichas de Personagem entram no cemitério por cálculo, sem gravar nada
     nelas);
  2. toda Ficha de NPC/Criatura **concedida a esse jogador nessa campanha** passa a `OwnerId = null` (volta a ser
     do GM) e **continua anexada** à campanha (o `CampaignAttachment` fica). Uma ficha conta como concedida nesta
     campanha quando o jogador é o dono e ela está anexada a esta campanha; se ela também estiver anexada a outra
     campanha em que o jogador continua membro, o dono **não** muda *(chamada minha)*;
  3. remove dos Encontros **em preparação ou em andamento** desta campanha todo participante vindo de uma Ficha de
     Personagem dele ou de uma ficha que estava concedida a ele, com as mesmas regras de "remover participante"
     (Gerenciador de Encontros R0008: passagem da vez, log do Encontro). Encontros **finalizados** não mudam (são
     histórico);
  4. Notas Secretas e entradas de diário não mudam. O jogador perde o acesso a elas por não ser mais membro; volta
     a vê-las se voltar à campanha *(chamada minha)*.
- O jogador removido continua dono das suas Fichas de Personagem e as vê no cemitério do seu painel (já entregue).
  A campanha some de "Minhas Campanhas" dele.
- Avisar o jogador (aviso em tempo real, como os existentes) para o painel aberto se atualizar *(chamada minha)*.
- Readicionar o jogador (Campanha R0003, já existente) tira do cemitério as fichas não marcadas como mortas. As
  concessões **não** voltam sozinhas — o GM usa "alterar a posse".

### Alterar a posse de uma ficha concedida

- Novo: em **Concessões existentes** (aba Conceder Ficha da campanha), o GM muda o dono de uma ficha de NPC/Criatura
  anexada à campanha para **outro membro ativo** da campanha, ou a devolve ao GM (sem dono). A ficha é a mesma (não
  é cópia): o histórico e o log continuam.
- As fichas que voltaram ao GM por remoção aparecem ali como "sem dono" *(chamada minha: hoje uma ficha sem dono
  não aparece em Concessões; passa a aparecer quando está anexada à campanha por concessão)*.
- Mudar a posse com a ficha num Encontro ativo: o participante continua, ligado à mesma ficha *(chamada minha)*.

### Estado da conta

- A conta de jogador ganha um estado: **Ativa** (padrão), **Desativada**, **Excluída**. Só contas de papel Jogador.
- **Quem pode:** só o GM que convidou o jogador (`InvitedByGmId`). Qualquer outro recebe 404.
- **Desativar** (de Ativa): aplica "remover da campanha" em **todas** as campanhas do GM em que o jogador é membro;
  revoga os refresh tokens; daí em diante o login é recusado com a mensagem "Esta conta está desativada. Fale com o
  seu GM." *(texto: chamada minha)* e toda requisição autenticada da conta é recusada (401) mesmo com um token de
  acesso ainda válido — o construtor escolhe o mecanismo.
- **Reativar** (de Desativada): volta a Ativa; login liberado; nenhuma campanha é restaurada.
- **Excluir** (de Ativa ou Desativada; irreversível): faz o que Desativar faz e então:
  - **a linha da conta não é apagada** (escolha de arquitetura: fichas, logs, notas e anexos apontam para ela;
    apagar exigiria tratar todos esses vínculos). Ela vira um registro de conta excluída: estado Excluída; e-mail,
    senha e demais dados de login apagados, para que ninguém entre com ela e o **mesmo e-mail possa se cadastrar
    de novo**; o apelido fica (para mostrar de quem era a ficha) e continua reservado *(chamada minha)*;
  - confirmação forte: o GM digita o apelido do jogador para confirmar *(chamada minha)*.
- Imagens enviadas pelo jogador, diários das fichas e o log das fichas ficam como estão.

### Motivos do cemitério

- O motivo reservado `ContaInativa` passa a dois: **`ContaDesativada`** ("Conta do jogador desativada") e
  **`ContaExcluida`** ("Conta do jogador excluída"). Calculados pelo estado da conta do dono (no
  `Cemiterio.Motivos`/`CemiterioDaFicha`), como os demais.
- Como desativar/excluir também remove o jogador das campanhas, uma ficha nesse caso mostra dois motivos (removido e
  conta) — aceito; a ficha sai só quando nenhum resta.
- Na aba Cemitério, o dono de uma ficha de conta excluída aparece pelo apelido; o motivo diz "Conta do jogador
  excluída".

### Listas e telas

- **Adicionar membro à campanha:** só jogadores com conta Ativa.
- **Jogadores do GM** (página de jogadores da ferramenta Convidar Jogador, Convite de Jogador R0006/R0007): a lista e a
  busca mostram os **ativos** como hoje, com as novas ações **Desativar** e **Excluir**; uma seção separada
  **Desativados** lista os desativados com **Reativar** e **Excluir**. Contas excluídas não aparecem.
- Redefinir senha (R0007) só para contas ativas e desativadas, nunca excluídas.
- Contas excluídas também não aparecem em nenhum seletor de jogador (concessão, notas secretas, membros).

## Documentação (`Docs/Requisitos/`)

- **Campanha:** novos requisitos para remover membro e alterar a posse de ficha concedida; R0003 (só ativos), R0010
  (posse), R0011 (notas ficam), R0017 (motivos de conta).
- **Convite de Jogador:** novos requisitos de desativar, reativar e excluir; R0006/R0007 com os estados.
- **Login e Cadastro:** login recusado para conta desativada/excluída; e-mail de conta excluída livre para novo
  cadastro.
- **Ficha de Personagem R0010:** os motivos de conta.
- **Gerenciador de Encontros:** remoção de participantes ao remover o jogador.
- **Modelo de Dados:** estado da conta.
- **01 - Visão Geral:** a página de jogadores com os desativados.

## Estrutura (pedido do usuário: seguir a estrutura da 2.0.0, sem refatoração futura)

O código novo segue o mapa do `CLAUDE.md` e os desenhos D (`2026-10-18-versao-2-0-0-d-backend-design.md`) e E
(`2026-10-19-versao-2-0-0-e-cliente-design.md`). Aplicado a esta entrega:

- **Regras no Domain, puras:** as transições de estado da conta (Ativa → Desativada → Ativa; Ativa/Desativada →
  Excluída; nenhuma saída de Excluída), "este GM pode gerir esta conta" e os motivos do cemitério ficam em
  `src/RuinaRPG.Domain`, sem EF, ASP.NET ou Identity, com testes unitários. Controllers e serviços só buscam os
  dados e chamam essas regras.
- **Uma única "saída do jogador":** remover da campanha, desativar e excluir usam **o mesmo** serviço da Api (um
  ponto que, para um jogador e um conjunto de campanhas, apaga os vínculos, devolve as concessões e tira os
  participantes dos encontros ativos). Nenhuma das três ações reimplementa esses passos.
- **Encontros:** a remoção de participantes reaproveita o caminho já existente de "remover participante"
  (Gerenciador de Encontros R0008 — passagem da vez, log, Condições). Se ele hoje só existe dentro do controller,
  é extraído para um serviço e o controller passa a usá-lo; nada é copiado.
- **Fichas concedidas (NPC e Criatura):** "alterar a posse" é **uma** implementação genérica sobre
  `ITipoDeFicha<TSheet>` (base em `Controllers/Fichas/`, fechada por `TipoNpc`/`TipoCriatura`, como os demais
  recursos de ficha), com **um** record em `Contracts/Fichas/`. Testes genéricos por tipo, no padrão de
  `tests/.../Controllers/Fichas/` (classe abstrata + subclasse por tipo), para NPC e Criatura.
- **Contas:** endpoints na área de jogadores já existente (`PlayersController`/rotas `players`), contratos em
  `Contracts` junto dos de jogadores; o estado da conta é uma coluna de `ApplicationUser` mapeada pelo
  `RuinaRpgDbContext`, sem tabela paralela.
- **Cliente:** as ações entram nas páginas e componentes que já existem (aba Membros e Concessões em
  `CampanhaDetalhe`, página de jogadores `GmJogadores`), reaproveitando os padrões delas (diálogo de confirmação,
  `DismissibleAlert`, tooltips). Nada de componente com ramos "se Personagem / se Criatura": diferenças por tipo de
  ficha vêm de `TipoDeFichaCliente`. Os rótulos de motivo do cemitério ficam onde os outros já estão
  (`FichaEstado.RotuloDoMotivo`), e textos compartilhados por servidor e cliente ficam em `Contracts`, como
  `TextosDoCemiterio`.
- **Se algo não couber nessa estrutura**, o implementador para e reporta, em vez de criar um caminho paralelo.

## Restrições

- TDD em toda unidade de comportamento (Técnico R0011); build com 0 warnings; projetos de teste um de cada vez.
- Migration só acrescenta (coluna de estado da conta com padrão Ativa), compatível com a 2.0.0 no blue-green.
- `rotas-da-api.txt` muda só pelas rotas novas, de forma intencional e explicada.
- Remover, desativar e excluir são transacionais: nada pela metade se algo falhar no meio.
- O cemitério continua sendo a única fonte de "está no cemitério" (`CemiterioDaFicha`).

## Deixado para o construtor

- Nomes de rotas, campos e enum; mecanismo de recusar tokens de uma conta inativa.
- Layout das ações na aba Membros, em Concessões e na página de jogadores, com os componentes MudBlazor já usados.
- Ordem das tarefas do plano.
