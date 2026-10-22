# Versão 2.0.0 — Spec E: Client consolidation — Design

**Status:** draft, awaiting user approval (builder check done 2026-10-19). Version 2.0.0, branch `release/2.0.0`.
Detailed spec of sub-project **E** of the roadmap (`2026-10-16-versao-2-0-0-roadmap-design.md`).

## Intent

E is preventive maintenance. Nothing is broken. Today a change to one tab of a sheet (Perícias, Posses,
Magias…) usually has to be made three times, once per sheet page (Personagem, NPC, Criatura), because each
page is a large file with near-identical copies. After E, such a change is made once and holds for all
three kinds, so future updates are cheaper and the three sheets can't drift apart by accident. Faster
build/test/load is a welcome bonus, not the goal.

User decisions taken while brainstorming E:

- **Scope is the three sheet pages only.** `CampanhaDetalhe.razor` (1 034 lines, 7 tabs, but no
  duplication) is **out of scope** and left as a later suggestion. The user picked the sheets when asked
  to choose one.
- **The user sees no change:** same screens, fields, order, texts, icons and behavior (roadmap rule).
- **Shape approved by the user:** one short page per kind that assembles shared tab components; each tab
  component receives the sheet kind and derives the API address from it; kind-only parts stay outside the
  shared components.
- **Anti-goal (approved):** a shared component full of `if Personagem / if Criatura` branches. When a tab
  differs a lot between kinds, the different part becomes its own component instead of more conditions.
- **Accidental visible differences between the pages** (user, 2026-10-19, answering the builder check):
  - **swapped order** (e.g. Vitalidade/Foco in Combate) → all kinds follow the **Personagem** order;
  - **Runas** → all kinds that have Runas use the **Personagem** table;
  - **Pontos de Perícia**: NPC/Criatura's "Pontos de Perícia Gastos: N" is correct and stays. They have
    no skill budget; Personagem keeps "Gasto / Disponível". This is a real difference: a capability flag.
  - Any **other** apparently accidental difference found while working is **listed for the user** with a
    recommendation, and decided before that tab's code is written. Decisions are recorded in this spec
    under "Differences decided". Intentional differences backed by the API or an earlier spec (inline
    edit of Maestrias/Afeições only on Personagem, `CatalogoItemPicker` with "+" on NPC/Criatura vs
    `EntityPicker` on Personagem, from the 2026-09-15 cadastro-inline spec) stay as they are, without
    asking.
  - The E.1 safety net pins the **decided** result: where a difference is unified, the net asserts the
    Personagem version for every kind, and that change is the only visible change E makes.
- **Manual check:** the dev stack at 192.168.80.16 (this server) is where the user checks updates by
  hand. At the end, E is deployed there (`make deploy`, then `make migrate`, since the stack runs in
  Production mode). The user does the manual check; the builder deploys and confirms `/api/health`.
- **D's leftover server follow-ups** (`Db` in `FichaControllerBase`, `IFichaStats`, flag single source,
  `GmId` queried twice): **not part of E** (builder's call). On the server, E only changes type names
  where records are merged.
- **Targets are reference numbers only.** Before/after are recorded; a missed number is reported, not a
  failure (D missed every numeric target; the user cares that the code becomes easier to change).

## Findings that drive E

Measured on `30a8fa4`. Type names normalized (`Personagem|Character|Npc|NPC|Criatura|Creature` removed),
leading whitespace ignored, counting changed lines of a diff:

| Section | Lines P / N / C | Diff P↔N | Diff P↔C |
|---|---|---|---|
| Header (before tabs) | 96 / 98 / 46 | 20 | 70 |
| Informações Básicas | 126 / 122 / 122 | 24 | 68 |
| Atributos & Perícias | 131 / 117 / 53 | 22 | 86 |
| Combate | 261 / 241 / 258 | 34 | 79 |
| Magias & Habilidades | 100 / 95 / 57 | 9 | 45 |
| Posses | 94 / 88 / 77 | 14 | 51 |
| Antecedentes | 73 / 18 / — | 57 | — |
| Diário | Personagem only | | |
| `@code` | 1 219 / 1 136 / 869 | 487 (393 ignoring order) | 840 (706) |

- Files: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` 2 109 lines, `FichaDeNpc.razor` 1 924,
  `FichaDeCriatura.razor` 1 490: 5 523 of the client's 19 986 `.razor`/`.cs` lines.
- `@code` methods: Personagem 90, NPC 82, Criatura 67; **56 exist in all three**.
- Kind-only behavior:
  - only Personagem: Diário (add/edit/delete entries), XP and PI deltas, Perícia crítico delta, Afeição
    update, Maestria update, campaign name;
  - NPC and Criatura: Nível and Experiência atual edited by hand (`SaveNivelAsync`,
    `SaveExperienciaAtualAsync` and their notify hooks);
  - only Criatura: Espólios (add/delete/qty), its own características dialog
    (`NovaCaracteristicaDeAsync`), rules-auditor check, item search over all items, a much smaller
    Atributos & Perícias tab; no Antecedentes tab.
- API addresses differ only by prefix: `character-sheets/…`, `npc-sheets/…`, `creature-sheets/…`.
- `Contracts`: each kind has its own records. **27 of the 30 NPC records are structurally identical to
  the Character ones** (exceptions: `AddNpcRuneRequest` differs only in comments, `NpcSheetResponse`,
  `UpdateNpcSheetRequest`). For Creature, 16 match Character and 8 differ in real ways (e.g. weapon/armor
  `DurabilidadeMaximo` vs `DurabilidadeMaxima`, `ItemId` nullable, no `Peso` on weapons, no
  `ContaHistorico` on skills, no `IsRacial` on traits). The D spec left merging these records to E.
- Client tests touching the sheets: about 2 000 lines in `tests/RuinaRPG.Tests.Client/Pages/Ficha*Tests.cs`
  and similar; some are already `[Theory]` over kinds (`"character"`, `"npc"`).
- Reference numbers (baseline doc): client tests 936 passing, ~28 s; client `.razor` 18 240 lines.

## Design

### E.1 Safety net first

Before any production code moves, add bUnit tests that pin what each tab renders today **for each kind
that has it**, plus the HTTP calls its main actions make (address, method, body). They pass on the current
code, are committed in their own commit, and must pass **unchanged** after every later step, like D's
`RotasDaApiTests`. Prefer asserting on visible text, labels and requests, not on component structure, so
the net survives the split. Existing sheet tests are kept as they are; when a test only needs a new
component name it may change in a separate, reviewed step (never together with the code it guards).

### E.2 Merge the per-kind `Contracts` records

- Records whose JSON is identical across kinds are replaced by one shared record in a new namespace
  (`RuinaRPG.Contracts.Fichas`, builder picks file names). Character and NPC almost entirely; Creature
  where it matches.
- Records that really differ stay per kind. **JSON on the wire does not change** for any endpoint
  (property names, order not required, nullability, defaults): renaming `DurabilidadeMaximo` →
  `DurabilidadeMaxima` or similar is not done.
- The API side changes only in which type it names; D's generic controllers lose the type parameters
  that become a single type. Routes are untouched: `RotasDaApiTests` stays green without regenerating
  its snapshot, and the integration suite stays green.
- `tools/RuinaRPG.Smoke` and any other consumer compile against the merged types.

### E.3 Sheet kind on the client

A small client-side description of the kind (name builder's call, e.g. `TipoDeFichaCliente`): its API
prefix and the facts the shared components need (has Diário, has Antecedentes, edits Nível by hand, has
Espólios…). It is a value passed down, not a service with HTTP of its own. Facts are named after the
capability, not the kind ("EditaNivelAManual", not "IsNpcOuCriatura"), so components ask "does this
sheet have X", never "is this a Creature".

### E.4 Shared tab components

- One component per tab under `src/RuinaRPG.Client/Shared/Ficha/` (folder name builder's call):
  Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses, Antecedentes. Each
  takes the sheet id, the kind description, the loaded sheet data it needs and callbacks for changes that
  other tabs must see (the page keeps reloading/auto-save coordination: `AutoSaveCoordinator`,
  `RecargaAdiada`, condições root, SignalR, as today).
- The 56 shared methods move with the tab that uses them; logic used by several tabs goes into a shared
  helper/state class, not copied.
- **Kind-only parts are their own components** used only by the page that needs them: Diário and XP/PI
  events (Personagem), Nível/Experiência manual (NPC + Criatura), Espólios (Criatura), the Criatura
  Atributos & Perícias body if after the split it still differs as much as the table above suggests.
- A shared component may branch on a capability flag for a **small** difference (a field shown or not).
  When a branch grows beyond a few lines or two branches pile up in one place, split it out instead.
- Existing `Shared/` components already used by the sheets (`SubAtributosSection`,
  `HabilidadesPassivasSection`, `ProgressaoDoNivelSection`, …) are reused, not rewritten.

### E.5 The three pages

`FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor` keep their `@page` routes and
become short: load the sheet, own the page-level state (breadcrumbs, header, auto-save, reload,
condições, SignalR), and lay out the tabs in today's order with today's titles. Whether a common page
base/component also absorbs the header and loading is the builder's call, under the same anti-goal.

### E.6 Tests follow

Client tests of the shared components become `[Theory]` over the kinds that have the feature
(`Personagem`, `NPC`, `Criatura`), so each shared behavior is covered for every kind that has it. Tests
already duplicated per kind collapse into those theories. TDD (Técnico R0011) applies to every new unit:
new helper/state classes get their failing test first.

### E.7 Order of work

Safety net (E.1) → contracts merge (E.2) → kind description (E.3) → one tab at a time, smallest first
(Magias & Habilidades, Posses, …, Combate last) with the whole suite green after each → kind-only parts →
pages slimmed → tests collapsed → measurements. Each step is a commit; push as work progresses.

## Success criteria

- `dotnet build` 0 warnings / 0 errors; unit, client and integration suites green.
- E.1 safety-net tests unchanged and green at the end; `RotasDaApiTests` snapshot unchanged.
- E deployed to the dev stack (192.168.80.16) with `/api/health` = `OK`, and the user's manual check of
  the three sheets there.
- No `Pages/FichaDe*.razor` keeps a copy of a tab that a shared component provides.

## Reference numbers (reported, not gates)

Recorded before and after in the baseline doc: lines of the three pages and of the new shared
components; client `.razor`/`.cs` total; `Contracts` lines; client test count and time; client build
time; `_framework` size. Expected (guess): the three pages plus shared components go from 5 523 to about
2 500–3 000 lines. No meaningful change in download size is expected (Blazor ships the same compiled
assemblies).

## Out of scope

- `CampanhaDetalhe.razor` and every other page (Auditoria*, GerenciadorDeEncontros…).
- Any visible change other than the unified accidental differences above, including fixing D's 8
  "Divergências encontradas" (still awaiting user decision).
- Server behavior, routes and the DB schema.

## Differences decided

| Difference | Decision |
|---|---|
| Vitalidade/Foco order in Combate (and any other swapped order) | Personagem order for all |
| Runas: table (Personagem) vs one line of text (NPC) | Personagem table for all |
| Pontos de Perícia: "Gasto / Disponível" vs "Gastos: N" | Kept per kind (budget only on Personagem) |
| A1 — racial-trait dialog wording (P second person, N "do NPC") | Kept per kind: one shared dialog, the two texts as parameters (user, 2026-10-22) |
| A2 — NPC "XP por abate e assistência" InfoPopup ("só o GM vê"), none on Criatura | Kept as is: popup text is an optional parameter, none for Criatura (user, 2026-10-22) |
| A3 — "Progressão do nível" panel on P and N, absent on C | Kept as is (`MostraProgressaoDoNivel`) (user, 2026-10-22) |
| A4 — blank line after Atributos and after Perícias on P only | Personagem version for all (N and C gain the gap) (user, 2026-10-22) |
| A5 — granted NPC/Criatura breadcrumb points a player to a GM-only page | Kept as is in E; later suggestion (navigation change, out of E's scope) (user, 2026-10-22) |
| A6 — Criatura ignores a refused attribute/skill change (no message) | Personagem version for all: C shows the server's message, and a successful save clears an earlier error (user, 2026-10-22) |

## Left to the builder

File and type names, the exact parameter list of each tab component, whether page-level state lives in a
cascading value or parameters, how the safety-net tests fake HTTP (reuse what the client test project
already does; tests in the E.1 net use raw JSON instead of `Contracts` types, so E.2 can't force edits to
them), how the shared tabs handle the Creature records that stay separate (generics or small adapters),
and whether a common page base exists.
