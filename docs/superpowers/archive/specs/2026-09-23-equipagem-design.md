# Equipagem (Initial Equipment Kits) — Design

**Status:** approved by user 2026-09-23, ready for planning.

## Overview

`Docs/Sistema RPG/Equipagem.md` defines 12 starting-equipment "kits" (Viajante,
Explorador, Patrulheiro, Caçador, Arcano, Ocultista, Devoto, Artesão,
Sobrevivente, Investigador, Negociante, Aprendiz), each a flavor paragraph plus
a list of items (some fixed, a few "de sua escolha"/"ou" player choices) and
an optional Ciclos grant. A trailing "MUNIÇÃO" note documents one conditional
bonus (Caçador's Arco choice grants 10 Flechas de Madeira).

This feature adds: a one-time "Escolher Equipamento Inicial" button on
Personagem/NPC sheets (Posses tab, above Inventário) that lets the player pick
one kit and have its items land automatically in their proper places; a
GM-auditor-managed catalog of kits (Auditoria page); and a "Equipagem" tab in
the Livro de Regras that mirrors that catalog live (same hybrid pattern as
Históricos: intro text from the source doc, cards from the DB). Criatura
sheets are excluded — Espólios is loot, not starting gear, same reasoning
already applied to Histórico.

## The central architectural fact: `Item` is per-GM, `EquipmentKit` is global

Unlike `Historico`/`Trait` (single global tables, `RequireRulesAuditorAsync()`
gates writes), the `Item` catalog (`ItemGeral`/`Arma`/`Armadura`/`Escudo`/
`Artefato`) is **owned per-GM** — every GM gets their own copy, seeded from
`DefaultCatalogItems.Build(gmId)` at registration
(`src/RuinaRPG.Infrastructure/Items/DefaultCatalogSeeder.cs`). A kit can't
hold a concrete `ItemId` FK the way `Historico` held one, because different
GMs have different `Item` rows (even nominally "the same" item is a different
row per GM).

Resolution: `EquipmentKit` and its child rows (`EquipmentKitItem`,
`EquipmentKitChoiceSlot`) reference items by **Nome + Tipo** (fixed rows) or
by a **filter** (Tipo + optional Subcategorias + optional Tier, for choice
slots), never by ItemId. At apply time the server resolves those against the
*calling sheet's campaign GM's own* `Items`. A fixed `ItemGeral` row whose
Nome isn't found in that GM's catalog is **auto-created** there (Peso=0,
Preço=0, matching the catalog's own "unknown values default to 0"
convention — see `Requisitos - Catálogo de Itens e Equipamentos.md`'s
preamble). A fixed Arma/Escudo/Artefato row is never auto-created (fabricating
weapon/shield stats out of nothing would be wrong) — in practice this only
matters for one row across all 12 kits (see Seed Data below, Patrulheiro's
shield is aliased to an item that already exists in every GM's default-seeded
catalog). A choice slot with zero matching items in a given GM's catalog
simply shows no options for that slot — a GM-catalog-completeness issue, not
something the app should paper over.

## Data model

New `RuinaRPG.Infrastructure.Rules` types (global tables, same folder as
`Historico`):

```csharp
public class EquipmentKit
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Ciclos { get; set; }
    public bool IsDeleted { get; set; }
}

public class EquipmentKitItem
{
    public Guid Id { get; set; }
    public Guid KitId { get; set; }
    public required string Nome { get; set; }       // target Item.Nome, matched case-insensitively
    public ItemTipo Tipo { get; set; }               // ItemGeral | Arma | Escudo | Artefato (never Armadura)
    public int Qtd { get; set; }
    public string? SubcategoriaHint { get; set; }    // used only if Tipo=ItemGeral and the item needs auto-creating
}

public class EquipmentKitChoiceSlot
{
    public Guid Id { get; set; }
    public Guid KitId { get; set; }
    public required string Label { get; set; }        // e.g. "Arma", "Condutor"
    public ItemTipo Tipo { get; set; }                 // always Arma in the seed data, kept general
    public string? SubcategoriasCsv { get; set; }      // comma-joined; null = any subcategoria
    public Tier? Tier { get; set; }                    // null = any tier
    public int Qtd { get; set; }
    public string? BonusSubcategoria { get; set; }     // conditional bonus: granted only if the chosen
    public string? BonusNome { get; set; }             // item's Subcategoria matches this
    public int? BonusQtd { get; set; }
}
```

`ItemTipo` and `Tier` already exist in `RuinaRPG.Domain.Items` — reuse, don't
redefine.

`CharacterSheet`/`NpcSheet` gain:

```csharp
public Guid? EquipmentKitId { get; set; }
```

EF config in `RuinaRpgDbContext`: `EquipmentKitItem`/`EquipmentKitChoiceSlot`
have a required FK to `EquipmentKit` with `OnDelete(DeleteBehavior.Cascade)`
(they're owned children, deleting a kit — only possible when unused, see
Auditoria below — deletes its rows too). `CharacterSheet.EquipmentKitId`/
`NpcSheet.EquipmentKitId` use the established no-navigation-property
`HasOne<EquipmentKit>().WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.SetNull)`
pattern (same as `HistoricoId`).

Migration: `AddEquipmentKitsAndSheetEquipmentKitId`.

## Seed data

Hand-authored C# literal data (`EquipmentKitSeedData.cs`), **not** parsed live
from `Equipagem.md` — unlike `Historico.md`'s clean numbered sections,
`Equipagem.md` is unstructured prose (bare item lines, no bullets, "de sua
escolha"/"ou" choices, a conditional-bonus footnote) that can't be parsed
reliably without inventing a bespoke mini-language. This mirrors the existing
`DefaultCatalogItems.cs` precedent, which is also hand-authored literal seed
data for exactly this reason. `EquipmentKitSeeder.SeedAsync(db)` inserts a kit
only if no `EquipmentKit` with that `Nome` exists yet (insert-missing-only,
same as `HistoricoSeeder` — GM edits via Auditoria are never overwritten by a
later restart). Wired into the same two `Program.cs` call sites
`HistoricoSeeder.SeedAsync` already uses.

All 12 kits, with item Nomes resolved against the existing
`DefaultCatalogItems.cs` catalog (aliases noted) and the 2 new `ItemGeral`
entries this feature introduces:

**New ItemGeral catalog entries** (missing from `DefaultCatalogItems.cs`
today — added there too, so every *new* GM registration has them from day
one; existing GMs get them lazily auto-created the first time a kit needs
them, per the per-GM resolution rule above): "Tônico de Vida simples",
"Tônico de Foco simples" — Subcategoria "Poções e Tônicos" (new, freely
extensible dropdown per `Requisitos - Catálogo de Itens e Equipamentos.md`
R0003), Peso=0, Preço=0.

**Name aliases** (kit references the *catalog's* exact Nome, not the doc's
prose wording): "Rações de viagem" → `Ração de Viagem` (Qtd 2/3 per kit);
"Saco de dormir" → `Saco de Dormir`; "Escudo de Madeira" → `Tampa de Madeira`
(Escudo, Leve, the only wooden-tier shield in the default catalog);
"Manuscrito Arcano (Volume 1)" → `Manuscrito Arcano Vol.1`; "Cera-viz" →
`Cera-viz (Material Ritualístico)`.

1. **Viajante** — Fixed: Mochila×1, Saco de Dormir×1, Corda×1, Tocha×1, Ração
   de Viagem×2. Ciclos: 25.
2. **Explorador** — Fixed: Mochila×1, Tocha×1, Corda×1, Pé de Cabra×1,
   Gazúa×1, Tônico de Vida simples×1. Ciclos: 15.
3. **Patrulheiro** — Choice slot "Arma" (Tipo=Arma, Subcategorias=null/any,
   Tier=F, Qtd=1). Fixed: Tampa de Madeira×1 (Escudo), Mochila×1, Tônico de
   Vida simples×1. Ciclos: 10.
4. **Caçador** — Choice slot "Arma à distância" (Tipo=Arma,
   Subcategorias=["Arcos","Fundas e Baladeiras"], Tier=F, Qtd=1,
   BonusSubcategoria="Arcos", BonusNome="Flecha de Madeira", BonusQtd=10).
   Fixed: Mochila×1, Corda×1, Ração de Viagem×2. Ciclos: 10.
5. **Arcano** — Choice slot "Condutor" (Tipo=Arma,
   Subcategorias=["Varinhas Mágicas","Cajados Mágicos"], Tier=F, Qtd=1).
   Fixed: Manuscrito Arcano Vol.1×1, Diário×1, Tinta×1, Pena×1, Tônico de
   Foco simples×1. Ciclos: 0.
6. **Ocultista** — Choice slot "Condutor" (same filter as Arcano). Fixed:
   Cera-viz (Material Ritualístico)×1, Manuscrito Arcano Vol.1×1, Diário×1,
   Tônico de Foco simples×1. Ciclos: 0.
7. **Devoto** — Fixed: Símbolo Sagrado×1, Diário×1, Papel×1, Tônico de Vida
   simples×1. Ciclos: 15.
8. **Artesão** — Fixed: Pé de Cabra×1, Gazúa×1, Tinta×1, Pena×1, Papel×1,
   Mochila×1. Ciclos: 15.
9. **Sobrevivente** — Fixed: Mochila×1, Saco de Dormir×1, Corda×1, Ração de
   Viagem×3, Vara de Madeira×1, Isca de Pesca×1, Tônico de Vida simples×1.
   Ciclos: 0.
10. **Investigador** — Fixed: Luneta×1, Diário×1, Tinta×1, Pena×1, Papel×1,
    Espelho×1. Ciclos: 15.
11. **Negociante** — Fixed: Mochila×1, Pena×1, Papel×1. Ciclos: 80.
12. **Aprendiz** — Fixed: Mochila×1, Tocha×1, Corda×1, Tônico de Vida
    simples×1, Tônico de Foco simples×1. Ciclos: 10.

Each kit's `Descricao` is the flavor paragraph already written in
`Equipagem.md` (e.g. Viajante: "Preparado para longas jornadas, o Viajante
aprendeu a carregar consigo aquilo que precisa para permanecer dias longe de
casa.").

## Apply flow

New controllers `CharacterEquipagemController`/`NpcEquipagemController`
(`api/character-sheets/{sheetId}/equipagem`,
`api/npc-sheets/{sheetId}/equipagem`), same
`CheckEditAuthorizationAsync`/`CanEdit` pattern as every other sheet
sub-controller. Both delegate the actual resolve/grant logic to a shared
`EquipmentKitGrantService` (new, `RuinaRPG.Infrastructure.Rules`) — unlike
most Character/Npc controller pairs (which duplicate simple CRUD on purpose,
matching this codebase's established style), this logic is non-trivial enough
(per-Tipo resolution, auto-creation, conditional bonus, campaign auto-attach)
that duplicating it verbatim would be a real maintenance risk; the service
takes the resolved `CampaignId`/`GmId`/sheet-kind-specific insert delegates.

**`GET .../equipagem/kits`** → `List<EquipmentKitOptionResponse>`:

```csharp
public record EquipmentKitOptionResponse(string Id, string Nome, string Descricao, int Ciclos,
    List<EquipmentKitChoiceSlotOptionResponse> ChoiceSlots);
public record EquipmentKitChoiceSlotOptionResponse(string SlotId, string Label,
    List<EquipmentKitEligibleItemResponse> Options);
public record EquipmentKitEligibleItemResponse(string ItemId, string Nome);
```

Every non-deleted `EquipmentKit` is returned (kits are global content, not
campaign-scoped — the campaign-visibility rule only applies to *items*, and
this feature's whole point is that applying a kit grants that visibility, so
gating the kit list itself on prior visibility would be circular). For each
`ChoiceSlot`, `Options` is resolved live against the calling sheet's campaign
GM's own `Items` (Tipo + Subcategoria-in-list-if-set + Tier-if-set filter).

**`POST .../equipagem/choose`**:

```csharp
public record ChooseEquipmentKitRequest(string KitId, List<ChoiceSlotSelectionRequest> ChoiceSelections);
public record ChoiceSlotSelectionRequest(string SlotId, string ItemId);
```

Server-side, in order:
1. Auth check, then `sheet.EquipmentKitId is not null` → 400 ("Equipagem
   inicial já escolhida.") — the one-time-only rule, enforced server-side
   even though the client hides the button after choosing.
2. Kit lookup (404 if missing/deleted).
3. For every `ChoiceSlot` on the kit: a `ChoiceSelections` entry must exist,
   and its `ItemId` must resolve to a real Item owned by the sheet's campaign
   GM that matches the slot's Tipo/Subcategoria/Tier filter — 400 otherwise.
   Never trust the client's filtering; this is a full server-side
   re-validation.
4. For every `EquipmentKitItem` (fixed row): resolve `Nome`+`Tipo` against
   the GM's catalog; auto-create if `Tipo=ItemGeral` and missing (using
   `SubcategoriaHint`, Peso=0, Preço=0); insert into the sheet's matching
   table per Tipo, reusing the exact same row-construction each existing
   Add* endpoint already uses (`Arma`→`CharacterWeapon`/`NpcWeapon`,
   `IsEquipped=false`, `DurabilidadeAtual=item.DurabilidadeMaxima ?? 0`, same
   as `CharacterArsenalController.AddWeapon`; `Escudo`→
   `CharacterShield`/`NpcShield`, same pattern as `AddShield`;
   `ItemGeral`→`CharacterInventoryItem`/`NpcInventoryItem` with `Qtd` from
   the kit row, same as `AddInventoryItem`; `Artefato`→
   `CharacterArtifact`/`NpcArtifact`, respecting the existing 3-per-TipoDeAlvo
   cap the same way `AddArtifact` already does, though no seeded kit
   currently grants one).
5. For every `ChoiceSlot`: insert the selected item the same way; if the
   slot has a `BonusSubcategoria` and the selected item's Subcategoria
   matches it, also insert `BonusNome`×`BonusQtd` (resolved/auto-created the
   same way as a fixed `ItemGeral` row).
6. `sheet.Ciclos = (sheet.Ciclos ?? 0) + kit.Ciclos`.
7. For every Item actually granted in steps 4-5: upsert a `CampaignAttachment`
   for `(sheet.CampaignId, ItemId)` with `IsPublic = true` (create if
   missing; if one exists with `IsPublic = false`, flip it to `true`) — this
   is the "itens de conhecimento geral" auto-attach the user asked for,
   scoped to exactly this action, not a general rule change to the normal
   Add-Weapon/Add-Inventory-Item endpoints.
8. `sheet.EquipmentKitId = kit.Id`.
9. One `SaveChangesAsync()` (EF Core's own per-call transaction covers
   atomicity — no explicit `BeginTransactionAsync` needed).

Response: `204 No Content` (client reloads the sheet + Combate/Posses
sections it already reloads after any other mutation).

## Client

- `FichaDePersonagem.razor`/`FichaDeNpc.razor`, Posses tab, immediately after
  `<MudTabPanel Text="Posses">` and before `<Section Title="Inventário">`:
  `@if (_form.EquipmentKitId is null) { <MudButton ...>Escolher Equipamento
  Inicial</MudButton> }`. `SheetFormModel` gains `EquipmentKitId` (both
  pages), loaded/saved the same way `HistoricoId` was (Task 5/12 pattern from
  the Histórico plan).
- New `EscolherEquipagemDialog.razor` (`Shared/`, embedded `MudDialog`
  pattern, sibling to `ClickableItemName`'s `MudDialog` usage): fetches
  `GET .../equipagem/kits` in `OnInitializedAsync`. `MudSelect` of kit Nomes
  → on change, shows `Descricao` + `Ciclos`; for each `ChoiceSlot`, a second
  `MudSelect` of `Options`. "Escolher Equipagem" `MudButton` disabled while
  any slot lacks a selection (`ChoiceSlots.All(s => Selections.ContainsKey(s.SlotId))`)
  or no kit chosen; "Cancelar" closes without calling the API. On confirm:
  `POST .../equipagem/choose`, close dialog, trigger the same "reload
  everything" callback the page already uses after other Posses/Combate
  mutations.
- `CharacterSheetResponse`/`UpdateCharacterSheetRequest`/`NpcSheetResponse`/
  `UpdateNpcSheetRequest` gain a trailing `string? EquipmentKitId` (same
  positional-record append discipline as `HistoricoId` — grep every
  `new CharacterSheetResponse(`/`new UpdateCharacterSheetRequest(`/etc. call
  site, including target-typed `new(...)` construction in test files, per the
  lesson recorded during the Histórico round).

## Auditoria + Livro de Regras

- `EquipmentKitsController` (`api/equipment-kits`), `RequireRulesAuditorAsync()`-gated
  writes, open GET — same shape as `HistoricosController`. Create/Update
  validate: `Nome`/`Descricao` non-blank, `Ciclos >= 0`, every
  `EquipmentKitItem.Tipo` is `ItemGeral`/`Arma`/`Escudo`/`Artefato` (never
  `Armadura` — reject with a clear message), every `Qtd >= 1`. Delete checks
  `db.CharacterSheets.AnyAsync(s => s.EquipmentKitId == id) ||
  db.NpcSheets.AnyAsync(...)` before soft-deleting (409 if in use) — same
  guard shape as `HistoricosController.Delete`.
- `AuditoriaEquipagem.razor` (new client page), mirrors
  `AuditoriaHistoricos.razor`: list/create/edit/delete kits; each kit's edit
  form sub-manages its fixed items (name + Tipo + Qtd + optional
  SubcategoriaHint, add/remove rows) and choice slots (Label + Tipo +
  Subcategorias free-text-list + Tier dropdown + Qtd + optional
  BonusSubcategoria/BonusNome/BonusQtd, add/remove rows) — same
  add-row-to-a-sub-table idiom `BancoDeMagiasForm`'s Efeitos sub-table
  already established.
- `RulesAuditorNavLinks.razor`: new `<MudNavLink Href="auditoria/equipagem">Auditoria: Equipagem</MudNavLink>`.
- `RulebookRenderer.BuildEquipagemAsync()`: intro paragraph from
  `Equipagem.md`'s lead text (`SplitIntoSections(..., splitLevel: 1)`,
  intro-only, discarding `.Sections` — same as `BuildHistoricosAsync`), cards
  built live from `db.EquipmentKits.Where(!IsDeleted).OrderBy(Nome)` (Nome,
  Descricao, Ciclos, fixed items, choice slots — using `PericiaLabels`-style
  canonical formatting is N/A here since nothing is Pericia-keyed, but reuse
  whatever item-Tipo display helper the client already has, not a fresh
  hand-rolled switch). New "Equipagem" `RulebookDocument`, **not** in
  `RulebookDocumentsController.ValidSlugs` (no Markdown-override support,
  same as Históricos).

## Docs to update (`Docs/Requisitos/`)

- `Requisitos - Ficha de Personagem.md` — new bullet/R00xx under section 5
  (Posses) documenting the button, its one-time nature, and where each item
  type lands.
- `Requisitos - Ficha de Criaturas.md` — one-line diff note: Criatura has no
  Equipagem button (Espólios is loot, not starting gear).
- `Requisitos - Livro de Regras.md` — new row in R0001's document table +
  new R00xx "the Equipagem tab reflects the catalog live" (mirrors R0007 for
  Históricos).
- `Requisitos - Auditoria de Regras.md` — new R00xx: Auditor CRUD over the
  Equipagem-kit catalog (fixed items + choice slots), in-use delete guard.
- `Requisitos - Catálogo de Itens e Equipamentos.md` — note that applying an
  Equipagem kit may auto-create a missing Item Geral in the acting GM's
  catalog with Peso/Preço 0, same convention as the "catálogo inicial"
  preamble already documents.
- `Requisitos - Campanha.md` — note the one narrow exception to R0008/R0009's
  manual-attach-and-publish flow: applying an Equipagem kit auto-attaches and
  publishes whatever items it grants, since Equipagem items are "de
  conhecimento geral."
- `Requisitos - Modelo de Dados.md` — new `EquipmentKit`/`EquipmentKitItem`/
  `EquipmentKitChoiceSlot` tables, new `EquipmentKitId` column on
  `CharacterSheets`/`NpcSheets`.

## Test strategy

- `EquipmentKitSeedDataTests` (unit): every kit's fixed-item Nomes resolve
  against `DefaultCatalogItems.Build` (a fake GmId) without needing
  auto-creation, except the 2 new Tônico entries (which the same build now
  includes) — a regression guard against a future rename in
  `DefaultCatalogItems.cs` silently breaking a kit reference.
- `EquipmentKitsControllerTests` (integration): CRUD, auditor-gating,
  Nome/Descricao/Ciclos/Tipo validation, in-use delete guard (character and
  NPC side, mirroring `HistoricosControllerTests`).
- `CharacterEquipagemControllerTests`/`NpcEquipagemControllerTests`
  (integration): kit list shows live-resolved choice options; choosing a kit
  with no choice slots places every fixed item correctly and adds Ciclos;
  choosing a kit with a choice slot rejects a selection outside the filter;
  the conditional-bonus slot (Caçador) grants the bonus only when the
  matching alternative is picked; second `POST .../choose` on an
  already-chosen sheet returns 400; applying a kit whose fixed ItemGeral is
  missing from the GM's catalog auto-creates it; applying a kit auto-attaches
  every granted item to the sheet's campaign as public (both create-new and
  flip-existing-private-to-public cases).
- Client `bUnit` tests for `EscolherEquipagemDialog` follow the same
  no-bUnit-for-MudDialog-portal-rendering precedent already established for
  `ClickableItemName`/`HistoricoSelect` (MudDialog's portal isn't reliably
  drivable in bUnit's synchronous harness) — covered by the integration
  tests above plus manual verification instead.
