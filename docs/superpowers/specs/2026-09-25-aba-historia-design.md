# Aba "História" (Ficha de Personagem + Ficha de NPC) — Design

**Status:** approved by user 2026-09-25 (three design sections approved in chat), ready for planning.

## Overview

Both the Ficha de Personagem and the Ficha de NPC get a new tab, **História**, that gathers the
character's narrative identity in one place:

1. **Estrela** — the existing `EstrelaSelect` (dropdown + ⓘ popup reading the Livro de Regras),
   moved out of "Informações Básicas".
2. **Histórico** — the existing `HistoricoSelect` (dropdown + ⓘ), moved out of "Informações Básicas".
3. **História do personagem** — a new free-form, rich-text field the player writes the character's
   backstory in, edited with a full-featured WYSIWYG editor (**Jodit**, MIT).

Sina stays where it is (Combate tab). No images inside the História (explicitly out of scope — text
formatting only).

## UI

### Tab placement

- Ficha de Personagem: Informações Básicas → Atributos & Perícias → Combate → Magias & Habilidades →
  Posses → **História** → Diário.
- Ficha de NPC (no Diário): Informações Básicas → Atributos & Perícias → Combate → Magias &
  Habilidades → Posses → **História**.

### Tab contents (top to bottom)

`EstrelaSelect`, `HistoricoSelect` (same bindings/`@bind-Value:after="NotifySavedAsync"` as today —
they still go through the sheet's general autosave), then the `RichTextEditor` spanning the tab's
width, ~500px initial height, growing with content.

### Editor toolbar (Jodit, pt-BR)

undo/redo · paragraph/heading format · font family · font size · bold · italic · underline ·
strikethrough · superscript · subscript · text color / background color · bulleted list · numbered
list · outdent/indent · alignment · blockquote · link · table (insert, add/remove rows/cols, merge
cells — Jodit's built-in table plugin) · horizontal rule · find & replace · clear formatting ·
fullscreen.

Explicitly **disabled**: image, video, file upload, source/HTML view, print, "about".

Theme follows the app: Jodit's `theme: 'dark'` whenever `document.documentElement` has
`data-theme="dark"` (set by `wwwroot/js/theme.js`); a `MutationObserver` on that attribute keeps an
open editor in sync when the user toggles the theme.

Pasting from Word/Google Docs uses Jodit's paste cleaning (`askBeforePasteHTML: false`,
`defaultActionOnPaste: 'insert_clear_html'`), so the stored HTML stays within the allowlist below
even before the server sanitizes it.

### Saving

Autosave, no Save button: the editor reports a change ~1s after typing stops (debounced in JS) and
immediately on blur. Each sheet wires those to its **own** `AutoSaveCoordinator` instance dedicated
to the História, which calls `PUT …/historia`; the sheet's existing autosave indicator shows the
História's save state as well (the page combines both coordinators' states — Saving/Error win over
Saved).

A 400 from the API (too long) surfaces through the sheet's existing `_errorMessage` alert.

## Data model

New nullable column on both sheet tables (EF Core, one migration `AddSheetHistoria`):

| Table | Column | Type | Notes |
|---|---|---|---|
| `CharacterSheets` | `Historia` | `text`, NULL | sanitized HTML; NULL = empty editor |
| `NpcSheets` | `Historia` | `text`, NULL | same |

`Docs/Requisitos/Requisitos - Modelo de Dados.md` gets both columns.

## API

- **Read:** `Historia` (string?, already-sanitized HTML) is added to `CharacterSheetResponse` and
  `NpcSheetResponse`, so it arrives with the sheet's normal GET.
- **Write:** dedicated endpoints, **not** part of the general sheet update request:
  - `PUT api/character-sheets/{id}/historia` — body `UpdateHistoriaRequest(string? Historia)`.
  - `PUT api/npc-sheets/{id}/historia` — same body.

  Keeping it out of `UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest` means the general
  autosave (which fires on any other field) can never overwrite the História with a stale copy, and
  vice versa.
- **Authorization:** exactly what each sheet's general `PUT` already does —
  Personagem: 404 if the sheet doesn't exist; 403 (`Forbid`) unless
  `CharacterSheetAuthorization.CanEdit(caller, sheet.OwnerId, campaign GM)` (owner or the campaign's
  GM). NPC: 404 if the sheet doesn't exist or `GrantedSheetAuthorization.CanEdit` fails (GM, or the
  player the NPC was granted to) — same as `NpcSheetsController.Update`.
- **Responses:** 204 on success; 400 when the raw input exceeds **200,000 characters**
  (`"A História pode ter no máximo 200.000 caracteres."`). Empty/whitespace-only input (after
  sanitizing) is stored as NULL.

### Sanitization (server-side, before persisting)

A `HistoriaSanitizer` in `RuinaRPG.Infrastructure` (wrapping the `HtmlSanitizer` NuGet package,
MIT) with an explicit allowlist:

- **Tags:** `p`, `div`, `br`, `hr`, `h1`–`h6`, `strong`, `b`, `em`, `i`, `u`, `s`, `strike`, `sub`,
  `sup`, `span`, `blockquote`, `ul`, `ol`, `li`, `a`, `table`, `thead`, `tbody`, `tr`, `th`, `td`.
- **Attributes:** `style` (any allowed tag), `href` (`a`), `colspan`/`rowspan` (`th`/`td`).
  `target`/`rel` are not accepted from input — the sanitizer sets them itself on every `a`.
- **CSS properties (inside `style`):** `color`, `background-color`, `font-family`, `font-size`,
  `text-align`, `text-decoration`, `padding-left`, `margin-left` (Jodit indents with either).
- **URL schemes:** `http`, `https`, `mailto`. Every surviving `a` gets
  `target="_blank" rel="noopener noreferrer"`.
- Everything else is dropped: `<script>`, `<style>`, `<iframe>`, `<img>`, `<object>`, event-handler
  attributes (`on*`), `javascript:`/`data:` URLs, `class`/`id`.

The client renders only HTML that came back from the API, so a direct API call bypassing the editor
can't inject script either.

### NPC deep copy

`CampaignGrantsController.DeepCopyNpcAsync` (grant a copy of an existing NPC to a player) copies
`Historia` along with the other scalar fields.

## Client

- **Vendored Jodit:** `src/RuinaRPG.Client/wwwroot/lib/jodit/` holds `jodit.min.js` and
  `jodit.min.css` from the npm package `jodit@4.15.14` (`es2021/` build, which bundles the pt_br
  locale), plus its `LICENSE.txt`. Not referenced from `index.html` — loaded lazily.
- **`wwwroot/js/richTextEditor.js`** — `window.ruinaRichText`:
  - `create(element, dotNetRef, initialHtml)` — on first call injects the Jodit `<link>`/`<script>`
    and awaits load; then builds the editor with the toolbar/theme/paste config above; wires
    `change` (debounced 1000ms in JS) → `dotNetRef.invokeMethodAsync('OnEditorChanged', html)` and
    `blur` → `dotNetRef.invokeMethodAsync('OnEditorBlurred', html)`.
  - `setValue(element, html)` — replaces content without firing a change (used when the sheet
    reloads).
  - `destroy(element)` — tears the editor and the theme observer down.
  - Registered in `index.html` next to `theme.js`/`clipboard.js` (it's tiny; only Jodit itself is lazy).
- **`Shared/Fields/RichTextEditor.razor`** — parameters `Value`, `ValueChanged`, `OnCommit`
  (`EventCallback<string?>`, fired on the debounced change and on blur); renders a `<div @ref>`
  host; `OnAfterRenderAsync(firstRender)` → `create`; `[JSInvokable] OnEditorChanged/OnEditorBlurred`
  update `Value`, raise `ValueChanged`, then `OnCommit`; `OnParametersSet` with an externally
  changed `Value` → `setValue`; `IAsyncDisposable` → `destroy` (swallowing `JSDisconnectedException`).
- **Sheets:** `FichaDePersonagem.razor`/`FichaDeNpc.razor` get the new `<MudTabPanel Text="História">`,
  lose `EstrelaSelect`/`HistoricoSelect` from Informações Básicas, and add `_form.Historia` plus a
  `_historiaAutoSave` coordinator whose delegate `PUT`s `…/historia`.

## Testing (TDD — failing test first for every unit)

- **Unit** (`tests/RuinaRPG.Tests.Unit`): `HistoriaSanitizer` keeps headings, inline styles on the
  allowlist, tables with colspan, http/mailto links (and adds target/rel); strips `<script>`,
  `onclick`, `javascript:` links, `<img>`, `<iframe>`, `<style>`, disallowed CSS properties,
  `class`; returns null for empty/whitespace.
- **Integration** (`tests/RuinaRPG.Tests.Integration`), for both Personagem and NPC:
  PUT then GET round-trips sanitized HTML; malicious HTML is cleaned before persisting;
  >200,000 chars → 400; Personagem: unrelated player/other GM → 403; NPC: other GM → 404;
  the general sheet PUT leaves `Historia` untouched; granted-player NPC can write it;
  NPC deep-copy grant carries `Historia`.
- **bUnit** (`tests/RuinaRPG.Tests.Client`): `RichTextEditor` calls `ruinaRichText.create` on first
  render and propagates an `OnEditorChanged` JS callback to `ValueChanged` + `OnCommit`; both sheets
  render a "História" tab in the right position containing Estrela and Histórico, and Informações
  Básicas no longer contains them.
- **Browser check** at the end (bUnit can't run Jodit): run the app locally, open a sheet's História
  tab, confirm toolbar, pt-BR, dark theme, typing → "Salvo", reload → content persists, a table and
  a colored heading survive the round trip.

## Docs

- `Requisitos - Ficha de Personagem.md`: new tab section "História" (Estrela and Histórico moved
  there from 1.a, new História field with editor/autosave/sanitization rules); tab list updated.
- `Requisitos - Ficha de NPCs.md`: states the same tab applies to NPCs (diff against Personagem).
- `Requisitos - Modelo de Dados.md`: `Historia` on both tables.
- Changelog: untouched — left for the next release.

## Out of scope

Images/video/files inside the História; showing the História anywhere other than the sheet itself
(campaign views, Ficha de Criatura); HTML source view; collaborative/real-time editing.
