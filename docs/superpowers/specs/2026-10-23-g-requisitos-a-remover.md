# G, Tarefa 5 — cortes propostos em `Docs/Requisitos/` (para aprovação)

Lista do passo 1: **nada foi editado ainda**. Cada item: `doc — R#### ou trecho — categoria — ação proposta — confiança`.
Categorias: **repete** = repete o Personagem em vez de dizer só a diferença; **narra** = narra implementação (classe, endpoint, entidade, código HTTP, passo interno, histórico de versão) sem regra visível ao usuário.
Regra aplicada: nenhum requisito que seja o **único** lugar de um comportamento visível foi proposto para remoção — no máximo encurtado. Requisito removido mantém o título e vira `— removido na 2.0.0` + uma linha de motivo.

Sugestão de aprovação: aprovar todos os **alta** em bloco; decidir os **média** um a um. A seção final ("Sinalizados") não é corte: são inconsistências encontradas de passagem, para decidir à parte.

## Resumo

| | repete | narra | total |
|---|---|---|---|
| alta | 21 | 45 | **66** |
| média | 8 | 13 | **21** |
| **total** | **29** | **58** | **87** |

Por ação: encurtar **43** · remover trecho **40** · marcar R#### como removido **3** · trocar por link **1**.
Estimativa: **~2.200 palavras** a menos (≈ 1.600 nos itens alta, ≈ 600 nos média), de ~34.600 nos docs do escopo (≈ 6%). A maior parte sai de Ficha de NPCs (~500), Auditoria de Regras (~450), Ficha de Criaturas (~400) e Banco de Runas (~180).
Requisitos marcados como removidos: NPCs R0002 (alta), NPCs R0014 (média), Catálogo R0011 (média).

## Requisitos - Ficha de NPCs

- **N1** — R0001 — "não há a distinção de 'campos editáveis pelo jogador' vs. 'criação/exclusão exclusiva do GM'…" — repete — encurtar para "O GM que a criou tem acesso total, a qualquer momento." — **alta**
- **N2** — R0002 — "Todas as abas, subgrupos, campos, fórmulas e convenções de campo … se aplicam … sem alteração." — repete (o preâmbulo do próprio doc já diz isso) — marcar R0002 como removido ("coberto pelo preâmbulo") — **alta**
- **N3** — R0004 — "e cabe aos jogadores catalogarem o que descobrem sobre o NPC em seus próprios diários (ver … 6)" — repete (e aponta para a seção errada; o Diário é a 7) — remover a oração — **alta**
- **N4** — R0005 — "Ao contrário do Personagem (onde só a Experiência atual é editável e o Nível é sempre calculado…)" — repete 1.b — encurtar para "Diferente do Personagem (1.b), o GM edita o Nível diretamente; …" — **alta**
- **N5** — R0005 — "Pontos de Ignição seguem 1.b do Personagem: só o valor atual é exibido, e ele acompanha o Nível…" — repete — encurtar para "Pontos de Ignição seguem 1.b do Personagem, também quando o GM edita o Nível; o NPC não tem bônus manual." — **média**
- **N6** — R0006 — "a Variante do NPC concede uma Característica Gratuita (e, para algumas Variantes, também uma Obrigatória) a custo 0…" — repete 5.d — encurtar para "Mesmo mecanismo de 5.d do Personagem; a única diferença é quem resolve…" — **alta**
- **N7** — R0007 — "a soma do *Gasto* dos 8 atributos contra os 'Pontos de Atributo' que o *Nível* … já concedeu…" — repete 2.a — encurtar para "Como no Personagem (2.a), a seção exibe Pontos de Atributo: gasto / disponíveis. Diferente…" — **alta**
- **N8** — R0008 — "inclusive a origem da Runa (do zero ou do Banco de Runas) e a cópia automática para o banco do GM…" — repete Personagem 4.d / R0003 e Banco de Runas R0003/R0007 — encurtar para "segue 4.d do Personagem sem alteração; num NPC concedido, a campanha é a da concessão (Banco de Runas R0007)." — **média**
- **N9** — R0009 — "(Estrela, Histórico e a história em texto formatado, com o mesmo salvamento automático, a mesma limpeza de HTML…)" — repete R0006 — remover o parêntese; manter "sem Diário, Antecedentes é a última aba" e a cópia da História na concessão — **alta**
- **N10** — R0011 — "a Passiva só vem do Banco…, os Requisitos são conferidos contra os valores do NPC e bloqueiam também o GM" — repete 4.f / R0008 (que já diz "GM incluído") — encurtar para "segue 4.f e R0008 sem alteração (ao contrário de R0007/R0010, sem exceção para o GM)" + a frase da concessão — **média**
- **N11** — R0012 — "o valor da coluna EAP, para o Nível da ficha, … somado ao resultado de Âmbares Absorvidos…" — repete 1.b — encurtar para "Como no Personagem (1.b), o VIS Atual é calculado e somente leitura; acompanha o Nível editado pelo GM (R0005) e determina Graduação, Vocação Arcana e Requisitos de Passiva." — **média** (o Personagem 1.b não diz "somente leitura" com todas as letras; a frase curta preserva isso)
- **N12** — R0012 — "Isto substitui a edição manual do VIS Atual que a Ficha de NPC permitia antes." — narra (histórico) — remover — **alta**
- **N13** — R0013 — "a tag 'Requisito(s) não cumprido(s)' nas linhas de 3.a, 3.b, 3.c e 5.b (com os Requisitos e a Penalidade no popup…)" — repete Personagem 2.a/3.b e Catálogo R0014 — encurtar para "Funcionam na Ficha de NPC exatamente como no Personagem (ver Catálogo R0014)." — **alta**
- **N14** — R0014 — "O saldo de Pontos de Ignição bloqueia também o GM." — repete (Personagem 4.b já diz "A regra vale para todos, inclusive o GM") — marcar R0014 como removido ("coberto por Personagem 4.b") — **média** (o contraste com R0007/R0010 se perde; alternativa: encurtar para uma linha)

## Requisitos - Ficha de Criaturas

- **K1** — R0004, 1.a — "*Afinidade*: mesmo campo do Personagem (dropdown de Elementos/Sub-Elementos)." — repete (o preâmbulo diz que o que não é mencionado é igual) — remover a linha — **alta**
- **K2** — R0004, 1.a — "*Efeito de Batalha*: mesmo campo pendente do Personagem (3.d)." — repete — remover a linha — **alta**
- **K3** — R0004, 1.b — "*Nível* e *Experiência atual*: ao contrário do Personagem … editar um deles recalcula automaticamente o outro…" — repete NPC R0005 palavra por palavra — trocar por link: "*Nível* e *Experiência atual*: como em [[Requisitos - Ficha de NPCs#…R0005…]]." — **alta**
- **K4** — R0004, 1.c — "*Foco* … (mesmo campo, mesma fórmula: `(Astúcia × 2) + Status de classe Arcana`)" — repete — encurtar para "*Foco* é rotulado **Arcana**." — **alta**
- **K5** — R0004, 1.c — "*Vitalidade*: mesma fórmula do Personagem…" e "*Adrenalina*: mesmo campo e fórmula do Personagem." — repete — remover as duas linhas (a diferença real é o "Status de classe vem da Tabela de Arquetipos", que fica) — **alta**
- **K6** — R0005, 2.a — "campo tratado como os demais atributos (mesma estrutura Gasto/Bônus/Maestria/Total e mesmas fórmulas de Total do Personagem)" — repete — encurtar para "Ego não entra em nenhuma fórmula por ora." — **média**
- **K7** — R0005, 2.a — "soma do *Gasto* dos 6 atributos contra os 'Pontos de Atributo' que o *Nível* … concede na Tabela de Níveis" — repete NPC R0007 — encurtar para "Pontos de Atributo: gasto / disponíveis, como em NPC R0007. Diferença: …" — **alta**
- **K8** — R0005, 2.b — "mesmos campos e fórmulas do Personagem (Iniciativa, Movimentação, Esquiva Natural, …)" — repete — encurtar para "**2.b Sub-Atributos** — diferenças:" — **alta**
- **K9** — R0005, 2.d — "Mesma estrutura Gasto/Modificador/Atributo/Total do Personagem;" — repete — remover — **alta**
- **K10** — R0006, 3.a — "mesmo comportamento do Personagem (3.a) — escolhe uma Arma …, todos os campos (Nome, Tipo de Dano, …) vêm preenchidos…" — repete — encurtar para "**Vincular ao Catálogo**: como no Personagem (3.a)." — **alta**
- **K11** — R0006 — "O restante (Peso quando vinculado ao Catálogo, seleção de Equipada) segue igual…", "3.b … e 3.c: sem alteração", "3.d … e 3.e: sem alteração" — repete — remover as três linhas — **alta**
- **K12** — R0007, 4.b — "mesma estrutura do Personagem (Nome, Tipo, Grau, Efeitos, Gasto em PI, Custo em Arcana, Descrição — ver 4.b)" + "O saldo de PI e a edição … seguem 4.b…; o bloqueio vale também para o GM" — repete — encurtar para "como no Personagem (4.b), com o Grau limitado pelo Rank (equivalência pendente); o saldo é o campo *Pontos de Ignição* (1.b)." — **média**
- **K13** — R0007, 4.e — "Como no Personagem, a seção é exibida na aba Combate, logo abaixo de Armas." — repete (Personagem 4.e já diz "vale igualmente para … Criatura") — remover a frase — **alta**
- **K14** — R0008, 5.c — "**5.c Afeições**: sem alteração em relação ao Personagem." — repete — remover — **alta**
- **K15** — R0009 — "tag 'Requisito(s) não cumprido(s)' nas linhas de equipamento (com os Requisitos e a Penalidade no popup…), alerta…" — repete — encurtar para "Funcionam como no Personagem (ver NPC R0013). Exceções: …" — **alta**

## Requisitos - Ficha de Personagem

- **P1** — 1.a *Classe* — "(Chamado de 'Sub-Vocação' no código e no banco de dados — só o rótulo exibido mudou.)" — narra — remover — **alta**
- **P2** — 1.a *Trabalho* — "campo previsto porém ainda não implementado. Vamos apenas não implementa-lo." — narra — encurtar para "campo previsto, não implementado." — **alta**
- **P3** — 1.b *Nível* — "não é mais editável diretamente na Ficha de Personagem" — narra (histórico) — encurtar para "não é editável" — **alta**
- **P4** — 1.b *VIS atual* — "(Chamado de 'EAP' no código e no banco de dados — só o rótulo exibido mudou.)" — narra — remover — **alta**
- **P5** — 1.b *Pontos de Ignição* — "O valor é salvo sozinho, … para que uma edição em outro campo da ficha nunca sobrescreva o saldo." — narra — encurtar para "É salvo assim que editado." — **média** (a garantia "outra edição não sobrescreve o saldo" é visível só em concorrência)
- **P6** — 2.b *Esquiva Natural* — "(Esse termo vale sempre 0: a penalidade de uma armadura agora vem da Penalidade do próprio equipamento…)" — narra — encurtar para "(o termo vale 0; a penalidade da armadura vem de Catálogo R0014)" — **média** (a fórmula espelha [[Formulas]], que não muda nesta tarefa)
- **P7** — 2.b *Cobertura* — "os valores guardados continuam Nenhuma/Parcial/Completa." — narra — remover — **alta**
- **P8** — 2.c *Sub-Elemento* — "— é o que preserva, até a próxima edição de Essência, uma linha salva antes desta mudança cujo Sub-Elemento não vem da Matriz." — narra (dado legado) — remover a oração; manter "só é recalculado quando a Essência 1 ou 2 muda" — **média**
- **P9** — 4.e *Disposição* — "apesar do identificador 4.e (mantido para não quebrar as referências existentes)," — narra — remover o parêntese — **alta**

## Requisitos - Campanha

- **C1** — R0010 — "Uma Magia/Habilidade antiga, sem vínculo com o banco, reaproveita uma entrada idêntica do banco do GM … só na falta dela é criada uma entrada nova…" — narra (dado legado) — remover — **média**
- **C2** — R0014 — "limitado a −1000..1000, com erro 400 e mensagem em português fora disso" — narra — encurtar para "limitado a −1000..1000" — **alta**
- **C3** — R0015 — "um aviso sonoro (`harp_notification.mp3`)" — narra — encurtar para "um aviso sonoro" — **média** (o arquivo não é escolha do usuário, mas identifica o som)
- **C4** — R0016 — "Ver '[[Requisitos - Modelo de Dados]]' (SheetLogEntries)." — narra — remover o nome da entidade; manter o link — **alta**

## Requisitos - Catálogo de Itens e Equipamentos

- **I1** — R0003–R0006 — "valor numérico decimal (float) ≥ 0" (5 ocorrências) — narra — encurtar para "valor numérico decimal ≥ 0" — **alta**
- **I2** — R0004, R0005, R0006 *Durabilidade* — "não é mais digitada — vem do Rank…" (3 ocorrências) — narra (histórico) — encurtar para "vem do Rank…" — **alta**
- **I3** — R0011 — "A mesma regra vale de forma geral para qualquer campo de Item nas fichas … (Ficha de Personagem R0003)" — repete Personagem R0003, que já diz exatamente isso — marcar R0011 como removido ("coberto por Ficha de Personagem R0003") — **média**
- **I4** — R0012 — "(ver … R0014 e '[[Requisitos - Modelo de Dados]]', EquipmentKitFixedItems)" — narra — remover a referência ao Modelo de Dados/entidade — **alta**
- **I5** — R0013 — "(ver '[[Requisitos - Modelo de Dados]]', SubcategoriaOptions; não há uma página nova)" — narra — remover o parêntese — **alta**
- **I6** — R0013 — "não existe uma flag booleana separada gravada no item; o checkbox … (client-side), nunca enviado ao servidor…" — narra — encurtar para "O prefixo é o que identifica a Subcategoria como vinda do construtor; por isso Categoria e Família não podem conter o separador `" - "` (cadastro rejeitado)." — **alta**
- **I7** — R0014 — parágrafo "**Migração (versão 1.4.3)**: os campos antigos foram convertidos…" — narra (migração de dados passada) — remover — **alta**

## Requisitos - Banco de Magias e Habilidades

- **M1** — R0003 — "O servidor recusa (400) uma entrada de outro GM, ou uma que o jogador não alcança — não basta o cliente só listar as públicas." — narra — encurtar para "Uma entrada fora desse escopo é recusada." — **alta**
- **M2** — R0005 — "— não são mais campos de texto/número livres" — narra (histórico) — remover — **alta**

## Requisitos - Banco de Runas

- **R1** — R0001 — "Uma Runa que já existia numa ficha antes deste banco existir **não** é copiada retroativamente — o banco começa vazio." — narra (histórico) — remover — **média**
- **R2** — R0009 — "No fio, o Tipo é o texto 'Arcana' ou 'Negra' … rejeitado com 400 ('Tipo de Runa desconhecido.')" — narra — remover — **alta**
- **R3** — R0009 — "A listagem do banco aceita o filtro de Tipo ('Arcana', 'Negra' ou 'Nenhum'…)" — narra (repete R0004 em termos de API) — remover — **alta**
- **R4** — R0010 — "e **não** envia nada ao servidor (…); o servidor também rejeita com 400 ('Disciplina é obrigatória.')" — narra — encurtar para "…mostra 'Escolha a Disciplina da runa.' e não salva." — **alta**
- **R5** — R0010 — "No fio, a Disciplina é o nome do valor ('Adicao', …); qualquer outro valor é rejeitado com 400." — narra — remover — **alta**
- **R6** — R0010 — "A listagem do banco aceita o filtro de Disciplina (um dos quatro nomes ou 'Nenhuma'…)" — narra (repete R0004) — remover — **alta**

## Requisitos - Auditoria de Regras

- **A1** — preâmbulo — "(embora nada no modelo de dados imponha isso)" — narra — remover — **alta**
- **A2** — R0001 — "A concessão é verificada diretamente no banco a cada requisição — não fica embutida no token da sessão — então…" — narra — encurtar para "A concessão e a revogação têm efeito imediato, sem o Auditor precisar sair e entrar de novo." — **alta**
- **A3** — R0006 — "sem precisar de uma alteração de código" — narra — remover — **alta**
- **A4** — R0007 — "— não mais um campo de texto livre —" — narra (histórico) — remover — **alta**
- **A5** — R0007 — "O servidor recusa (400) qualquer submissão cujo … — a tela guia, o servidor garante." — narra — encurtar para "Uma submissão cujo Custo em PI, Grau ou pré-requisitos não batem é recusada." — **alta**
- **A6** — R0008 — "(o casamento do seed usa o Nome como chave)" — narra — remover o parêntese; o comportamento visível (o nome original volta) fica — **média**
- **A7** — R0009 — "(o casamento do seed usa o Nome como chave, mesma limitação do `HistoricoSeeder` de R0008)" — narra — encurtar para "(mesma limitação de R0008)" — **alta**
- **A8** — R0009 — "Cada alteração de um kit … grava o kit inteiro; se o servidor recusar (por exemplo, por uma linha ainda não convertida)…" — narra — encurtar para "Uma alteração recusada mostra o motivo." — **média**
- **A9** — R0010 — "(usada como separador da lista de Famílias permitidas de um slot de escolha, ver '[[Requisitos - Modelo de Dados]]' EquipmentKitChoiceSlots)" e "(usada como separador na string composta…)" — narra — remover os dois parênteses; manter a proibição de `,` e `" - "` — **alta**
- **A10** — R0010 — "a exclusão é lógica (soft delete)", "— antes só Arma", "(um multi-seletor, não mais texto livre)" — narra — remover "(soft delete)", "— antes só Arma" e "não mais texto livre" — **alta**
- **A11** — R0013 — parágrafo "**Colunas de sistema novas**: … `TabelaDeNiveisSeeder` … `Program.cs` … deploy da 1.4.2…" — narra — encurtar para "Uma coluna de sistema acrescentada por uma versão nova entra logo depois da coluna de sistema anterior; as seguintes são deslocadas." — **alta**
- **A12** — R0013 — "(…; salva ao marcar/desmarcar, e uma falha reverte a caixa e mostra a mensagem da API)" e "guardada em TabelaDeNiveisConfig, ver '[[Requisitos - Modelo de Dados]]'" — narra — remover os dois trechos; manter "vale para o servidor inteiro" — **alta**
- **A13** — R0013 — "ela não é mais editada em Auditoria → Livro de Regras." — narra (histórico; R0002 já exclui a aba) — remover — **alta**
- **A14** — R0014 — parágrafo "**Conversão na versão 1.4.3**: … por `make migrate` …" — narra — encurtar para "Um item da base que só tem nome (vindo da conversão dos kits antigos) é marcado **'Detalhes incompletos'** até o Auditor preenchê-lo." — **média** (a marca é visível e citada em R0009, por isso não some)
- **A15** — R0015 — "(a versão fica em `AppVersionInfo.Current`; … LastSeenAppVersion, ver '[[Requisitos - Modelo de Dados]]')" — narra — remover — **alta**
- **A16** — R0015 — "Na 1.4.3, descreve a base de itens fixos… Na 1.4.4, descreve a página da Tabela de Afinidades (R0016)." — narra (texto de changelog por versão) — remover — **alta**

## Requisitos - Gerenciador de Encontros

- **E1** — R0003 — "pelo mesmo caminho da recarga por Condições (sem trocar o campo que está sendo editado)" — narra — encurtar para "A ficha aberta do dono e a do GM mostram os valores novos na hora, sem perder o campo em edição." — **média**
- **E2** — R0007 — "Encontros criados antes desta regra: os que nunca tiveram a vez avançada ficam no Turno 0…" — narra (dado legado) — remover — **média**
- **E3** — R0010 — "Ver '[[Requisitos - Modelo de Dados]]' (EncounterLogEntries)." — narra — remover o nome da entidade; manter o link — **alta**
- **E4** — R0014 — "todo número do resumo vem do mesmo cálculo das telas da ficha, feito duas vezes — com as Condições e sem elas." — narra — encurtar para "os números são os mesmos das telas da ficha." — **média**

## Requisitos - Habilidades Raciais

- **H1** — preâmbulo — "calculado pela API a partir do que o GM cadastrou aqui" — narra — encurtar para "calculado a partir do que o GM cadastrou aqui" — **alta**
- **H2** — R0002 — "(inclusive para os GMs que já tinham a tabela: as 18 Arcas já cadastradas continuam onde estavam…)" — narra (transição de versão) — remover o parêntese — **média**

## Requisitos - Livro de Regras

- **L1** — R0004 — "Documentos longos (até 60 títulos) eram exibidos como um único bloco de HTML contínuo… não mais texto corrido" — narra (histórico) — encurtar para "Cada documento é dividido em seções a partir de um nível de título (tabela abaixo), cada uma num cartão separado." — **alta**
- **L2** — R0004, tabela, linha Equipagem — "vem do catálogo de EquipmentKits" — narra — encurtar para "vem do catálogo de kits" — **alta**
- **L3** — R0008 — "(`EquipmentKit`/`EquipmentKitItem`/`EquipmentKitChoiceSlot`, …) … por meio do `EquipmentKitsController` …, gated ao Auditor" — narra — encurtar para "montada a partir do catálogo de kits ('[[Requisitos - Auditoria de Regras]]' R0009); uma edição do Auditor aparece na aba imediatamente." — **alta**

## Requisitos - Compêndio de Regras

- **D1** — preâmbulo "Status" — "o endpoint e o índice de busca … continuam existindo no código (nada foi removido)… `IRulesDataProvider` … não foi afetado." — narra — encurtar para "a entrada de menu foi ocultada em favor do Livro de Regras; a página continua existindo, fora do menu." — **alta**

## Requisitos - Login e Cadastro

- **G1** — preâmbulo — "Formaliza em R000x o que até agora só existia como texto solto nesses três blocos." — narra (histórico) — remover — **alta**
- **G2** — preâmbulo — "Toda outra tela exige um JWT válido (ver '[[Requisitos - Técnico]]' R0004)." — narra — encurtar para "Toda outra tela exige login (ver Técnico R0004)." — **alta**
- **G3** — R0004 — "revoga o refresh token atual (ver … e a tabela `RefreshTokens` em …)" — narra — encurtar para "encerra a sessão" — **alta**
- **G4** — R0005 — "revoga todos os refresh tokens ativos daquele GM (… tabela `RefreshTokens` …)" — narra — encurtar para "encerra todas as sessões abertas daquele GM" — **alta**

## Técnico e Modelo de Dados

Nada novo: já tratados nas Tarefas 3–4.

## Sinalizados (não são cortes — inconsistências encontradas, para decidir à parte)

- **S1** — Livro de Regras, preâmbulo "Natureza do conteúdo" e **R0003**; Compêndio, preâmbulo "Natureza do conteúdo" e **R0004** — dizem "ninguém edita pela interface", e o preâmbulo "Edição" logo abaixo (e a Auditoria) dizem o contrário. Proposta: no Livro de Regras, R0003 → "somente leitura para GM e jogadores; só o Auditor de Regras edita ([[Requisitos - Auditoria de Regras]])" e fundir os dois preâmbulos.
- **S2** — Banco de Runas R0008 — "Diferente do Banco de Magias (cuja lista um jogador vinculado consegue ler por inteiro)" contradiz o preâmbulo do Banco de Magias ("a listagem é GM-only"). Dá para conferir no código qual dos dois vale.
- **S3** — Campanha, preâmbulo "Documentos relacionados" — "'Requisitos - Ficha de Criaturas' e 'Requisitos - Ficha de NPCs' (documentos ainda não detalhados)" está desatualizado; trocar por wikilinks.
- **S4** — Referências ao Diário como seção "6" do Personagem (o Diário é a 7): Campanha R0011, Catálogo R0010 e NPCs R0004 (este último já sai com N3).
- **S5** — Personagem 4.c Contratos — "Como a Ficha de Criaturas ainda não foi detalhada…" está desatualizado (o doc existe; Contratos continuam pendentes por falta de regra).
- **S6** — Personagem 4.e *Gasto Maestria* — "(o total exato … ainda não está totalmente definido — ver R0001 em 1.b, Âmbares Absorvidos, para outro gap semelhante)" está desatualizado: Âmbares já tem valores, e a Tabela de Níveis tem colunas de Maestria (Auditoria R0013).
- **S7** — Convite de Jogador R0002 — a coluna Status lista Ativo/Usado/Revogado, mas R0004 acrescenta **Expirado**.
- **S8** — NPCs R0003 — "Experiência dada" está na lista de **filtros**, mas é a definição de um campo da ficha; ficaria melhor como requisito ou parágrafo próprio.
