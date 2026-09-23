# Equipagem — Construtor de Subcategoria (rectification) — Design

**Status:** approved by user 2026-09-23, ready for planning.

## Overview

The Equipagem feature (already shipped) resolves a kit's player-choice slots by
filtering the GM's own catalog on `Item.Subcategoria` — a free-text field the
GM types when registering an item. Because that text is arbitrary, a GM's own
naming ("Arco" vs "Arcos", a whole different word entirely) can silently fail
to match a slot's filter, leaving the slot with zero options and no
explanation — the exact gap the final review's finding #1 flagged and the
user is now asking to fix at the root, not just paper over in the UI.

The fix: give the Rules Auditor a closed, per-Tipo vocabulary of
**Categoria** and **Família** values; let a GM opt an item into that
vocabulary via a "Item Inicial" checkbox on the Catálogo item form, which
swaps the free-text Subcategoria field for two selects drawn from that
vocabulary and composes a predictable string
(`"Equipamento inicial - {Tipo} - {Categoria} - {Familia}"`); and have
Equipagem's choice slots match on the parsed **Família** segment against the
same closed vocabulary, instead of comparing the whole Subcategoria string.

This also expands choice slots from Arma-only to Arma/Armadura/Escudo/
Artefato, since the vocabulary approach removes the reliability problem that
made expanding choice-slot Tipo coverage risky before.

## Data model

`Item`'s abstract base class is **not** touched — `Subcategoria` stays
declared per-subtype (matches the user's explicit "mantenha do jeito que
está" — no base-class refactor). `ItemGeral`/`Arma` already declare it;
`Armadura`/`Escudo`/`Artefato` each gain their own:

```csharp
public string? Subcategoria { get; set; }
```

New global table (no GmId — same treatment as `Historico`/`EquipmentKit`):

```csharp
public enum SubcategoriaFacet { Categoria, Familia }

public class SubcategoriaOption
{
    public Guid Id { get; set; }
    public ItemTipo Tipo { get; set; }          // Arma, Armadura, Escudo, Artefato (never ItemGeral)
    public SubcategoriaFacet Facet { get; set; }
    public required string Valor { get; set; }
    public bool IsDeleted { get; set; }
}
```

`EquipmentKitChoiceSlot` gains one nullable field:

```csharp
public ArmorSlotType? ArmorSlot { get; set; }   // required when Tipo=Armadura, must be null otherwise
```

`ArmorSlotType` already exists (`RuinaRPG.Domain.CharacterSheets`, 3 values:
`Capacete`, `Superior`, `Inferior`).

**Contracts**: `EquipmentKitChoiceSlotResponse`/`EquipmentKitChoiceSlotInput`
(the Auditoria-facing CRUD contracts, `RuinaRPG.Contracts.Rules`) both need a
trailing `string? ArmorSlot`, appended after `BonusQtd` — append-only, same
discipline as every other trailing-field addition in this codebase. The
player-facing `EquipmentKitChoiceSlotOptionResponse`/
`EquipmentKitEligibleItemResponse` (what `GET .../equipagem/kits` returns)
do **not** need it — the player never picks the armor slot, only which item
fills the kit-defined slot, so it stays purely server-internal from
`EquipmentKitChoiceSlot` through `EquipmentGrantPlanItem` to `AddGrant`.
New contracts for the vocabulary: `SubcategoriaOptionResponse(string Id,
string Tipo, string Facet, string Valor)`,
`CreateSubcategoriaOptionRequest(string Tipo, string Facet, string Valor)`.

One migration: adds `Subcategoria` (nullable string) to `Armadura`/`Escudo`/
`Artefato`'s columns on the shared `Items` table, creates `SubcategoriaOptions`,
adds `ArmorSlot` (nullable int) to `EquipmentKitChoiceSlots`.

## The composed Subcategoria string

Exactly 4 segments joined by `" - "`, no grammar/pluralization logic —
Categoria and Família are both raw, Auditor-typed values from their own list:

```
"Equipamento inicial - {Tipo} - {Categoria} - {Familia}"
```

e.g. `"Equipamento inicial - Arma - Mágica - Varinha"`,
`"Equipamento inicial - Arma - Distância - Arcos"`.

The literal `"Equipamento inicial"` prefix **is** the signal Equipagem uses to
recognize the item — there is no separate boolean column. The "Item Inicial"
checkbox on the item form is pure client-side UI state (toggles which input
mode is shown); it is never sent to the server as a field.

## Catálogo item form: "Item Inicial" checkbox

`CatalogoItemForm.razor`, for Tipo ∈ {Arma, Armadura, Escudo, Artefato} only
(never ItemGeral): a checkbox that swaps the free-text Subcategoria
`MudTextField` for two `MudSelect`s (Categoria, Família), each populated from
`GET api/subcategoria-options?tipo={tipo}` filtered by `Facet`. On submit,
compose the 4-segment string client-side and send it as the existing
`Subcategoria` field on `CreateItemRequest`/`UpdateItemRequest` — **no
contract change**, both records already carry `Subcategoria`.

On edit-load: if the loaded item's `Subcategoria` splits on `" - "` into
exactly 4 parts with `parts[0] == "Equipamento inicial"` and
`parts[1] == tipo.ToString()`, pre-check the box and pre-select
`parts[2]`/`parts[3]` into the two selects; otherwise default to free-text
mode. This means editing an item built by the constructor reopens in
constructor mode automatically.

`ItemsController.Create`/`Update`: `Subcategoria = request.Subcategoria` is
already wired for `ItemGeral`/`Arma`; add the same line to the `Armadura`/
`Escudo`/`Artefato` branches now that the property exists. `ToResponseAsync`
similarly needs `Subcategoria` included in those 3 types' response
construction (currently passes `null` there since the field didn't exist).

## Equipagem choice slot: Família-based matching, wider Tipo support

**Auditoria: Equipagem** (existing page, `/auditoria/equipagem` — **no new
page**, per the user's explicit correction; the vocabulary-management UI
below is a new section on this same page): gains a "Construtor de
Subcategoria" section — a Tipo selector (Arma/Armadura/Escudo/Artefato) plus
two add/remove lists (Categorias, Famílias) for that Tipo, backed by a new
`SubcategoriaOptionsController` (`api/subcategoria-options` — open GET,
Auditor-gated POST/DELETE; no PUT, since renaming a value is delete+re-add,
matching this app's existing simple-flat-list precedent). This section must
ship in the same task as the choice-slot Tipo/Família UI below, since the
Família select there depends on it having data.

Choice-slot authoring (same page, existing sub-form) changes:
- `Tipo` select now offers **Arma, Armadura, Escudo, Artefato** (not
  hardcoded "Arma").
- When `Tipo == Armadura`, an additional `ArmorSlot` select appears
  (Capacete/Superior/Inferior) — required.
- The "Subcategorias" free-text input becomes a multi-select of **Família**
  values, sourced from `SubcategoriaOption` filtered to the slot's chosen
  Tipo (`Facet == Familia`) — no more typing raw strings.

`EquipmentKitsController.ValidateRequest`: choice-slot `Tipo` validation
relaxes from "must be Arma" to "must be Arma, Armadura, Escudo, or
Artefato" — Armadura is now a legal choice-slot Tipo, unlike fixed
`EquipmentKitItem` rows, which still reject it (see "Explicitly out of
scope" below). When `Tipo == Armadura`, `ArmorSlot` is required; when
`Tipo != Armadura`, `ArmorSlot` must be absent (reject if provided).

`EquipmentKitGrantService.ResolveEligibleOptionsAsync`: Tipo-dispatches via a
switch to the matching concrete `DbSet<T>` (`Arma`/`Armadura`/`Escudo`/
`Artefato` — same style as `ResolveOrCreateFixedItemAsync`'s existing
per-Tipo switch, for consistency with this file's established idiom), scoped
by `GmId`, then filters in-memory: an item is eligible if its `Subcategoria`
splits into exactly 4 `" - "`-joined parts with
`parts[0] == "Equipamento inicial"`, `parts[1] == slot.Tipo.ToString()`, and
`parts[3]` (Família) is one of the slot's stored Família values — **OR**
(backward compatibility with the already-shipped Caçador/Arcano/Ocultista/
Patrulheiro kits) the item's raw `Subcategoria` exactly equals one of the
slot's stored values, the pre-existing matching behavior. Both checks run in
parallel against the same stored list — no schema change to
`EquipmentKitChoiceSlot.SubcategoriasCsv`, its semantics just widen to accept
either a legacy raw Subcategoria string or a new Família name.

`BuildPlanAsync`'s conditional-bonus check (`slot.BonusSubcategoria`) gets
the same dual-match treatment: compare against the selected item's raw
`Subcategoria` (legacy) OR its parsed Família (new) — preserves the
already-shipped Caçador kit's "10 Flechas de Madeira se Arcos" bonus without
requiring it to be re-authored.

## Grant flow: Armadura goes by UPDATE, not INSERT

Unlike Arma/Escudo/ItemGeral/Artefato (which insert a new
`CharacterWeapon`/`CharacterShield`/`CharacterInventoryItem`/
`CharacterArtifact` row), a granted Armadura **updates** the sheet's existing
`CharacterArmorSlot`/`NpcArmorSlot` row for the slot's target `ArmorSlot` —
every sheet already has one pre-seeded row per `ArmorSlotType` (created at
sheet-creation time). Same effect as the existing
`PUT .../armor-slots/{slot}` endpoint: set `ItemId`, reset
`DurabilidadeAtual = item.DurabilidadeMaxima ?? 0`.

`EquipmentGrantPlanItem` gains a field:

```csharp
public record EquipmentGrantPlanItem(ItemTipo Tipo, Guid ItemId, int Qtd, int? DurabilidadeMaxima, ArmorSlotType? ArmorSlot);
```

`ArmorSlot` is populated from the **slot definition** (the Auditor's choice
when authoring the kit), never from the player — the player only picks which
Arma/Armadura/Escudo/Artefato fills that predetermined slot.

`CharacterEquipagemController.AddGrant`/`NpcEquipagemController.AddGrant`
gain an `ItemTipo.Armadura` case that updates the matching `CharacterArmorSlot`/
`NpcArmorSlot` row instead of inserting.

## Explicitly out of scope (confirmed with the user)

- **Fixed `EquipmentKitItem` rows still never support Armadura.** The
  Subcategoria-matching reliability problem only affects choice slots
  (which filter); fixed rows already resolve by exact Nome, unaffected by
  any of this.
- **No new Auditoria page.** Everything above lives on the existing
  `/auditoria/equipagem` page — explicit user correction from an earlier
  draft of this design.
- **`Item`'s abstract base class is not touched.** `Subcategoria` stays a
  per-subtype declaration — explicit user correction from an earlier draft.

## Docs to update

- `Requisitos - Catálogo de Itens e Equipamentos.md` — document the new
  `Subcategoria` field on Armadura/Escudo/Artefato (R0005/R0006/R0009
  currently omit it), and the "Item Inicial" constructor convention.
- `Requisitos - Auditoria de Regras.md` — extend the existing Equipagem
  section (R0009) to cover the Construtor de Subcategoria vocabulary
  management and the choice slot's new `ArmorSlot` field.
- `Requisitos - Modelo de Dados.md` — new `SubcategoriaOptions` table, new
  `Subcategoria` columns, new `ArmorSlot` column.

## Test strategy

- Unit: the 4-segment parse/compose round-trip (compose then parse yields
  the same Categoria/Família; a non-matching-pattern string parses as "not
  a constructed item").
- Integration: `SubcategoriaOptionsController` CRUD + Auditor gating;
  `ItemsController` Create/Update persist and return Subcategoria for
  Armadura/Escudo/Artefato; `EquipmentKitsController` choice-slot validation
  (Armadura requires ArmorSlot, others reject it); `EquipmentKitGrantService`
  resolves eligible options via both the legacy-string and new-Família paths
  for the same slot; a full Choose flow granting an Armadura updates the
  correct `CharacterArmorSlot`/`NpcArmorSlot` row (not an insert); the
  already-shipped Caçador conditional-bonus test still passes unmodified
  (proves backward compatibility).
