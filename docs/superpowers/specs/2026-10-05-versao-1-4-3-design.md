# Versão 1.4.3 — Design

**Status:** design approved by user 2026-10-05 (in chat). Version 1.4.3, branch `release/1.4.3`.

## Overview

Six independent changes shipped together as 1.4.3:

1. **Disciplina da Runa** — a new required select on every Runa.
2. **Requisitos e Penalidade de equipamentos** — structured requirements and an automatic penalty on
   Arma, Armadura, Escudo and Artefato, replacing three legacy fields.
3. **Base de itens fixos** — a global catalog of complete items behind the fixed items of the Equipagem
   kits, with search-and-pick instead of a typed name.
4. **Passivas no Livro de Regras** — filters by Categoria, Vocação and Classe, alphabetical groups, collapsible entries.
5. **Novidades para Auditores** — an auditor-only section in the version changelog dialog.
6. **Progressão do nível recolhível** — the section's content moves into an expansion panel.

Implementation order: **1, 2, 3, 4, 6, 5**. Item 3 depends on item 2 (a fixed item carries the item-2
fields), and item 5 is written last because its text describes what the others changed.

Schema migrations exist in items 1, 2 and 3, so `make migrate` is a mandatory step of the 1.4.3 deploy.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Disciplina options | **Adição** — Influencia o corpo do usuário; **Alteração** — Influencia objetos inanimados; **Emissão** — Influencia alvos inanimados; **Manifestação** — Manifesta a aura do usuário. Texts as given by the user. |
| Disciplina required? | Yes, on create and on saving an edit. Runas that already exist stay without one until edited. |
| Where the descriptions go | An information popup next to the select. |
| What "equipamentos" means | Arma, Armadura, Escudo and Artefato. Item Geral is out. |
| Requirement fields | Vocação, Classe, Atributo, Perícia, Sub-Atributo, Afinidade, Estrela. |
| Unmet requirements | **Warn, never block.** The equipment can be added and equipped; the sheet shows what is missing. |
| Penalidade | A list of numeric lines over Atributo / Perícia / Sub-Atributo plus a free-text field. Vocação, Classe, Afinidade and Estrela are not penalty targets. |
| Is the penalty applied? | **Automatically**, while the requirements are unmet. The free text is display only. |
| Legacy fields (`RequisitoAtributo`, `RequisitoVigor`, `Penalidade`) | **Migrated and removed.** |
| What the fixed-item base stores | The **complete item**. Applying a kit copies it into the GM's own catalog; the copy then belongs to the GM. |
| Passivas organization | **Amended 2026-10-05:** always the three groups Passivas Livres, Passivas Vocacionais, Passivas de Classe, **alphabetical** inside each, no sub-groups. Filters by name, Categoria, Vocação, Classe; count per group; collapsible entries. |
| Editing an existing fixed-item row of a kit | **Added 2026-10-05:** the row has an edit action opening a popup to complete the item's missing details or to pick another item that replaces it. |
| "Atualizações de auditoria para auditores" | Auditor-only **release notes** in the changelog dialog. Not an edit history. |
| Progressão do nível | Expansion panel, **collapsed by default**, on Personagem and NPC. |

## 1. Disciplina da Runa

### Domain and schema

- New enum `RuinaRPG.Domain.Runes.DisciplinaDeRuna { Adicao, Alteracao, Emissao, Manifestacao }`, next to
  `TipoDeRuna`. A static `DisciplinaDeRunaInfo` in the same folder holds the display label and the
  description of each value, so the popup, the lists and the API share one source.
- New nullable column `Disciplina` (enum stored as text, same convention as `Tipo`) on `RuneBankEntries`,
  `CharacterRunes` and `NpcRunes`. One migration. Existing rows stay `NULL`.

### API

- `CreateRuneBankEntryRequest`, the bank update request, `AddCharacterRuneRequest` and the NPC equivalent
  gain `Disciplina` (string, the enum name). The responses gain it too.
- **Required on:** bank create, bank update, and adding a Runa **from scratch** to a Personagem/NPC sheet.
  A missing or unknown value answers `400` with a message naming the field.
- **Adding from a bank entry** copies the entry's Disciplina, as it copies Tipo. A legacy entry with no
  Disciplina can still be picked and yields a sheet Runa with none; it is not a validation error, because
  the player cannot fix a GM's bank entry.
- The automatic bank copy (Banco de Runas R0001), the campaign attachment listing and the NPC
  grant/copy flow carry Disciplina wherever they already carry Tipo.

### Client

- `BancoDeRunasForm` and `RuneOrigemFields` (from-scratch mode): a `MudSelect` **Disciplina** with the four
  options and no blank option, marked required, followed by an `InfoPopup` titled "Disciplina da Runa"
  listing the four `name — description` lines.
- Runa lists (bank, Personagem 4.d, NPC 4.d, runas released to a player in the campaign): a **Disciplina**
  column showing the label or "—".
- `BancoDeRunas` list: a **Disciplina** filter (Todas + the four), combinable with the existing Nome, Grau
  and Tipo filters.
- Editing a legacy bank entry: the select starts empty and the autosave does not fire until a Disciplina is
  chosen; the form shows the validation message instead.

### Docs

Banco de Runas (new requirement, plus R0004 filters and R0005 fields), Ficha de Personagem 4.d, Ficha de
NPCs R0008 if it enumerates fields, Modelo de Dados (three tables).

## 2. Requisitos e Penalidade de equipamentos

### Data

Both structures are stored as `jsonb` on the `Items` table (TPH), mapped on `Arma`, `Armadura`, `Escudo`
and `Artefato`; `ItemGeral` does not map them.

- **`Requisitos`** — the existing `RequisitosDePassiva` record, reused as is (same converter and comparer).
  Only Vocação, Classe, Afinidade, Estrela, Atributos, SubAtributos and Pericias are accepted for an
  equipment: the API nulls out Nivel, Linhagem, Variante, Graduacao, CoracaoDeMana and HistoricoId on save.
- **`Penalidade`** — new record `RuinaRPG.Domain.Items.PenalidadeDeEquipamento`:
  - `Atributos: List<(Atributo, int Valor)>`
  - `SubAtributos: List<(SubAtributo, int Valor)>`
  - `Pericias: List<(int PericiaId, int Valor)>` — keyed by the `Pericias` row Id, like the requirement
  - `Texto: string?`
  - `Valor` is a positive magnitude (≥ 1) that is **subtracted**. A target appears at most once per list.
- Both are `NULL` when the equipment has no requirements. There is no stored "Possui requisitos" flag: the
  checkbox is checked when either is non-null.

### Catalog form

At the end of the form of the four equipment types, a checkbox **"Possui requisitos"**. Checked, it shows:

- **Requisitos** — `RequisitosDePassivaEditor` gains a parameter selecting which fields to render; the
  equipment form renders only the seven accepted ones. The Passiva form keeps rendering all of them.
- **Penalidade** — a new `PenalidadeDeEquipamentoEditor`: three add/remove tables (Atributo, Sub-Atributo,
  Perícia) with a target select and a numeric "Penalidade" ≥ 1, in the same layout as the requirement
  tables, plus a multi-line **"Outras penalidades"** text field.

Unchecking the box clears both structures on the next save. The catalog list and the campaign catalog show
the requirements and the penalty spelled out (`PassivaRequisitosEvaluator.Descrever` plus a new
`PenalidadeDeEquipamento` describer with the same labels).

### On the sheets (Personagem, NPC, Criatura)

- Every Arma, Armadura slot, Escudo and Artefato line whose item has requirements is evaluated with
  `PassivaRequisitosEvaluator.Pendencias` against the sheet. The response of each line gains
  `RequisitosPendentes: List<string>` and `Penalidade` (numeric lines spelled out + the free text).
- A line with pending requirements shows a warning icon and "Requisitos não cumpridos", with the missing
  requirements and the penalty. A line whose requirements are all met shows them as plain information.
- Nothing is blocked: adding, equipping and slotting keep working exactly as today.
- On a Criatura, Vocação, Classe and Estrela requirements are ignored (`TemIdentidadeDePersonagem = false`),
  as for Passivas.

### Penalty calculation

- **Active penalty** = the penalty of an item with at least one pending requirement that is *in use*: an
  Arma or Escudo with `IsEquipped`, an Armadura in a slot, an Artefato on the sheet's list.
- Active penalties enter the sheet's calculations as **negative modifier inputs** on the same path the
  Artefato bonuses use (`ArtifactBonusInput` with `TipoDeAlvo` Atributo / Perícia / SubAtributo and a negative
  `Valor`). This reuses every formula that already has an Artefatos term: attribute totals, skill totals,
  the six Sub-Atributos, and whatever derives from them. Penalties of several items add up.
- **No cascade:** requirements are evaluated against the sheet **without** penalties. Each `*SheetStats`
  service builds the `FichaParaRequisitos` snapshot from Artefato inputs only, decides which penalties are
  active, and only then appends them for the displayed totals. A penalty therefore never makes another
  equipment (or a Passiva) fail its requirements. Passiva requirements keep using that same penalty-free
  snapshot, so their behavior does not change.
- An unmet Artefato keeps granting its own bonus; only its penalty is added.
- The Atributos & Perícias and Combate tabs show an alert **"Penalidades de equipamento ativas"** listing
  item → penalty, so the reduced totals are explained. Where a breakdown shows an "Artefatos" term, the
  penalty is reported in its own "Penalidade" term rather than folded into it.

### Legacy fields

One migration adds the two columns, converts the data and drops the old columns:

| Legacy field | Becomes |
|---|---|
| `Armadura.RequisitoVigor`, `Escudo.RequisitoVigor` (int) | Requirement `Atributos: [Vigor ≥ N]` |
| `Armadura.Penalidade`, `Escudo.Penalidade` (text) | `Penalidade.Texto` |
| `Arma.RequisitoAtributo` (text, e.g. `10 Dex`) | Parsed when it matches `<number> <attribute abbreviation or name>` (either order) into an Atributo requirement; otherwise copied into `Penalidade.Texto` prefixed with "Requisito: " for the GM to review |

The parser lives in the Domain (`RequisitoAtributoLegado`), unit-tested, and the migration applies it
through a data step that reads the rows and writes the jsonb. `RequisitoVigor`, `RequisitoAtributo` and the
text `Penalidade` leave the contracts, the catalog form and the sheet tables; the sheets show the new
requirement/penalty display in their place. `DefaultCatalogItems` is updated to the new shape.

`CharacterSheetStats` currently hardcodes `penalidadeArmadura = 0` with a comment about the free-text
field; that comment is updated, since armor penalties now arrive through the structured path.

### Docs

Catálogo de Itens (R0004–R0006, R0009, a new requirement for Requisitos/Penalidade, preamble example),
Ficha de Personagem 3.a–3.c and 5.b, Ficha de NPCs and Ficha de Criaturas where they diff those sections,
Modelo de Dados (`Items`).

## 3. Base de itens fixos dos kits

### Data

- New global table **`EquipmentKitFixedItems`**: `Id`, `Nome`, `Tipo` (`ItemTipo`), `Dados` (`jsonb`).
  `Dados` is the item's full field set in the shape of the catalog's create request (every per-type field,
  including the item-2 Requisitos and Penalidade), minus the image — images belong to a user. Unique on
  (`Nome`, `Tipo`).
- `EquipmentKitItems` gains `FixedItemId` (FK, `Restrict`) and `ArmorSlot` (nullable, required when the
  fixed item is an Armadura). `EquipmentKitChoiceSlots` gains `BonusFixedItemId` (FK, `Restrict`).
- **Backfill** (`EquipmentKitFixedItemBackfill`, run with the other seeders: at startup in Development, in
  the `--migrate` branch in Production): for each distinct (`Nome`, `Tipo`) used by a kit item or a slot
  bonus, create a fixed item — with full data from `DefaultCatalogItems` when a default item of that name
  and type exists, otherwise with only the name (and `SubcategoriaHint` as Subcategoria) — and link the
  rows. It is idempotent. `EquipmentKitSeeder` creates fixed items the same way for kits it seeds.
- The legacy columns `EquipmentKitItems.Nome/Tipo/SubcategoriaHint` and
  `EquipmentKitChoiceSlots.BonusNome` become nullable and unused after the backfill. They are dropped in a
  later release, once every deployed database has run it.

### Applying a kit

`EquipmentKitGrantService.ResolveOrCreateFixedItemAsync` resolves through the fixed item:

1. If the GM's catalog has an item with the same `Nome` and `Tipo`, use it.
2. Otherwise create it in the GM's catalog from `Dados`, through the same mapping the catalog's create
   endpoint uses (extracted from `ItemsController` into a shared factory so both paths validate and build
   an item identically).

No kit fails for a missing Arma, Escudo or Artefato any more, and an Item Geral is no longer created with
zeroed fields. The created item is an ordinary catalog item: the GM may edit it, and later edits to the
fixed item do not touch copies already made. Renaming a fixed item means a GM who already had the old name
gets a new copy on the next application. An Armadura fixed item is placed in its `ArmorSlot`, following the
rule choice slots already use for an occupied slot.

### Auditoria de Equipagem

- **Add fixed item to a kit:** a Tipo select (Item Geral, Arma, Armadura, Escudo, Artefato), then an
  autocomplete searching the base by name within that Tipo, Quantidade, and — for Armadura — the Slot.
  When the search has no match, the list offers **"Cadastrar novo item"**, which opens a dialog with the
  full form of the selected Tipo, the name pre-filled; saving adds the item to the base and selects it.
- **Quantidade** (and Slot) are editable on an existing row.
- **Edit action on an existing row:** a button on every fixed-item row opens a popup with two choices:
  - **"Editar detalhes"** — the full form of the row's fixed item, to fill in what is missing (the
    backfill leaves name-only items for anything absent from the default catalog). It saves to the base,
    so the change applies to **every kit that uses that fixed item**; the popup says so and lists those kits.
  - **"Substituir item"** — the same Tipo select + name autocomplete used to add a row (including
    "Cadastrar novo item"); picking an item points this row at it, keeping the Quantidade. The Tipo may
    change; switching to Armadura asks for the Slot, switching away clears it. The item that was replaced
    stays in the base.
  Rows whose fixed item still has only a name are flagged "Detalhes incompletos" in the table, so the
  Auditor can see which ones need the popup.
- **Choice-slot bonus:** the bonus item is picked from the base with the same autocomplete (Item Geral
  only, as today).
- **New section "Itens fixos":** lists the base with a Tipo filter and name search, with edit (same dialog)
  and delete. Deleting an item referenced by a kit item or a slot bonus is refused with `409` naming the kits.

The item field block of `CatalogoItemForm` is extracted into a shared component used by the catalog page
and by this dialog, so the two forms cannot drift.

### API

`equipment-kit-fixed-items`, Auditor-only: `GET ?tipo=&q=`, `POST`, `PUT {id}`, `DELETE {id}`. The kit
item and choice-slot requests take `FixedItemId` / `BonusFixedItemId` instead of names; the kit item gains
an update endpoint (`FixedItemId`, `Qtd`, `ArmorSlot`), which today does not exist — rows can only be
added and deleted. The kit responses return the fixed item's Id, Nome, Tipo and whether its details are
incomplete. The player-facing kit listing (`EscolherEquipagemDialog`, Livro
de Regras rendering) keeps showing names and quantities.

### Docs

Auditoria de Regras R0009/R0010 and a new requirement for the base, Catálogo de Itens R0012 (rewritten:
any type is now created automatically), Modelo de Dados (new table, changed columns).

## 4. Passivas no Livro de Regras

- `PassivaDoLivroResponse` gains `Vocacao` (display label or null) and `Classe` (or null), taken from the
  Passiva's Requisitos. Filtering and grouping stay client-side, on the list already loaded.
- **Filters**, side by side above the list and all combinable:
  - name search (as today);
  - **Categoria**: Todas, Passiva Livre, Passiva Vocacional, Passiva de Classe;
  - **Vocação** and **Classe**: "Todas" plus the distinct values present in the loaded list. Choosing a
    value shows only the Passivas that require exactly it.
- **Grouping:** always the three groups, in this order and with these headings: **Passivas Livres**,
  **Passivas Vocacionais**, **Passivas de Classe**. There are no sub-groups. The filters narrow what is
  inside the groups; they never change the grouping. A group with no Passiva after filtering disappears.
- **Order** inside a group: alphabetical by name, ignoring case and accents (`pt-BR` comparison).
- **Count:** each heading shows the number of Passivas under it after filtering.
- **Entries:** each Passiva is a `MudExpansionPanel` (multi-expansion, all collapsed initially). The header
  shows the name and the requirements summary; the body shows the description and the full requirements.
- The empty state distinguishes "no Passiva available" from "no Passiva matches the filters".

Docs: Livro de Regras R0010.

## 5. Novidades para Auditores

- `ChangelogDialog` receives `IsRulesAuditor` (already in `MeResponse`). When true, a second block
  **"Para Auditores"** follows the general list, describing what changed on the Auditoria pages in this
  version. Non-auditors see the dialog exactly as before.
- The dismissal mechanism is unchanged (one pending version per user).
- 1.4.3 auditor text: the new fixed-item base on Auditoria de Equipagem (search-and-pick, "Cadastrar novo
  item", Armadura as fixed item, editable Quantidade), and that equipment requirements/penalties can be set
  on fixed items.
- The release process note for future versions: fill the auditor block whenever a version touches an
  Auditoria page; leave it out when it does not.

Docs: the requirement that describes the version changelog dialog, and Auditoria de Regras.

## 6. Progressão do nível recolhível

`ProgressaoDoNivelSection` keeps its `Section` title and `InfoPopup`, and wraps the table in a
`MudExpansionPanel` that starts collapsed. Used by Personagem and NPC, so both change. The expanded state
is not persisted.

Docs: Ficha de Personagem (the "Painel Progressão do nível" note).

## Release

- `AppVersionInfo.Current = "1.4.3"` and the `ChangelogDialog` general text covering items 1–4 and 6.
- Deploy: `make deploy` then `make migrate` (schema migrations of items 1–3 and the fixed-item backfill).

## Testing

TDD throughout (Técnico R0011).

- **Unit:** `DisciplinaDeRunaInfo` labels; the legacy `RequisitoAtributo` parser (matching and
  non-matching inputs); penalty activation and the no-cascade rule (requirements evaluated without
  penalties); the penalty describer; Passivas grouping/alphabetical ordering/filtering extracted into a
  testable class.
- **Integration:** Disciplina required on create/update/from-scratch and copied from a bank entry; legacy
  Runa without Disciplina; equipment requirements and penalties round-tripping through the catalog; a sheet
  whose attribute, skill and sub-attribute totals drop while a requirement is unmet and recover when it is
  met, for Personagem, NPC and Criatura, and for each of the four equipment types; an unequipped Arma not
  penalizing; the data migration of the three legacy fields; fixed-item CRUD and the `409` on delete; updating a kit row (replace item, change Quantidade, Armadura
  slot rules); kit
  application creating a complete item in an empty GM catalog and reusing an existing one; the backfill on
  a database seeded before 1.4.3; `rulebook/passivas` returning the two new fields.
- Build stays at 0 warnings.

## Out of scope

- An edit history of the Auditoria pages.
- Blocking equipment on unmet requirements.
- Penalties on Dano, resource maximums, or anything other than Atributo, Perícia and Sub-Atributo.
- Requirements or penalties on Item Geral.
- Syncing a GM's catalog copy when the Auditor later edits the fixed item.
