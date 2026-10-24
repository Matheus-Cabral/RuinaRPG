# Peso do Inventário e Capacidade Extra — Design

**Status:** Approved by user in chat (design sections), ready for implementation planning.

## Goal

Show a real "Peso Atual / Peso Máximo" on the Inventário section of Personagem
and NPC's Posses tab, with an overweight warning when the max is exceeded, and
let the GM add "extra capacity" items (backpacks etc.) to the catalog that
raise a character's max carry weight without adding their own weight to the
current total.

This also fixes a pre-existing bug: `Peso Total Carregado` (the value that
already feeds the Movimentação formula today) has never included the
Inventário list at all — only Armas/Armaduras/Escudos. Movimentação has been
wrong for every sheet that has anything in its Inventário since the feature
was built.

## Scope

**In scope:** `CharacterSheetsController`/`FichaDePersonagem.razor`,
`NpcSheetsController`/`FichaDeNpc.razor`, the shared `SubAttributesResponse`
contract, `SubAttributeFormulas.Movimentacao`, and Item Geral's catalog
schema/UI (`ItemsController`, `CatalogoItemForm.razor`).

**Explicitly out of scope:** `CreatureSheetsController`/`FichaDeCriatura.razor`
— Criatura's 5.a section is "Espólios" (loot dropped when defeated), not a
carried inventory (Requisitos - Ficha de Criaturas R0005: "não um inventário
que ela carrega e usa"). Its Movimentação formula and `SubAttributes` action
are untouched; the shared `SubAttributesResponse`'s two new fields are simply
`null` there. Artefatos (5.b) stay out of the weight formula, as they are
today — the requirement never included them, and this work doesn't add them.
Weapon/Shield equip UI is unchanged; only how their weight is aggregated
changes.

## The weight formula (corrected)

> **PesoAtual** = Σ(Peso × Qtd) of every Inventário (5.a) row whose item has no
> Capacidade Extra, **+** Σ Peso of every **desequipada** Arma/Escudo.
>
> Armaduras never count — a `CharacterArmorSlot`/`NpcArmorSlot` with an
> `ItemId` is inherently worn (no separate equipped/unequipped state exists
> for armor in this schema), so including it would mean armor always counts,
> which contradicts "só o que não está equipado conta."
>
> **PesoMaximo** = `piso((Força + Vigor) / 2)` + Σ(Capacidade Extra × Qtd) of
> every Inventário row whose item **has** a Capacidade Extra.
>
> **Sobrepeso (aviso)** = `PesoAtual > PesoMaximo` (client-derived from the
> two fields below — no separate boolean on the wire).

Both `PesoAtual` and `PesoMaximo` stay `decimal` end-to-end (Item.Peso is
already `decimal` in the catalog) so a sheet with several sub-1 weight items
displays real fractional totals instead of a silently truncated integer,
which is what happens today (the current code casts to `(int)` before
comparing to Limite de Carga).

`SubAttributeFormulas.Movimentacao` changes shape: it currently takes
`(int agilidade, int artefato, int pesoTotalCarregado, int forca, int vigor)`
and computes `limiteDeCarga`/`sobrepeso` internally from raw `forca`/`vigor`.
Since `PesoMaximo` now needs the Capacidade Extra term (unknown to this pure
formula function), the "floor((Força+Vigor)/2)" piece moves to a new
`CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal)` in
Domain, called once by the controller (which already builds `PesoMaximo` for
the response anyway). `Movimentacao`'s new signature is
`(int agilidade, int artefato, decimal pesoAtual, decimal pesoMaximo)`; it
keeps owning the "turn the decimal overage into an int penalty" step:
`sobrepesoPenalidade = (int)Math.Ceiling(Math.Max(0m, pesoAtual - pesoMaximo))`
— rounding up, so half a kilo over the limit still costs 1 point rather than
being silently discarded by truncation. The 3 existing unit tests for
`Movimentacao` (`SubAttributeFormulasTests.cs`) get updated to the new
signature, not dropped — same behavior at integer inputs, now precise at
fractional ones.

`CarryWeightCalculator` (new, Domain, pure, unit-tested) also gets
`CountsTowardPesoAtual(decimal? capacidadeExtra) => capacidadeExtra is null or 0`
— the one business rule ("does this item's own weight count") worth locking
down as its own testable unit rather than inlined in a controller LINQ query.

## Capacidade Extra (the "mochila" mechanic)

A new nullable field on **Item Geral only** (not Arma/Armadura/Escudo/
Artefato — backpacks are things that go in the existing Inventário list,
Item Geral's `Subcategoria` "Equipamentos de Aventura" already covers this
kind of item):

- **Capacidade Extra**: `decimal? ≥ 0`, default `null` (displays **—**,
  matching this doc's own NULL-field convention). When null or 0, the item
  behaves exactly as it does today — pure Peso, no effect on capacity.

When an Item Geral row with a non-null/non-zero Capacidade Extra sits in a
sheet's Inventário: its own `Peso × Qtd` is **excluded** from `PesoAtual`
(the exclusion is keyed on Capacidade Extra being set, not on what Peso
happens to be — a GM can still enter a nonzero Peso on a container item for
flavor without it ever being added to the carried total), and
`Capacidade Extra × Qtd` is **added** to `PesoMaximo`. The row's own
Peso/Total columns in the Inventário table render exactly as any other row
(no special-casing in the per-row display) — only the page-level aggregate
sums differ.

Capacidade Extra scales with Qtd (2 backpacks of +5 = +10), the same
precedent `Peso × Qtd` already sets for the Total column.

## Contracts

- `CreateItemRequest`/`UpdateItemRequest`/`ItemResponse` (all three, existing
  flat positional records shared by every Item Tipo) gain a trailing
  `decimal? CapacidadeExtra` — same pattern as `DurabilidadeMaxima` etc.
  before it. `ItemsController`'s `ItemGeral` create/update/`ToResponse`
  branches map it; every other Tipo branch passes it through as `null` and
  ignores it on write (mirrors how Arma-only/Armadura-only fields already
  behave for other Tipos).
- New EF column: `ItemGeral.CapacidadeExtra` (`decimal?`), one migration.
- `SubAttributesResponse` (shared by Character/Npc/Creature) gains
  `decimal? PesoAtual, decimal? PesoMaximo` at the end. Character/Npc's
  `SubAttributes` actions compute and populate them for real; Creature's
  passes `null, null` (with a one-line comment pointing at this doc,
  matching this codebase's convention for a deliberately-out-of-scope
  field — see e.g. the Criatura Espólios precedent).

## UI

`FichaDePersonagem.razor` and `FichaDeNpc.razor`'s "Inventário" `<Section>`
gets, above the existing item table: a "Peso: `PesoAtual` / `PesoMaximo`"
line, and — only when `PesoAtual > PesoMaximo` — a `MudAlert Severity="Warning"`
reading something like "Sobrecarregado — reduz Movimentação." No new HTTP
call: both pages already load `SubAttributesResponse` in `OnInitializedAsync`
(consumed today only by the Sub-Atributos section), this just renders the two
new fields a second place.

`CatalogoItemForm.razor`'s Item Geral field group gains a "Capacidade Extra"
`MudNumericField T="decimal?"`, next to Peso, following the same optional-
field convention already used for e.g. Armadura's RF/RM.

## Testing

- `CarryWeightCalculatorTests` (new): `PesoMaximo` formula (floor + extra
  term), `CountsTowardPesoAtual` (null → true, 0 → true, positive → false).
- `SubAttributeFormulasTests.cs`: update the 3 existing `Movimentacao` tests
  to the new `(agilidade, artefato, pesoAtual, pesoMaximo)` signature; add a
  fractional case (e.g. `pesoAtual = 5.5m, pesoMaximo = 5m` → penalty 1, not
  0) proving the ceiling behavior the old `(int)` cast couldn't express.
- Integration: `CharacterSheetsControllerTests.cs`/`NpcSheetsControllerTests.cs`
  already cover `GET .../sub-attributes` (existing Movimentação/Iniciativa
  tests) — add cases there for: an Inventário item now counting toward
  `PesoAtual` (the bug fix — write this one to fail against the pre-fix
  aggregation, proving it actually catches the bug); an unequipped weapon/
  shield counted but the equipped one of the same Tipo excluded; an armor
  slot never counted regardless of contents; a Capacidade Extra item raising
  `PesoMaximo` while its own Peso is excluded from `PesoAtual`.
  `CreatureSheetsControllerTests.cs` gets one new case confirming
  `PesoAtual`/`PesoMaximo` come back `null` (the deliberate out-of-scope
  decision, not an oversight).
- `ItemsController`-side: `CreateItemRequest`/`UpdateItemRequest` round-trip
  for an Item Geral with Capacidade Extra set, and confirm it's silently
  ignored (not rejected) for a non-ItemGeral Tipo, matching how other
  Tipo-specific fields already behave.

## Migration / deploy note

New nullable column, no backfill needed (`NULL` is the correct "not a
container" default for every existing Item Geral row). Standard
`dotnet ef migrations add` + this repo's normal `make migrate` step on the
next deploy — no data risk, unlike the Atributo-enum question raised in the
prior round on this branch of work.
