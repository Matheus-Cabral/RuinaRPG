# Campaign-scoped item/spell/image pickers on player sheets

**Date:** 2026-09-04
**Status:** Approved for planning

## Problem

The character sheet (and granted NPC/Criatura sheets), when edited by the player who owns them, currently let the player pick an Item, a Magia/Habilidade, or an Image from their linked GM's **entire** catalog/bank/personal image library — regardless of whether the GM ever attached that content to the campaign, and regardless of the attachment's public/private toggle (`Requisitos - Campanha.md` R0008/R0009). Concretely:

- `ItemsController.List` (`GET items?nome=&tipo=`) filters only by `GmId`.
- `SpellAbilityBankController.List` (`GET spell-ability-bank?nome=`) filters only by `GmId`.
- `ImagesController.Mine` (`GET images/mine`) filters only by `UploadedByUserId`.

None of these consult `CampaignAttachments`/`IsPublic`. This is a real gap between what `Requisitos - Campanha.md` promises (a player only sees public attachments) and what the sheet editor actually does.

Separately: a GM currently reaches `/campanhas/{id}` (the GM management page, including the Anexos tab) with no page-level guard — a player who types that URL directly gets a page that silently fails its GM-only API calls, instead of being redirected to their own view (`/campanhas/{id}/jogador`, `MinhaCampanha.razor`).

## Goals

1. When a **Jogador** edits their own Ficha de Personagem, or a NPC/Criatura sheet granted to them, the Item/Magia-Habilidade/Imagem pickers only offer content **publicly attached to that sheet's campaign**.
2. A GM editing any sheet (their own NPC/Criatura roster, or a player's sheet) keeps full, unrestricted catalog/bank/image access — the restriction is keyed off **caller role**, not sheet ownership.
3. A Magia/Habilidade a player creates "do zero" on their sheet, and an Image a player uploads from their sheet, are **automatically** attached to that sheet's campaign as **public** — no GM approval step for player-originated content. (Items are never player-created, so no auto-attach path is needed for them.)
4. A player can never reach the GM's campaign-management page (`/campanhas/{id}`) — they're redirected to their own view.

## Non-goals

- No change to the GM-facing endpoints/pages (`GET items`, `GET spell-ability-bank`, `GET images/mine`, `CampanhaDetalhe.razor`'s Anexos tab) beyond the redirect guard.
- No retroactive effect on content a sheet already references — this only changes what the *picker* offers going forward. Existing sheet data (already-picked items/spells/images) is untouched.
- No change to how the GM manually attaches/toggles-public GM-curated catalog/bank content — that flow (`CampaignAttachmentsController`) is unchanged.

## Data model

**No schema change, no EF migration.** `CampaignAttachment` already carries everything needed (`CampaignId`, one of `ItemId`/`SpellAbilityBankEntryId`/`ImageId`, `IsPublic`). `NpcSheet`/`CreatureSheet` still have no direct `CampaignId` column — it's resolved through the grant-link `CampaignAttachment` row (`Where(a => a.NpcSheetId == id).Select(a => a.CampaignId).FirstOrDefault()`), same as today's `IsGrantLinkAsync` pattern.

## Backend changes

### 1. New `CampaignCatalogController` (`src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs`)

`[Authorize]`, `Route("api/campaigns/{campaignId}/...")`, membership-checked the same way as `CampaignPlayerViewController.Get` (`campaign.GmId == callerId || CampaignMembers.Any(...)`, 404 if campaign missing, 403 if not a member). Reuses the **existing response contracts** — no new DTOs:

- `GET available-items?nome=&tipo=` → `List<ItemResponse>`, same query shape as `ItemsController.List`, but the item must have a `CampaignAttachment` in this campaign with `IsPublic == true`. `tipo` still does the sub-type filter (Arma/Armadura/Escudo/ItemGeral/Artefato) via `ItemResponse.Tipo`, so no contract changes needed there.
- `GET available-spell-abilities?nome=` → `List<SpellAbilityEntryResponse>`, same shape as `SpellAbilityBankController.List`, scoped the same way via `SpellAbilityBankEntryId`.
- `GET available-images` → `List<ImageSummaryResponse>`, same shape as `ImagesController.Mine`, scoped via `ImageId`.

### 2. `CampaignPlayerViewController` — bugfix while in the area

Lines 77 and 87 build the Item/Image thumbnail URLs in `AnexosPublicos` as `$"/{path}"` instead of `$"/images/{path}"` (every other place in the codebase uses the `images/` prefix). Fix both to match.

### 3. `ImagesController.Upload` — optional auto-attach

Add an optional form field `string? campaignId` to `Upload`. When present, parses to a `Guid`, and if the caller is in role **Jogador** and is a member of that campaign (same membership check as above), creates a `CampaignAttachment { CampaignId, ImageId = image.Id, IsPublic = true }` in the same `SaveChangesAsync` call. Malformed/non-member `campaignId` is ignored (upload still succeeds; no attachment is created) rather than failing the upload — the field is best-effort context, not a hard requirement of the upload itself.

### 4. `CharacterSpellAbilitiesController.Add`, `NpcSpellAbilitiesController.Add`, `CreatureSpellAbilitiesController.Add` — auto-attach

After the existing R0001 bank-copy is added (`db.SpellAbilityBankEntries.Add(bankCopy)`), if the caller is **not** the campaign's/sheet's GM (i.e. `callerId != campaignGmId` / `callerId != sheet.GmId` — the authorization check already established the caller is either the GM or the owning Jogador, so this is just "which one"), also add `CampaignAttachment { CampaignId, SpellAbilityBankEntryId = bankCopy.Id, IsPublic = true }`. For `NpcSpellAbilitiesController`/`CreatureSpellAbilitiesController`, resolve `CampaignId` via the grant-link lookup described above. Note this fires on **every** Add call from a player, including `fromBank`-sourced ones (a fresh independent bank copy is made either way per existing R0001 behavior) — each becomes its own public attachment. That's consistent with the existing "independent copy" philosophy and is called out here rather than silently accepted.

### 5. `NpcSheetResponse` / `CreatureSheetResponse` — expose `CampaignId`

Add `string? CampaignId` (nullable — an un-granted sheet has none) as a new trailing field on both records, computed the same grant-link-lookup way, in `NpcSheetsController`/`CreatureSheetsController`'s `ToResponseAsync`. This mirrors how `FichaDePersonagem.razor` already gets `_campaignId` straight from `CharacterSheetResponse.CampaignId` — the granted-sheet pages need the same thing and currently have no way to get it.

## Client changes

### 1. `ImageAttachmentField.razor` — optional `CampaignId`

New `[Parameter] public string? CampaignId { get; set; }`. When set, `HandleUploadAsync` includes it as a form field in the multipart upload. No other behavior change — the component still just shows whatever `AvailableImages` it's given (already correct for the "scoped list" case, see below).

### 2. `FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor` — role-conditional sourcing

Each page already loads (or will load, for Npc/Criatura) its sheet's `CampaignId`. Add a cheap role check in `OnInitializedAsync` (inject `TokenAuthenticationStateProvider`, `(await AuthProvider.GetAuthenticationStateAsync()).User.IsInRole("GM")` → `_isGmCaller`), then:

- Item search delegates (`SearchItemsByTipoAsync` and friends) hit `items?...` when `_isGmCaller`, else `campaigns/{_campaignId}/available-items?...`.
- The spell/ability bank list (`_bankEntries`) loads from `spell-ability-bank` when `_isGmCaller`, else `campaigns/{_campaignId}/available-spell-abilities`.
- `_myImages` loads from `images/mine` when `_isGmCaller`, else `campaigns/{_campaignId}/available-images`.
- Every `ImageAttachmentField` on these pages (avatar picker, Diário picker) gets `CampaignId="@(_isGmCaller ? null : _campaignId)"`.

`FichaDeNpc.razor`/`FichaDeCriatura.razor` also gain `_campaignId`, sourced from the new `NpcSheetResponse.CampaignId`/`CreatureSheetResponse.CampaignId`.

### 3. `CampanhaDetalhe.razor` — route guard

In `OnInitializedAsync`, before loading any GM data: check the caller's role the same way; if not `"GM"`, `Navigation.NavigateTo($"campanhas/{CampaignId}/jogador", replace: true)` and return early.

## Requirements doc updates

Per CLAUDE.md, code must trace to a requirement — these docs currently don't state this scoping (only `Requisitos - Catálogo de Itens e Equipamentos.md` R0010 touches it, and only for the "reuse an existing image" sub-feature). Add, continuing each file's existing numbering:

- **`Requisitos - Ficha de Personagem.md` R0003** (new): the Item/Magia-Habilidade/Imagem fields in 1.a, 3.a–3.c, 4.b, 5.a–5.b and 6 are scoped to the sheet's campaign's public attachments (`Requisitos - Campanha` R0008/R0009) when edited by the owning Jogador; a GM editing any sheet keeps full catalog/bank/image access.
- **`Requisitos - Campanha.md` R0012** (new): a Magia/Habilidade a player creates on a sheet, and an Image a player uploads from a sheet, are automatically attached to that sheet's campaign as public (no GM approval step). The GM's campaign-management screen (Membros/Anexos/Diário/etc.) is never reachable by a player.
- **`Requisitos - Banco de Magias e Habilidades.md` R0007** (new, cross-referencing R0001): when the creator is a Jogador (not the GM), the auto-copy into the bank is also auto-attached to the campaign as public, per `Requisitos - Campanha` R0012.
- **`Requisitos - Catálogo de Itens e Equipamentos.md` R0011** (new, cross-referencing R0010): generalizes R0010's player-scoping language — every item picker on a sheet, not just the "reuse an image" feature, is limited to the campaign's public attachments.

## Testing plan

- Integration tests for the new `CampaignCatalogController` endpoints: membership enforcement (404/403), public-only filtering (a private attachment is excluded), sub-type filtering for items.
- Integration tests for `ImagesController.Upload`'s auto-attach: Jogador + valid campaignId → attachment created public; GM caller → no attachment (GM uploads stay private-by-default via the existing GM flows); malformed/non-member campaignId → upload still succeeds, no attachment.
- Integration tests for each `*SpellAbilitiesController.Add`: Jogador caller → public attachment created (both `fromScratch` and `fromBank` cases); GM caller → no attachment.
- Integration tests for `NpcSheetResponse`/`CreatureSheetResponse.CampaignId`: populated for a granted sheet, null for an un-granted one.
- bUnit test for `ImageAttachmentField`'s `CampaignId` passthrough on upload.
- No new client component tests beyond that — the role-conditional URL switching in the three sheet pages is simple enough to cover at the integration level (does `available-items`/etc. return the right set) rather than re-testing Blazor wiring already covered by existing `EntityPicker`/`ImageGalleryDialog` tests.

## Open risk called out, not resolved here

Repeated "from bank" reuse by players multiplies public attachments (independent copies) with the same name in `AnexosPublicos` — this already matches the pre-existing R0001 "independent copy, no dedup" philosophy for the bank itself, so it's accepted as consistent rather than treated as a bug to fix in this change.
