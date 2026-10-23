# Sistema de Históricos — Design

## Contexto

`Docs/Sistema RPG/Historico.md` define 26 Históricos fixos (ex: "Estudo Acadêmico", "Vida Errante")
— cada um concede **+6 em uma Perícia e +3 em outra**. Hoje isso não existe em código: nem como campo
na ficha, nem como catálogo, nem refletido em nenhum cálculo.

Este é o segundo de dois sistemas novos (o primeiro, Estrelas Alkerianas/Sina, já está implementado —
ver commit `ba971dc`). Ao contrário daquele (uma lista fixa de 10 valores, hardcoded num enum), o
usuário pediu explicitamente uma **página de auditoria para gerenciar os históricos** — ou seja,
Histórico precisa ser um catálogo editável em banco, não um enum fixo.

## Objetivo

1. Um catálogo `Historico` (Nome, Descrição, duas Perícias bonificadas) seedado de `Historico.md`,
   com CRUD completo pelo Auditor de Regras — mesmo padrão já usado por `Trait`/`TraitsController`/
   `AuditoriaCaracteristicas.razor`.
2. Um campo *Histórico* na Identidade (1.a) da Ficha de Personagem e da Ficha de NPC: um selectlist
   + ícone ⓘ que abre um popup com a descrição e as duas perícias bonificadas.
3. As duas perícias bonificadas do Histórico escolhido entram **direto no Modificador** daquela
   Perícia — não um campo à parte, um valor que se propaga por tudo que já deriva de Modificador
   (Sub-Atributos via "Bruto [Perícia]", Maestrias).
4. Uma 6ª aba no Livro de Regras, "Históricos", montada ao vivo a partir do catálogo (não do
   Markdown) — uma edição salva na página de auditoria aparece nela imediatamente, mesmo mecanismo
   que a aba "Características" já usa para o catálogo de Traits. O parágrafo introdutório de
   `Historico.md` (o texto explicativo sobre o que são Históricos e a regra do +6/+3) continua vindo
   do arquivo fonte, extraído como `IntroHtml`.

Fora de escopo, deliberadamente:

- **Ficha de Criatura** — sem Histórico (criatura não tem esse tipo de passado narrativo; mesma
  exclusão já registrada para Estrela/Sina em `Requisitos - Ficha de Criaturas.md`). O parâmetro novo
  de `SkillFormulas.Modificador` (ver abaixo) é passado como `0` nos 2 pontos de chamada de Criatura,
  sem nenhum jeito de a Ficha de Criatura conceder Histórico.
- **Sobrescrita de Markdown pelo Auditor** — a aba "Históricos" não entra em `RulebookDocumentOverride`/
  `ValidSlugs` (mesma exclusão de "caracteristicas"): a edição acontece só pelo CRUD do catálogo.

## Abordagem

Clonar deliberadamente o padrão `Trait`/`TraitsController`/`AuditoriaCaracteristicas.razor`, com uma
diferença: o vínculo da ficha é uma **única FK direta** (`HistoricoId` na raiz do `CharacterSheet`/
`NpcSheet`, como `ImageId`), não uma tabela de junção tipo `CharacterTrait` — porque um Histórico é
uma escolha única por ficha ("selectlist"), não uma lista incremental como Características.

Alternativa descartada: copiar Nome/Descrição/bônus para a ficha no momento da escolha ("cópia
independente", como Magia/Habilidade do Banco). Rejeitada porque o usuário pediu que a página de
auditoria **gerencie** os históricos — uma cópia independente nunca refletiria uma edição/correção
posterior do Auditor, contrariando o próprio pedido.

## Modelo de dados

```
Historico
  Id                Guid PK
  Nome              string (required)
  Descricao         string (required)
  PericiaMaisSeis   enum Pericia   // +6
  PericiaMaisTres   enum Pericia   // +3, != PericiaMaisSeis
  IsCustomized      bool   // true após criar/editar pelo Auditor — protege do re-seed
  IsDeleted         bool   // soft delete
  UpdatedByUserId   Guid? FK -> Users
  UpdatedAt         DateTime?
```

Alterações em tabelas existentes:

```
CharacterSheets  + HistoricoId  Guid? FK -> Historicos (nullable)
NpcSheets        + HistoricoId  Guid? FK -> Historicos (nullable)
```

Padrão de vínculo: **Referência ao vivo ao Catálogo** (ver legenda de
`Requisitos - Modelo de Dados.md`) — mesmo tratamento de Arma/Armadura/Escudo/Artefato equipados.
Nada é copiado para a ficha; Nome/Descrição/bônus exibidos vêm sempre do registro atual do catálogo.

## Seed

`HistoricoSeedParser` (Domain) + `HistoricoSeeder` (Infrastructure), espelhando
`TraitSeedParser`/`TraitSeeder` (`src/RuinaRPG.Infrastructure/Rules/TraitSeeder.cs`, chamado em
`Program.cs` nos dois pontos onde `TraitSeeder.SeedAsync` já roda hoje — path de `make migrate` e
startup normal). Parseia `Historico.md` (26 blocos "N. NOME" + parágrafo de descrição + "+6 X" +
"+3 Y", separados por `---`). Casa por **Nome** (chave natural — não há Custo/Polaridade aqui como em
Trait); insere o que falta. Diferente de `TraitSeeder` (que resincroniza `RequerEspecificacao` em
linhas não customizadas a cada rodada), aqui uma linha já existente e não-customizada não é tocada em
nada — não há um campo "barato" equivalente a `RequerEspecificacao` que valha a pena ressincronizar, e
`IsCustomized=true` continua protegendo qualquer linha editada pelo Auditor, como de costume.

`Historico.md` precisa de um novo `<EmbeddedResource>` em `RuinaRPG.Infrastructure.csproj`
(`LogicalName="Historico.md"`) — usado tanto pelo seed quanto pelo `RulebookRenderer` (só o
parágrafo introdutório, ver abaixo).

## API

`HistoricosController` (`api/historicos`), espelhando `TraitsController`:

- `GET` — qualquer usuário autenticado, sem gate de Auditor (mesmo padrão de `GET /api/traits`);
  filtra `!IsDeleted`; filtro opcional `?nome=`.
- `POST` / `PUT /{id}` / `DELETE /{id}` — exigem Auditor de Regras (`IsRulesAuditor`, checado ao vivo
  no banco, mesmo helper `RequireRulesAuditorAsync` clonado). Create/Update rejeita
  `PericiaMaisSeis == PericiaMaisTres`; ambos setam `IsCustomized = true` e carimbam
  `UpdatedByUserId`/`UpdatedAt`. `DELETE` é soft e retorna 409 se algum `CharacterSheet`/`NpcSheet`
  tiver esse `HistoricoId`.

`CharacterSheetsController`/`NpcSheetsController`:

- `UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest` ganham `string? HistoricoId` (novo parâmetro
  no fim do record posicional, mesma convenção usada para `Estrela`/`SinaAtual`). `Update` valida:
  vazio/nulo → `null`; um Guid que não bate com nenhum Histórico não-deletado → 400 (mesmo padrão de
  `TryParseImageId`, mas com um lookup em vez de só `Guid.TryParse`).
- `CharacterSheetResponse`/`NpcSheetResponse` ganham `string? HistoricoId` no fim (o cliente já busca
  o catálogo inteiro para montar o dropdown — ver Cliente — então não precisa de Nome/Descrição
  resolvidos aqui, só o id cru para popular o `<select>` e destacar a opção atual).

## O bônus no Modificador

`SkillFormulas.Modificador(int gasto)` → `SkillFormulas.Modificador(int gasto, int historicoBonus)`
(`src/RuinaRPG.Domain/CharacterSheets/SkillFormulas.cs`). Novo helper puro,
`HistoricoBonusCalculator.For(Pericia pericia, Historico? historico)`, retornando `6`/`3`/`0`.

Pontos de chamada que precisam carregar o `Historico` da ficha (quando houver) e passar o bônus da
Perícia certa:

| Arquivo | Método | Efeito hoje sem o bônus |
|---|---|---|
| `CharacterSkillsController.cs:48` | `List` (2.d, `GET .../skills`) | Perícia individual errada na tabela |
| `NpcSkillsController.cs:40` | idem, NPC | idem |
| `CharacterSheetsController.cs:365` (`BrutoOf`, dentro de `SubAttributes`) | Iniciativa/Movimentação/Esquiva/Defesa Natural | sub-atributos não refletem o Histórico |
| `NpcSheetsController.cs:324` (`BrutoOf`, `SubAttributes`) | idem, NPC | idem |
| `CharacterMasteriesController.cs:119` | Total de Maestria | idem |
| `NpcMasteriesController.cs:86` | idem, NPC | idem |
| `CreatureMasteriesController.cs:92` | Criatura — **passa `0`, sem Histórico** | (sem mudança de comportamento) |
| `CreatureSheetsController.cs:333` (`BrutoOf`) | Criatura — **passa `0`** | idem |
| `CreatureSkillsController.cs:41` | Criatura — **passa `0`** | idem |

Cada um dos 6 pontos de Personagem/NPC carrega o `Historico` da ficha uma vez (ou reaproveita se já
carregado nesse request) e chama `HistoricoBonusCalculator.For(pericia, historico)` por Perícia.

## Cliente

- `HistoricoSelect.razor` (`Shared/Fields/`), clone de `EstrelaSelect.razor`: em vez de uma lista
  estática, busca `GET api/historicos` uma vez (`OnInitializedAsync`) e monta as opções a partir da
  resposta; o ícone ⓘ abre o mesmo tipo de `MudDialog` com Nome, Descrição e "+6 X / +3 Y". Montado
  na Identidade de `FichaDePersonagem.razor` e `FichaDeNpc.razor`, ao lado do `EstrelaSelect`
  existente.
- `AuditoriaHistoricos.razor` (`/auditoria/historicos`), clone de `AuditoriaCaracteristicas.razor`:
  formulário de adicionar (Nome, Descrição, dois `MudSelect<Pericia>` para as perícias bonificadas) +
  uma tabela única editável inline (sem split Positivas/Negativas — Histórico não tem Polaridade) +
  exclusão por linha. Linkada em `RulesAuditorNavLinks.razor`, junto das outras páginas de Auditoria.

## Livro de Regras

Nova `BuildHistoricosAsync()` em `RulebookRenderer.cs`, espelhando `BuildCaracteristicasAsync` mas
com duas fontes: `IntroHtml` vem do parágrafo antes do primeiro Histórico em `Historico.md` (reaproveita
`SplitIntoSections(markdown, splitLevel: 1)` só pelo `.IntroHtml` — as `.Sections` desse parse são
descartadas, viriam do Markdown e ficariam fora de sincronia com o catálogo); as `Sections` vêm da
tabela `Historicos` do banco (`!IsDeleted`, ordenado por `Nome`), cada uma com Nome, Descrição e uma
linha "+6 X / +3 Y". Não entra em `ValidSlugs` (`RulebookDocumentsController`) — sem sobrescrita de
Markdown, mesma exclusão de "caracteristicas".

## Docs (`Docs/Requisitos/`)

- `Requisitos - Ficha de Personagem.md`, 1.a Identidade: novo campo *Histórico*, mesmo texto-padrão
  de *Estrela* (dropdown + ícone ⓘ), citando a regra do +6/+3.
- `Requisitos - Ficha de Criaturas.md`: adiciona *Histórico* à lista de campos que não existem na
  Ficha de Criatura (mesma linha onde *Estrela*/*Sina* já foram adicionados).
- `Requisitos - Livro de Regras.md`: R0001 passa a listar 6 documentos; nova linha na tabela de R0004
  ("Históricos | — | uma por Histórico; vem do catálogo, não do Markdown"); novo requisito espelhando
  R0004 de Características ("a aba de Históricos reflete o catálogo em tempo real").
- `Requisitos - Auditoria de Regras.md`: novo R0007, mesmo formato de R0003 ("O Auditor de Regras tem
  CRUD completo sobre o catálogo de Históricos").
- `Requisitos - Modelo de Dados.md`: nova tabela `Historicos` (seção de Regras/Auditoria) e a coluna
  `HistoricoId` nas seções de `CharacterSheets`/`NpcSheets`.

## Testes

TDD (CLAUDE.md R0011), espelhando a cobertura que já existe para Traits/Características:

- `HistoricosController`: CRUD feliz, rejeição de `PericiaMaisSeis == PericiaMaisTres`, 403 pra
  não-Auditor, 409 ao excluir um Histórico em uso.
- `CharacterSheetsController`/`NpcSheetsController` `Update`: `HistoricoId` inválido → 400; roundtrip
  válido; e — o ponto central do recurso — a Perícia bonificada aparece com o Modificador certo em
  `GET .../skills` depois de escolher um Histórico (um teste por perícia +6 e +3, cobrindo os dois
  tipos de ficha).
- `SubAttributes`: um teste confirmando que o bônus em Prontidão/Reflexos/Fortitude se propaga pra
  Iniciativa/Esquiva Natural/Defesa Natural quando o Histórico escolhido bonifica uma dessas três.
- `RulebookControllerTests`: nova aba aparece com 26 seções, reflete uma edição salva via
  `HistoricosController` imediatamente (mesmo teste que já existe pra Características refletindo
  `TraitsController`).
- Unit: `HistoricoBonusCalculator.For` (0/3/6 conforme a Perícia bate com qual campo do Histórico, ou
  nenhum) e `HistoricoSeedParser` (26 entradas, campos certos).
