# Auto-save on Blur — Design

**Status:** Approved by user in chat (design sections + spec), ready for implementation planning.

## Goal

Replace explicit "Salvar" buttons with automatic saving triggered when a form
field loses focus, on the six edit forms where this makes sense in this
codebase. No backend changes: every form keeps calling the exact PUT endpoint
and request DTO it calls today — only the client-side trigger changes, from a
button click to a debounced blur.

## Scope

### In scope — convert to auto-save-on-blur, remove the "Salvar" button

| # | Page | Form | Endpoint (unchanged) |
|---|------|------|----------------------|
| 1 | `Pages/FichaDePersonagem.razor` | "Informações Básicas" tab | `PUT character-sheets/{SheetId}` |
| 2 | `Pages/FichaDeNpc.razor` | "Informações Básicas" tab | `PUT npc-sheets/{SheetId}` |
| 3 | `Pages/FichaDeCriatura.razor` | "Informações Básicas" tab | `PUT creature-sheets/{SheetId}` |
| 4 | `Pages/CatalogoItemForm.razor` | **edit mode only** (`ItemId is not null`) | `PUT items/{ItemId}` |
| 5 | `Pages/BancoDeMagiasForm.razor` | **edit mode only** (`EntryId is not null`) | `PUT spell-ability-bank/{EntryId}` |
| 6 | `Pages/CampanhaDetalhe.razor` | "Detalhes" tab | `PUT campaigns/{CampaignId}` |

All six are "edit an existing record" pages — none of them also serves record
creation for the same fields (the sheet pages are edit-only routes; the
Catálogo/Banco de Magias pages branch their `SubmitAsync` into POST-vs-PUT,
and only the PUT branch is in scope).

### Explicitly out of scope — unchanged, keep manual Salvar (and Cancelar where present)

- Diary entries (`FichaDePersonagem.razor`'s Diário tab, `CampanhaDetalhe.razor`'s diary tab).
- Secret notes (`CampanhaDetalhe.razor`).
- Encounter participant inline edit (`GerenciadorDeEncontros.razor`) and its "Renomear" action.
- Player password reset dialog (`GmJogadores.razor`) — a security-sensitive
  confirm action, not a persistent field edit.
- **Create mode** of `CatalogoItemForm.razor` and `BancoDeMagiasForm.razor`
  (`ItemId`/`EntryId is null`), campaign creation (`Campanhas.razor`), and every
  "Adicionar X" sub-form inside the sheet pages (add weapon, add shield, add
  spell/ability, add trait, add affinity) — all of these create a new record/
  row that doesn't exist yet; auto-save has no target to PUT to until the
  record exists, so they keep their explicit submit button.
- The sheets' existing per-field instant-PUT controls (attributes, skills,
  affinities, weapon/armor/shield durability & equip toggles, cobertura,
  ciclos, runes, masteries, affections) — these already save on every value
  change today. They are a different, already-working mechanism and are not
  touched by this work.

## Architecture

### `Shared/AutoSaveIndicator.razor` (new)

A small, presentation-only component. Parameters: `State` (enum
`AutoSaveState { Idle, Saving, Saved, Error }`) and `LastSavedAt` (`DateTime?`).
Renders nothing when `Idle`; otherwise a single muted line near the top of the
form:

- `Saving` → "Salvando…"
- `Saved` → "Salvo às {LastSavedAt:HH:mm}"
- `Error` → "Erro ao salvar"

No injected services, no HTTP calls — purely driven by the parameters the
host page passes in.

### `Services/AutoSaveCoordinator.cs` (new)

A plain C# class, **not** registered in DI — each host page instantiates its
own instance (`private readonly AutoSaveCoordinator _autoSave = new();`),
exactly the way each sheet page already owns its own local form model. It is
the shared mechanism the six forms above all use.

Responsibilities:
- Holds `AutoSaveState State` and `DateTime? LastSavedAt`, plus a
  `StateChanged` callback (`Action`) the host page subscribes to, to call its
  own `StateHasChanged()`.
- `void NotifyChanged(Func<Task<bool>> validateAndSaveAsync)` — called from
  every in-scope field's blur (or `ValueChanged` for non-text controls, see
  below). Cancels any in-flight debounce wait via a stored
  `CancellationTokenSource`, starts a new `Task.Delay(400, token)`, and when it
  elapses without being cancelled: sets `State = Saving`, raises
  `StateChanged`, awaits `validateAndSaveAsync()`.
  - The delegate returns `true` if it actually attempted the network call
    (validation passed), `false` if it was blocked by validation. On `false`,
    `State` returns to `Idle` (the field's own inline validation message
    already explains the problem — the top indicator should not also claim an
    "error" that implies a failed network call).
  - On `true` with no exception and a success HTTP status: `State = Saved`,
    `LastSavedAt = DateTime.Now`.
  - On `true` with a failed HTTP status or a thrown exception: `State = Error`.
  - This is the same debounce idiom (`Task.Delay` + `CancellationTokenSource`)
    already used for search-box filtering in `BancoDeMagias.razor`,
    `NpcsDoGm.razor`, `Compendio.razor`, `BestiarioDoGm.razor`,
    `GmJogadores.razor`, and `CampanhaDetalhe.razor` — reused here rather than
    reinvented.
- Because every field on a given form calls the same coordinator instance,
  tabbing quickly through several fields resets the same timer each time —
  the whole burst collapses into one PUT, 400ms after the last blur in the
  sequence.

### Per-page wiring (applies to all six forms)

- Keep the existing `EditForm`/local form model/`EditContext`. Add
  `<DataAnnotationsValidator />` to the two forms that don't have it yet
  (`CatalogoItemForm.razor`, and the "Detalhes" `EditForm` in
  `CampanhaDetalhe.razor`) — the other four already have it.
- Remove the `<MudButton ButtonType="Submit">Salvar</MudButton>` and the
  `OnValidSubmit` wiring on the `EditForm` (the `EditForm` stays, just with no
  submit path — nothing triggers it via Enter, since MudBlazor's inputs don't
  submit forms on Enter by default).
- Add `<AutoSaveIndicator State="_autoSave.State" LastSavedAt="_autoSave.LastSavedAt" />`
  right below each converted form's heading/`Section` title.
- For every field in the converted forms:
  - Text/numeric MudBlazor inputs (`MudTextField`, `MudNumericField`): add
    `OnBlur="@(_ => _autoSave.NotifyChanged(SaveIfValidAsync))"`.
  - Discrete-choice inputs where "blur" doesn't map to "done editing"
    (`MudSelect`, `MudCheckBox`, the image-picker's `SelectedIdsChanged`):
    call `_autoSave.NotifyChanged(SaveIfValidAsync)` from their existing
    `ValueChanged`/`SelectedIdsChanged` handler instead of `OnBlur`.
  - `BancoDeMagiasForm.razor`'s dynamic Efeitos rows: each row's `Nome`/
    `Quantidade`/`CustoPI` fields wire the same `OnBlur` as any other field;
    the "Remover" button's `OnClick` additionally calls
    `_autoSave.NotifyChanged(SaveIfValidAsync)` right after removing the row
    (since removing a row is itself a change with nothing to "blur" — there's
    no field left to lose focus from). "Adicionar Efeito" does **not** trigger
    a save by itself: the new row is empty, so a blur on its own fields will
    naturally gate on validation like any other field.
- `SaveIfValidAsync()` — the method the coordinator's delegate calls — is the
  existing `SaveAsync`/`SubmitAsync`/`SaveDetailsAsync` method for that page,
  reshaped to:
  1. `if (!_editContext.Validate()) return false;`
  2. Build the same request DTO the current code builds, PUT it to the same
     endpoint.
  3. If the PUT's `response.IsSuccessStatusCode` is `false`, throw an
     exception (e.g. `new InvalidOperationException("Não foi possível
     salvar.")`) instead of returning a value — `NotifyChanged`'s delegate
     invocation is wrapped in a `try/catch` inside `AutoSaveCoordinator`, which
     sets `State = Error` on any caught exception. On a successful response,
     return `true`. This one shape (return `false` only for a validation
     block; throw for an HTTP/network failure; return `true` for success)
     applies identically across all six forms — no per-form variation.
  4. **No `Navigation.NavigateTo(...)` call.** `CatalogoItemForm.razor` and
     `BancoDeMagiasForm.razor` currently navigate away
     (`/catalogo`, `/banco-de-magias`) after every successful save, including
     edits — that line must be removed for the edit-mode path, since
     navigating away after the very first field's auto-save would kick the
     user out of the page they're still editing. The create-mode path (POST,
     out of scope, unchanged) keeps its navigate-away.
  5. Existing full-page `_errorMessage` fields (rendered via `MudAlert` at the
     top of the page) are no longer set by these six save methods — the
     `AutoSaveIndicator`'s `Error` state replaces that role for save failures.
     `_errorMessage` may still be used by unrelated code on the same page
     (e.g. image upload failures) and is left in place for those.

### Validation

Per-field inline errors (`ValidationMessage`/MudBlazor's built-in error
styling via the `For` parameter) only work where the local form model actually
carries `DataAnnotations` attributes tied to `EditContext`. Today:

- `CatalogoItemForm.razor`'s `ItemFormModel` and `BancoDeMagiasForm.razor`'s
  `EntryFormModel`/`EffectFormModel` carry **no** attributes, even though the
  matching Contracts DTOs (`UpdateItemRequest`, `UpdateSpellAbilityEntryRequest`,
  `SpellAbilityEffectRequest`) already declare `[Range]`/`[Required]`
  constraints server-side. This work copies those exact attributes onto the
  local models (same bounds, no new rules invented) and adds a `For="..."`
  expression to each corresponding field that doesn't already have one, so the
  existing `<DataAnnotationsValidator />` becomes meaningful instead of inert.
- `FichaDePersonagem.razor`/`FichaDeNpc.razor`/`FichaDeCriatura.razor`'s local
  form models and `UpdateCharacterSheetRequest` (and the NPC/creature
  equivalents), and `CampanhaDetalhe.razor`'s `CampaignDetailsFormModel` /
  `UpdateCampaignRequest`, carry **no** DataAnnotations today. This work does
  **not** invent new validation rules for them — the only pre-save gate for
  these four forms remains whatever manual check already exists in their
  current save method (e.g. `FichaDePersonagem.razor`'s existing null-check on
  required numeric fields before building the request), now run inside
  `SaveIfValidAsync` before the PUT instead of inside the old button handler.
  If that manual check fails, treat it the same as an `EditContext.Validate()`
  failure: return `false`, no save, existing inline messaging for that check
  stays as-is (whatever the current code already shows next to/above the
  field).

## Testing

- New bUnit tests for `AutoSaveIndicator` (renders nothing when `Idle`, correct
  text for `Saving`/`Saved`/`Error`, `Saved` formats `LastSavedAt` as `HH:mm`).
- New unit tests for `AutoSaveCoordinator` (debounce collapses rapid
  `NotifyChanged` calls into one invocation of the delegate; `false` return
  from the delegate leaves `State == Idle`; a thrown exception or a `false`
  HTTP outcome from the delegate's own internal handling results in
  `State == Error`; a clean success results in `State == Saved` with
  `LastSavedAt` set) — these can run as plain xUnit tests against the
  coordinator class directly (no Blazor rendering needed), using
  `Task.Delay`-based timing or (preferably) an injectable delay/clock seam if
  the implementation plan finds that cleaner to test deterministically.
- Existing bUnit tests for the six converted pages/forms that currently assert
  on the "Salvar" button or `OnValidSubmit` flow must be updated to instead
  simulate a field blur and assert the resulting PUT call and indicator state.
  Grep each of the six files' matching test file under
  `tests/RuinaRPG.Tests.Client/` before changing behavior, so no coverage is
  silently dropped.
- Existing integration tests (`tests/RuinaRPG.Tests.Integration/`) hit the
  same PUT endpoints and DTOs as before — untouched by this change, since no
  backend code changes. They remain the safety net proving the endpoints still
  behave identically regardless of what triggers the client call.

## Non-goals

- No backend/API changes of any kind — same endpoints, same DTOs, same
  response shapes.
- No change to the sheets' existing per-field instant-PUT controls (already
  effectively auto-save; out of scope by design, see Scope above).
- No change to diary/secret-note/encounter-participant/password-reset flows.
- No new field-level partial-update (PATCH) endpoints — every in-scope save
  keeps sending the whole record, as it does today.
- No optimistic local-only "offline" queueing of failed saves — on `Error`,
  the user's edit remains in the form (nothing is discarded), the indicator
  shows "Erro ao salvar", and the very next successful blur on that same form
  (via the same debounce path) will retry with the form's current values.
