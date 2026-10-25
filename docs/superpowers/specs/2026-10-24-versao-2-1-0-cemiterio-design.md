# 2.1.0 — Cemitério de fichas

**Status:** desenho conversado com o usuário em 2026-10-24. Primeiro item da 2.1.0; é a base para
"remover jogador da campanha" e "desativar/excluir conta de jogador" (backlog
`2026-10-22-versao-2-1-0-backlog.md`), que terão desenhos próprios.

## Intenção

Na mesa, um personagem sai de jogo — morre, ou o jogador sai da campanha, ou a conta dele é desativada. A ficha
não deve sumir (é a história do personagem), mas também não deve continuar sendo mexida. O cemitério é o lugar
onde essas fichas ficam: **visíveis para o GM e para o dono, editáveis por ninguém**.

## Decisões do usuário

1. Vale só para **Ficha de Personagem** (NPC e Criatura não têm dono jogador).
2. **Quem vê** uma ficha no cemitério: só o GM da campanha e o jogador dono.
3. **Ninguém edita** uma ficha no cemitério — nem o GM —, exceto a caixa "Morto" descrita abaixo.
4. **Três motivos** levam uma ficha ao cemitério:
   - **Morte:** uma caixa de seleção **"Morto"** na ficha, que só o GM vê e usa.
   - **Remoção da campanha:** o dono deixa de ser membro da campanha da ficha.
   - **Conta desativada ou excluída:** a conta do dono deixa de estar ativa.
5. **A ficha sai do cemitério quando nenhum motivo resta.** Desmarcar "Morto" tira a ficha só se o dono
   ainda for membro com conta ativa; o jogador voltar a ser membro tira a ficha só se ela não estiver marcada
   como morta; e assim por diante.
6. **Onde aparece:**
   - GM: uma aba nova **"Cemitério"** dentro da campanha, listando as fichas no cemitério (imagem, nome, nível,
     dono), cada uma abrindo a ficha em modo leitura. A ficha deixa de aparecer na aba Membros.
   - Jogador: no painel do jogador, uma seção **"Cemitério"** separada, abaixo da lista de fichas, com as mesmas
     colunas da lista (imagem, nome, nível, campanha).
   - Um jogador removido da campanha continua dono e continua vendo a ficha no seu cemitério.
7. **Encontros:** uma ficha que entra no cemitério durante um Encontro em andamento **fica nele até o fim, só
   para leitura**; o GM pode removê-la a qualquer momento (Gerenciador de Encontros R0008, como qualquer
   participante).

## Escopo desta entrega

- O mecanismo inteiro do cemitério (estado, regra de leitura/escrita, listas, ficha em modo leitura, encontros).
- O motivo **Morte** completo (caixa, endpoint, log, aviso).
- O motivo **Remoção da campanha** já calculado (dono não é membro da campanha da ficha). Hoje não existe como
  remover um membro, então na prática só a Morte acontece nesta entrega; a funcionalidade de remover jogador só
  precisará apagar o vínculo de membro.
- O motivo **Conta** fica como ponto de extensão (uma função que decide os motivos; o desenho de contas
  acrescenta o seu caso nela). Não há conta desativada ainda.

**Fora do escopo:** remover jogador, desativar/excluir conta, qualquer coisa em NPC/Criatura, mudanças nas regras do
jogo.

## Comportamento

### Estado e motivos

- A ficha guarda só a marca de **morte** (booleana, padrão falso). Os outros motivos são **calculados** a partir do
  estado atual (vínculo de membro; depois, estado da conta) — nada a manter em sincronia, e "sai quando nenhum
  motivo resta" vale por construção.
- Uma única função do servidor responde, para uma ficha, a lista de motivos ativos (`Morte`, `RemovidoDaCampanha`,
  futuramente `ContaInativa`); "no cemitério" = lista não vazia. Todo lugar que precisa saber (autorização, listas,
  encontros, resposta da ficha) usa essa função — nunca recalcula por conta própria.
- A resposta da ficha (GET da Ficha de Personagem) passa a dizer se ela está no cemitério e por quais motivos, para
  o cliente montar o modo leitura e o aviso.

### Leitura × escrita

- Hoje ler e editar uma Ficha de Personagem usam a mesma regra (dono ou GM). Elas passam a ser **duas**: ler
  continua dono ou GM; editar exige, além disso, que a ficha **não** esteja no cemitério.
- **Toda** escrita na ficha e nas suas partes é recusada no cemitério — pelos controllers genéricos de
  `Controllers/Fichas/` e também pelos caminhos fora deles que escrevem numa Ficha de Personagem: o salvamento
  geral e o de PI em `CharacterSheetsController`, a escolha de Equipagem, Diário (`CharacterDiaryController`),
  galeria/imagens da História, e o que o Encontro grava na ficha (edição de PV/PF/PA, ajustes e Condições, ver
  abaixo). O plano deve inventariar esses caminhos (grep por escrita em `CharacterSheet` e tabelas filhas) e cobrir
  cada um com teste.
- **Exceções que continuam valendo no cemitério:** a caixa "Morto" (GM) e a **exclusão** da ficha pelo GM.
- A recusa de escrita numa ficha no cemitério responde **409 Conflict** com a mensagem
  "Esta ficha está no cemitério e não pode ser editada." *(chamada minha)* — o acesso existe, o estado é que impede;
  assim o 403/404 continua significando "sem acesso".

### Caixa "Morto"

- Só o GM da campanha vê e usa; para o jogador ela não aparece (o aviso do cemitério já diz o motivo).
- Fica no cabeçalho da ficha, junto do Nome *(chamada minha; o plano pode ajustar a posição)*, e continua
  utilizável quando a ficha está em modo leitura.
- Endpoint próprio, só do GM (ex.: `PUT /api/character-sheets/{id}/morte` com `{ "morto": bool }`) *(chamada
  minha)*. A mudança na superfície da API é intencional: o snapshot `rotas-da-api.txt` é atualizado com ela.
- Marcar/desmarcar entra no **log da ficha** (Campanha R0016) como alteração do GM no campo "Morto".
- O **dono é avisado** (como os outros avisos ao dono, via `NotificationHub`) quando a ficha entra ou sai do
  cemitério por esse motivo, e a ficha aberta do dono passa a modo leitura (ou volta a editável) sem precisar
  recarregar a página. *(O aviso foi um palpite meu que o usuário não contestou.)*

### Ficha em modo leitura (cliente)

- Todas as abas abrem sem controles de edição: campos desabilitados, sem botões de adicionar/remover, sem
  auto-save. O caminho natural é um sinal único em `FichaEstado` (algo como "somente leitura") que as abas
  compartilhadas de `Shared/Ficha/` já leem, em vez de tratar aba por aba.
- No topo, um aviso fixo com ícone MudBlazor (nunca emoji): "Esta ficha está no cemitério" + o(s) motivo(s)
  ("Personagem morto", "Jogador removido da campanha", "Conta do jogador inativa").
- O breadcrumb/voltar de uma ficha no cemitério leva o GM à aba Cemitério da campanha e o jogador ao seu painel.

### Listas

- **Aba Membros** (GM): fichas no cemitério não aparecem na lista de fichas do membro.
- **Aba Cemitério** (GM, nova): todas as Fichas de Personagem da campanha no cemitério, com imagem, nome, nível,
  dono e motivo(s); o ícone de log da ficha (R0016) também aparece ali. Lista vazia mostra uma mensagem simples.
- **Painel do jogador:** a lista de fichas mostra só as fora do cemitério; a seção "Cemitério" abaixo mostra as do
  jogador que estão no cemitério (imagem, nome, nível, campanha). Seção vazia não aparece *(chamada minha)*.
- **Minhas Campanhas** (jogador): a ficha no cemitério **some** de `PlayerCampaignViewResponse.MinhasFichas`
  (só um filtro; o jogador a encontra na seção Cemitério do painel) *(chamada minha)*.

### Encontros

- Uma ficha no cemitério **não pode ser adicionada** a um Encontro *(chamada minha)*; o seletor não a oferece e o
  servidor recusa.
- Se ela já era participante de um Encontro (em preparação ou em andamento), **continua** como participante:
  - o GM **não** edita PV/PF/PA dela no Encontro, nem aplica ou remove Condições nela;
  - os ajustes automáticos de Condições (Gerenciador de Encontros R0013) que gravariam na ficha são **pulados** e
    viram, como os outros pulos, uma linha no log do Encontro: "ficha no cemitério";
  - o **corte do atual ao novo máximo** quando Condições passam a valer (`CortarAsync` em
    `Services/CondicoesDoEncontro.cs`, chamado por `AoMudarAtivacaoAsync` — ex.: Condição aplicada na
    preparação que passa a valer no "Iniciar") também é **pulado** e vira linha "ficha no cemitério" no log do
    Encontro *(chamada minha: "ninguém edita" vale para o sistema também)*; R0003/R0011 citam esse pulo;
  - a expiração de Condições continua acontecendo, com a linha automática no log da ficha e o aviso ao dono de
    Gerenciador de Encontros R0012 (é estado do Encontro, não muda valor gravado na ficha) *(chamada minha)*;
  - a vez, a ordem de Iniciativa e as Ações seguem como para qualquer participante;
  - o GM pode removê-la (R0008) a qualquer momento.
- O cliente mostra o participante no cemitério com os controles de edição dele desabilitados e um indicador visual
  (ícone MudBlazor).

## Documentação (`Docs/Requisitos/`)

Cada mudança acima precisa estar nos requisitos, que são a fonte de verdade (continuar a numeração, nunca
renumerar):

- **Ficha de Personagem:** novo requisito do cemitério (estado, motivos, modo leitura, caixa "Morto", exceções) e
  ajuste no preâmbulo "Modelo de edição" (exceto fichas no cemitério).
- **Campanha:** novo requisito da aba Cemitério; R0009 e R0016 citam o cemitério onde couber.
- **Gerenciador de Encontros:** R0002 (ficha no cemitério não é adicionada) e um requisito novo (participante cuja
  ficha está no cemitério), mais o motivo de pulo novo em R0013.
- **01 - Visão Geral:** painel do jogador com a seção Cemitério; painel do GM, ferramenta 5, com a aba.
- **Modelo de Dados:** a coluna nova de morte em `CharacterSheets`.

## Restrições

- TDD em toda unidade de comportamento (Técnico R0011); build com 0 warnings.
- Migration: só **acrescenta** uma coluna booleana não nula com padrão falso — compatível com a release anterior
  durante o blue-green (CLAUDE.md, Migrations). Não precisa de `deploy/data-changes.yml` (não altera dados
  existentes).
- Redes de segurança que **devem** mudar, de forma intencional e explicada: `rotas-da-api.txt` (endpoint da
  morte; resposta da ficha com o estado do cemitério) e as abas renderizadas em `RedeDeSeguranca/` se o cabeçalho
  ganhar a caixa para o GM. Nenhuma outra deve mudar.
- O mecanismo de motivos deve ser fácil de estender pelos dois desenhos seguintes (remoção de membro, contas)
  sem mexer de novo em autorização, listas e encontros.

## Deixado para o construtor

- Nomes de classes, do campo e do endpoint; forma exata da resposta (desde que traga "no cemitério" e os
  motivos).
- Como dividir leitura/escrita em `ITipoDeFicha`/`FichaControllerBase` sem mudar o comportamento de NPC e
  Criatura.
- Layout exato da aba Cemitério, da seção do painel e do aviso, seguindo os componentes MudBlazor já usados.
- A ordem das tarefas no plano.
