# Versão 2.0.0 — Spec D: Backend consolidation — Design

**Status:** design approved by user 2026-10-18 (in chat). Version 2.0.0, branch `release/2.0.0`.
Detailed spec of sub-project **D** of the roadmap (`2026-10-16-versao-2-0-0-roadmap-design.md`). It also
carries the `/api/rulebook` N+1 fix found while measuring C (C spec, C.5 follow-up).

User decisions taken while brainstorming D:

- **Server only.** Routes, `Contracts` records and the client stay exactly as they are; merging the
  per-kind records (`NpcSkillResponse`, `CharacterSkillResponse`, …) is left to E.
- **Generic base controllers + a sheet-kind adapter** (approach 1), not shared services behind per-kind
  controllers (approach 2) and **not** merging the per-kind tables (approach 3, rejected: it would move
  every production row in the cutover).
- From C: the warm-build miss (110 s vs ≤ 90.5 s) is accepted; the rulebook fix belongs to D.

## Findings that drive D

Measured on `ed7a447`, type names normalized (`Character|Npc|Creature|Criatura|Personagem` → `X`),
counting changed lines of a diff:

| Feature controller | Lines C / N / Cr | Diff C↔N | Diff N↔Cr |
|---|---|---|---|
| Skills | 124 / 72 / 70 | 64 | 16 |
| Attributes | 105 / 83 / 78 | 54 | 15 |
| Masteries | 145 / 110 / 114 | 51 | 14 |
| Arsenal | 357 / 351 / 392 | 54 | 71 |
| Possessions | 441 / 409 / 375 | 112 | 274 |
| SpellAbilities | 320 / 335 / 323 | 79 | 36 |
| Runes | 166 / 173 / — | 49 | — |
| Affinities | 211 / 175 / — | 60 | — |
| Equipagem | 116 / 125 / — | 29 | — |
| Sheets | 660 / 696 / 453 | 828 | 493 |
| `*SheetStats` (Services) | 256 / 258 / 261 | 150 | 185 |

- `Controllers/` holds 74 files, 13 863 lines; `Services/` 1 950 lines. The per-kind feature controllers
  and stats total about 8 000 lines.
- Each kind has its own tables and entities (`CharacterSkill`, `NpcSkill`, `CreatureSkill`, …). They
  differ in the FK property name (`CharacterSheetId` / `NpcSheetId` / `CreatureSheetId`) and sometimes in
  a type (`Atributo` vs `AtributoCriatura`).
- The real rule differences are few and named: Creature Perícias limited to `DisponivelParaCriaturas`,
  level limits on Character/NPC only, `BonusDeHistorico` on Character/NPC only, different endpoint sets
  (inventory vs spoils, `PUT affections/{id}` on Character only, racial traits on Character/NPC only).
- Integration tests: 40 139 lines, of which about 15 800 are per-kind controller tests.
- `/api/rulebook` p50 is 69.8 ms; about 45 ms of it is one query per equipment kit in
  `RulebookRenderer.BuildEquipagemAsync` (`src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs`).

## Design

### D.1 Shared entity interfaces and the `SheetId` rename

- `IFicha` is implemented by `CharacterSheet`, `NpcSheet` and `CreatureSheet`: `Id`, `OwnerId`, `GmId`
  and whatever else the generic code reads (e.g. `Nivel` where all three have it).
- Each per-kind child entity implements one interface per feature (`IPericiaDaFicha<TAtributo>`,
  `IAtributoDaFicha`, `IMaestriaDaFicha`, `IArmaDaFicha`, `IItemDaFicha`, …) with `Id`, `SheetId` and the
  fields shared by every kind that has the feature. Kind-only fields stay off the interface; a type that
  differs per kind becomes a generic parameter.
- The FK property of every per-kind child entity is renamed in C# to **`SheetId`**; `RuinaRpgDbContext`
  maps it with `.HasColumnName("<old name>")` (e.g. `"NpcSheetId"`). Index and FK constraint names derive
  from column names, so the database is untouched.
- Proof that the schema did not move:
  1. `MigrationTests`: no pending model changes after the rename (no migration is added in D);
  2. `tools/migrations/verificar-baseline.sh <1.5.0 ref>` exits 0 on D's head (schema and full dump of a
     fresh database identical to 1.5.0's).

### D.2 The sheet-kind adapter

`ITipoDeFicha<TSheet>` with one implementation per kind (`TipoPersonagem`, `TipoNpc`, `TipoCriatura`)
holds everything the copies used to hard-code:

- the sheet `DbSet` and how to load a sheet;
- the authorization rule (today `GrantedSheetAuthorization.CanEdit(...)`);
- the active-conditions lookup (`CondicoesAtivasDaFicha.Para…Async`);
- named flags for the rule differences listed above (level limits, creature-only Perícias, history
  bonus, …).

Error handling keeps today's check order and status codes exactly. Example, Skills `Update`: unknown or
removed Perícia key → 404; missing sheet → 404; not allowed → 404; level limit or creature-list rule → 400
with the same message string.

### D.3 Generic controllers

- Each feature becomes an abstract generic controller in `src/RuinaRPG.Api/Controllers/Fichas/`, e.g.
  `PericiasDaFichaController<TSheet, TSkill, TAtributo, TResponse>`. Each kind keeps a **sealed** subclass
  carrying its current `[Route]`, the construction of its own `Contracts` record, and overrides only for
  real rule differences. Actions inherited from an abstract base are discovered by ASP.NET Core, so URLs
  do not change.
- Where kinds have different endpoint sets, the controller is split by sub-resource without changing any
  URL. Possessions becomes `InventarioDaFicha` (Character, NPC), `EspoliosDaCriatura` (Creature),
  `ArtefatosDaFicha`, `AfeicoesDaFicha`, `CaracteristicasDaFicha` and `TracosRaciaisDaFicha`
  (Character, NPC); each kind registers exactly the endpoints it has today. An endpoint only one kind has
  lives in that kind's subclass.
- Same pattern for Skills, Attributes, Masteries, Arsenal, SpellAbilities (three kinds), Runes,
  Affinities and Equipagem (Character, NPC).
- **Sheets controllers:** only operations identical across kinds after normalization move to
  `FichasController<TSheet, …>`. Kind-specific create/update fields, Character-only history/diary and NPC
  grants stay in the subclasses.

### D.4 Shared stats core

`FichaStats<TSheet>` holds the shared calculations (artifacts, equipment and penalties, sub-attributes,
maximums, Perícias, EAP); each kind supplies a small hook for what differs (attribute enum, history bonus,
…). `CharacterSheetStats`, `NpcSheetStats` and `CreatureSheetStats` remain as thin registered types in
`Program.cs`, so injection sites keep their shape.

### D.5 Divergence rule

Before moving any method, its versions are compared side by side:

- a real behavior difference → a named hook or flag in the adapter;
- a cosmetic difference (names, order of independent statements) → unified;
- an **accidental** divergence (one kind validates something the others don't) → behavior kept through a
  hook, **not** fixed in D, and recorded in a "Divergências encontradas" list appended to this spec for
  the user to decide later.

### D.6 `/api/rulebook` N+1

`BuildEquipagemAsync` loads every kit's fixed items in one query (`Include`, or one
`Where(i => kitIds.Contains(i.KitId))` grouped in memory) instead of one query per kit. A frozen-output
test captures today's JSON from the seeded test database first; the new code must return identical bytes.
Target p50 with `tools/bench/measure-api-latency.sh`: **< 30 ms** (today 69.8 ms).

## Tests

### Order of work

1. **Production code first, tests untouched.** Each feature group is moved to the generic form while
   every existing test file stays unchanged and green. A test that seems to need editing means behavior
   changed: the code change is reverted, not the test.
2. **Tests consolidated only after all production code is**, in separate tasks; no task changes
   production code and its tests together.

### New tests (written first, TDD)

- **Route inventory:** a test that lists every action the API exposes (HTTP method, route template,
  parameters, request and response types) and compares it to a committed file captured **before** any
  D change. Any added, removed or reshaped endpoint fails it.
- **Rulebook frozen output** (D.6).
- **Schema:** no pending model changes (D.1).

### Test consolidation

- Per-kind tests of the same feature become one abstract generic class (e.g.
  `PericiasDaFichaTestes<TTipo>`) with one tiny subclass per kind, so xUnit runs every case per kind.
  Cases that only exist for one kind stay in that kind's class.
- The plan lists every current test (class + method) and its destination. After consolidation the number
  of tests run per kind is ≥ today's, and no assertion is removed or weakened. Duplicates that differ only
  by kind collapse into one generic case run for every kind.

## Measurements

Recorded as an "After D" column in `2026-10-16-versao-2-0-0-baseline.md`, with `tools/bench/`:

| Metric | Before D | Target |
|---|---|---|
| Lines in `src/RuinaRPG.Api/Controllers/` + `Services/` | 15 813 | ≤ 11 500 |
| Lines in `tests/RuinaRPG.Tests.Integration/` | 40 139 | ≤ 34 000 |
| `/api/rulebook` p50 | 69.8 ms | < 30 ms |
| Integration test wall time | recorded | no regression |
| `dotnet build --no-incremental` | recorded | no regression |

A missed target is reported to the user, as in C, without tuning beyond the plan.

## Success criteria

- Whole suite green, `dotnet build` 0 warnings / 0 errors.
- Route inventory test unchanged against the pre-D capture.
- No pending model changes; `verificar-baseline.sh` exits 0.
- Rulebook frozen-output test passes; p50 target met.
- Tests run per kind ≥ before; no assertion removed or weakened.
- Size targets met or explicitly reported.

## Out of scope

- `Contracts` records and the Blazor client (E).
- Merging the per-kind tables or any other schema change.
- Fixing accidental divergences (listed for the user, D.5).
- Encounters, campaigns, auth, catalogs: no per-kind duplication there.

## Divergências encontradas

Divergências acidentais entre os tipos de ficha achadas ao consolidar (D.5): o comportamento de hoje foi
mantido; fica para o usuário decidir depois.

- Maestrias/PUT `{id}`: só o Personagem edita uma Maestria existente; NPC e Criatura só criam e apagam, embora
  Ficha de Criaturas 4.e diga "mesma estrutura do Personagem" (mantido via endpoint só na subclasse
  `CharacterMasteriesController`).
- Perícias/GET `budget`: só o Personagem tem o orçamento de pontos de Perícia; NPC e Criatura não têm o
  endpoint, embora Atributos tenha `budget` nos três tipos (mantido via endpoint só na subclasse
  `CharacterSkillsController`).
- Afeições/PUT `affections/{id}`: só o Personagem edita uma Afeição existente; NPC e Criatura só criam e apagam,
  embora Ficha de Criaturas 5.c diga "sem alteração em relação ao Personagem" e a Ficha de NPCs não mude 5.c
  (mantido via endpoint só na subclasse `CharacterAffectionsController`).
- Inventário/POST `inventory`: o Personagem só aceita Item Geral ("Item Geral não encontrado.", Ficha de Personagem
  5.a); o NPC aceita qualquer Item do Catálogo, inclusive Arma/Armadura/Escudo/Artefato ("Item não encontrado."),
  embora a Ficha de NPCs não mude 5.a (mantido via hook `ConferirItemAsync` de `InventarioDaFichaController`).
- Magias e Habilidades/GET `spell-abilities` e GET `spell-abilities/passivas-disponiveis`: o Personagem nega
  acesso com 404, enquanto seus POST/PUT/DELETE de Magias (e todos os outros endpoints da Ficha de Personagem)
  negam com 403 (mantido via `FichaParaLeituraAsync`, hoje em `FichaControllerBase`, que nega com 404 nos três tipos).
- Estatísticas/Peso na Movimentação (`sub-attributes` e tudo que usa a Movimentação): na Criatura, o Peso Total
  Carregado ainda soma toda Arma/Armadura/Escudo do Catálogo, equipados ou não, que é a fórmula anterior à
  correção de 2026-09-08 (`2026-09-08-inventory-weight-and-capacity-design.md`, que deixou a Criatura fora de
  escopo). No Personagem e no NPC só contam o Inventário e as Armas/Escudos desequipados (mantido via hook
  `CargaAsync` de `FichaStats`, sobrescrito em `CreatureSheetStats`).
- Fichas/leituras da própria ficha: o Personagem nega com 404 o GET `{id}`, `{id}/racial-ability`,
  `{id}/variantes-liberadas` e `{id}/historia/imagens` ("não confirma a um estranho que a ficha existe"), mas nega
  com 403 os GET `{id}/level-up-notice`, `{id}/sub-attributes`, `{id}/condicoes-ativas`, `{id}/equipment-penalties`
  e `{id}/modificador-de-dano`, que revelam o mesmo (mantido via `FichaParaLeituraAsync` × `FichaEditavelAsync` em
  `FichasController` / `FichaComLinhagemController`).
- Fichas/DELETE `{id}`: o Personagem só é apagado pelo Mestre da campanha (papel GM; 404 para qualquer outro); NPC e
  Criatura são apagados por quem pode editá-los, inclusive o jogador a quem foram concedidos, embora a Campanha R0010
  diga que, concedida a ficha, "criação e exclusão continuam exclusivas do GM" (mantido via `Delete` próprio em
  `CharacterSheetsController` e `ApagarAsync` de `FichasController` para NPC/Criatura).
