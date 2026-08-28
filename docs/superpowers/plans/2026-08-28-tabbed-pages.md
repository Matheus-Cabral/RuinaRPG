# Tabbed Multi-Topic Pages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the Campanha page and the three Ficha pages (Personagem, NPC, Criatura) plus Minha Campanha into Bootstrap-style tabs, and bring the forms/tables/buttons inside those tabs onto real Bootstrap conventions (`.form-label`/`.form-control`, `.table`, intent-classified `.btn-*`) so each tab reads as one coherent, usable unit.

**Architecture:** A new reusable `TabControl`/`TabPage` component pair (Blazor state-driven, no JS) renders Bootstrap's `nav-tabs`/`tab-content`/`tab-pane` markup. Each target page's existing `<h2>`-delimited sections become tab bodies verbatim — same `@code`, same bindings, same handlers, just wrapped and reformatted. No markup changes to pages outside this plan's scope; they keep Round 2's bare-element CSS (real Bootstrap classes outrank it, so no conflict where the two meet).

**Tech Stack:** Blazor WebAssembly 8, Bootstrap 5 CSS (already vendored, no new dependency), the existing `--rr-*` design tokens from `theme.css`/`components.css`.

**Spec:** `docs/superpowers/specs/2026-08-28-tabbed-pages-design.md` — read this first; it has the full per-page tab breakdown, the Bootstrap conversion conventions (field wrapper, checkbox, input-group, row/column grouping, button intent-to-class table), and the reasoning behind the tab boundaries. This plan's tasks apply that spec; where anything here and the spec seem to disagree, the spec wins.

## Global Constraints

- Zero behavior change: every `@code` member, `@bind`/`@bind-Value` target, `@onclick`/`@onchange` handler, and HTTP call must survive identically in every task — this is a markup-only reorganization.
- Tab titles are copied verbatim from the page's current `<h2>` text.
- Page-level elements (`<h1>`, the `@if (_errorMessage...)` error paragraph, `FichaDePersonagem.razor`'s level-up notice block) stay above/outside the `TabControl`, unchanged.
- Button classification (apply uniformly, do not improvise per page):
  | Intent | Example labels | Class |
  |---|---|---|
  | Primary form submit / create / attach / search | Salvar (the button that submits an `<EditForm>`), Adicionar *, Criar Ficha de Personagem, Conceder, Anexar *, Buscar, Novo Encontro | `btn btn-primary` |
  | Inline list-item action (non-destructive) | Editar, **Salvar (the inline edit-in-place confirm button, paired with an inline Cancelar — e.g. editing a diary entry or secret note directly in its `<li>`)**, Equipar/Desequipar, Adicionar Efeito | `btn btn-outline-primary btn-sm` |
  | Destructive | Remover, Excluir | `btn btn-outline-danger btn-sm` |
  | Cancel | Cancelar | `btn btn-outline-secondary btn-sm` |
  Use `btn-sm` for buttons inside a `<li>`/table row; full-size `btn` for the button that submits an `<EditForm>`. **"Salvar" is context-dependent, not label-dependent**: the one `<EditForm>`-submit "Salvar" per tab is `btn btn-primary`; every other "Salvar" (always an inline row-edit confirm, always paired with an inline "Cancelar") is `btn btn-outline-primary btn-sm` — see `CampanhaDetalhe.razor`'s Diário/Notas Secretas tabs (Task 2) for the worked example of both.
- Field wrapper: `<label>Campo <InputX .../></label>` → `<div class="mb-3"><label class="form-label">Campo</label><InputX class="form-control" .../></div>` (or `class="form-select"` for selects). Checkboxes → `<div class="mb-3 form-check"><InputCheckbox class="form-check-input" .../><label class="form-check-label">Campo</label></div>`.
- "Current / Maximum" fields (trailing `/ @_form.XMaximo` text) → Bootstrap input-group (see Task 3 worked example).
- Tables: `<table>` → `<table class="table">`. Editable `<td>` cells keep their current bare `<input>` structure (Round 2's CSS still narrows them).
- `dotnet build` must stay at 0 warnings/0 errors after every task.
- `src/RuinaRPG.Client/_Imports.razor` already has `@using RuinaRPG.Client.Shared` (added by Task 2, the first consumer of `TabControl`/`TabPage` — without it every page gets 7 `RZ10012` warnings). It applies to every `.razor` file in the project already — **do not add a per-page `@using RuinaRPG.Client.Shared`**, that would trigger a CS0105 duplicate-using warning and break the 0-warnings bar.
- No Blazor component test infrastructure exists in this repo — verification is `dotnet build` + running `dotnet run --project src/RuinaRPG.Client` standalone and `curl`-checking the served HTML for the expected tab/Bootstrap markup, plus careful manual diff-reading to confirm every binding/handler survived. No screenshot tool is available in this environment.

---

### Task 1: `TabControl` / `TabPage` shared components

**Files:**
- Create: `src/RuinaRPG.Client/Shared/TabControl.razor`
- Create: `src/RuinaRPG.Client/Shared/TabControl.razor.css`
- Create: `src/RuinaRPG.Client/Shared/TabPage.razor`

**Interfaces:**
- Produces: `<TabControl>` (parameter: `ChildContent`) wrapping one or more `<TabPage Title="...">` (parameters: `Title` (required string), `ChildContent`) — this is the public API every later task consumes. Usage:
  ```razor
  <TabControl>
      <TabPage Title="Aba Um"> ...markup... </TabPage>
      <TabPage Title="Aba Dois"> ...markup... </TabPage>
  </TabControl>
  ```
- `TabPage` registers itself with the nearest `TabControl` via `[CascadingParameter]`; no other page needs to reference `TabControl`'s or `TabPage`'s internals directly.

- [ ] **Step 1: Create `TabControl.razor`**

```razor
<div class="tab-control">
    <ul class="nav nav-tabs" role="tablist">
        @foreach (var page in _pages)
        {
            <li class="nav-item" role="presentation">
                <button type="button"
                        class="nav-link @(IsActive(page) ? "active" : "")"
                        role="tab"
                        aria-selected="@(IsActive(page) ? "true" : "false")"
                        @onclick="@(() => Activate(page))">
                    @page.Title
                </button>
            </li>
        }
    </ul>
    <div class="tab-content">
        <CascadingValue Value="this" IsFixed="true">
            @ChildContent
        </CascadingValue>
    </div>
</div>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private readonly List<TabPage> _pages = new();
    private TabPage? _activePage;

    internal void Register(TabPage page)
    {
        _pages.Add(page);
        _activePage ??= page;
        StateHasChanged();
    }

    internal bool IsActive(TabPage page) => ReferenceEquals(page, _activePage);

    private void Activate(TabPage page) => _activePage = page;
}
```

- [ ] **Step 2: Create `TabPage.razor`**

```razor
<div class="tab-pane @(IsActive ? "show active" : "d-none")" role="tabpanel">
    @ChildContent
</div>

@code {
    [CascadingParameter] public TabControl? Parent { get; set; }
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool IsActive => Parent is not null && Parent.IsActive(this);

    protected override void OnInitialized() => Parent?.Register(this);
}
```

- [ ] **Step 3: Create `TabControl.razor.css`**

```css
.tab-control .nav-tabs {
  border-bottom-color: var(--rr-border);
}

.tab-control .nav-link {
  color: var(--rr-text-muted);
  border: none;
  border-bottom: 2px solid transparent;
  background: none;
  padding: var(--rr-space-2) var(--rr-space-3);
  font-family: var(--rr-font-body);
  font-weight: 600;
  border-radius: 0;
}

.tab-control .nav-link:hover {
  color: var(--rr-text);
  border-bottom-color: var(--rr-border);
}

.tab-control .nav-link.active {
  color: var(--rr-primary);
  border-bottom-color: var(--rr-primary);
  background: none;
}

.tab-control .tab-content {
  padding-top: var(--rr-space-4);
}
```

- [ ] **Step 4: Verify the `.nav-link` scoped rule actually matches, empirically**

Blazor CSS isolation stamps a scope attribute only on elements a component's *own* markup renders directly, never on a child component's output (this exact gap caused a Critical bug in the Round 1 frontend work — `NavMenu.razor.css`'s `.nav-link` rule never matched any `<NavLink>`-rendered link because `<NavLink>` is a child component; the fix was the `::deep` combinator). Here, `TabControl.razor` renders the `<button class="nav-link">` **directly in its own markup** (not via a child component), so plain scoping should suffice without `::deep` — but do not assume this, verify it:

1. `dotnet build src/RuinaRPG.Client/RuinaRPG.Client.csproj`
2. Inspect the generated scoped CSS: `cat src/RuinaRPG.Client/obj/Debug/net8.0/scopedcss/Shared/TabControl.razor.rz.scp.css` and confirm it contains a rule shaped like `.tab-control .nav-link[b-xxxxxxxxxx] { ... }` (a scope attribute selector directly on `.nav-link`, not requiring `::deep`).
3. Also check the compiled component's generated output confirms the `<button>` in `TabControl.razor`'s render tree carries that same `b-xxxxxxxxxx` scope attribute (search `src/RuinaRPG.Client/obj/Debug/net8.0/generated/**/TabControl_razor.g.cs` for the scope attribute being added to the button's markup frame, or simpler: run the dev server per Step 5 below and curl a page using `TabControl` to confirm the rendered `<button class="nav-link ...">` in the HTML carries a `b-xxxxxxxxxx` attribute matching the CSS selector).

If it doesn't match (i.e., the CSS rule and the rendered element's scope attribute disagree), add `::deep` to the `.nav-link`/`.nav-tabs`/`.tab-content` selectors in `TabControl.razor.css` and re-verify. Record which outcome you found in your report — this determines whether later tasks' pages render tabs with the intended flat/underlined look or fall back to Round 2's filled-button look.

- [ ] **Step 5: Build and smoke-test standalone**

```bash
dotnet build
dotnet run --project src/RuinaRPG.Client --urls http://127.0.0.1:5299 &
sleep 8
curl -s http://127.0.0.1:5299/ -o /dev/null -w "%{http_code}\n"
kill %1
```
Expected: build 0 warnings/0 errors, HTTP 200. (No page uses `TabControl` yet, so there's nothing else to curl for content until Task 2 lands — this step only confirms the new components don't break compilation or startup.)

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Shared/TabControl.razor src/RuinaRPG.Client/Shared/TabControl.razor.css src/RuinaRPG.Client/Shared/TabPage.razor
git commit -m "feat: add reusable TabControl/TabPage components"
```

---

### Task 2: Tab `CampanhaDetalhe.razor`

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`

**Interfaces:**
- Consumes: `TabControl`/`TabPage` from Task 1 (`src/RuinaRPG.Client/Shared/`).
- No `@code` changes — every field/method in the existing `@code` block (lines 187–553 of the current file) stays exactly as-is; only the markup above it (lines 1–186) is restructured.

The current file has this shape (`<h1>Campanha</h1>`, then the error paragraph, then six `<h2>`-delimited sections: Membros, Diário, Encontros, Anexos, Conceder Ficha, Notas Secretas). Read the full current file before starting — this task shows the target shape for the first two tabs in full; apply the same conventions to the remaining four using the spec's button/field/table tables.

- [ ] **Step 1: Wrap the six sections in `TabControl`/`TabPage`, converting fields/buttons per convention**

Replace the body from `<h2>Membros</h2>` through the end of the Notas Secretas `<ul>` (i.e., everything between the error paragraph and the `@code` block) with:

```razor
<TabControl>
    <TabPage Title="Membros">
        <div class="mb-3">
            <label class="form-label">Buscar jogador para adicionar</label>
            <input class="form-control" @bind="_memberSearch" placeholder="Buscar jogador para adicionar" />
        </div>
        <button type="button" class="btn btn-primary" @onclick="SearchPlayersAsync">Buscar</button>
        <ul class="list-unstyled mt-3">
            @foreach (var player in _searchResults)
            {
                <li>@player.Nickname (@player.Email) <button type="button" class="btn btn-outline-primary btn-sm" @onclick="@(() => AddMemberAsync(player.Id))">Adicionar</button></li>
            }
        </ul>
        <ul class="list-unstyled">
            @foreach (var member in _members)
            {
                <li>
                    @member.Nickname (@member.Email)
                    <button type="button" class="btn btn-outline-primary btn-sm" @onclick="@(() => CreateSheetForAsync(member.UserId))">Criar Ficha de Personagem</button>
                    <ul>
                        @foreach (var sheet in _sheets.Where(s => s.OwnerId == member.UserId))
                        {
                            <li><a href="@($"fichas/{sheet.Id}")">@(sheet.Nome ?? "(sem nome)")</a></li>
                        }
                    </ul>
                </li>
            }
        </ul>
    </TabPage>

    <TabPage Title="Diário">
        <EditForm Model="_diaryForm" OnValidSubmit="AddDiaryEntryAsync">
            <div class="mb-3">
                <label class="form-label">Texto</label>
                <InputTextArea class="form-control" @bind-Value="_diaryForm.Texto" />
            </div>
            <button type="submit" class="btn btn-primary">Adicionar Entrada</button>
        </EditForm>
        <ul class="list-unstyled mt-3">
            @foreach (var entry in _diaryEntries)
            {
                <li>
                    @if (_editingEntryId == entry.Id)
                    {
                        <textarea class="form-control" @bind="_editText"></textarea>
                        <button type="button" class="btn btn-outline-primary btn-sm" @onclick="@(() => SaveDiaryEntryAsync(entry.Id))">Salvar</button>
                        <button type="button" class="btn btn-outline-secondary btn-sm" @onclick="CancelEditDiaryEntry">Cancelar</button>
                    }
                    else
                    {
                        <span>@entry.CreatedAt.ToString("g") — @entry.Texto</span>
                        <button type="button" class="btn btn-outline-primary btn-sm" @onclick="@(() => StartEditDiaryEntry(entry))">Editar</button>
                        <button type="button" class="btn btn-outline-danger btn-sm" @onclick="@(() => DeleteDiaryEntryAsync(entry.Id))">Excluir</button>
                    }
                </li>
            }
        </ul>
    </TabPage>

    <TabPage Title="Encontros">
        <button type="button" class="btn btn-primary" @onclick="CreateEncounterAsync">Novo Encontro</button>
        <ul class="list-unstyled mt-3">
            @foreach (var encounter in _encounters)
            {
                <li><a href="@($"campanhas/{CampaignId}/encontros/{encounter.Id}")">@(encounter.Nome ?? "(sem nome)")</a></li>
            }
        </ul>
    </TabPage>

    <TabPage Title="Anexos">
        <div class="row g-3 mb-3">
            <div class="col-md-6">
                <label class="form-label">Item (id)</label>
                <div class="input-group">
                    <input class="form-control" @bind="_attachItemId" />
                    <button type="button" class="btn btn-primary" @onclick="AttachItemAsync">Anexar Item</button>
                </div>
            </div>
            <div class="col-md-6">
                <label class="form-label">Entrada do Banco de Magias (id)</label>
                <div class="input-group">
                    <input class="form-control" @bind="_attachBankEntryId" />
                    <button type="button" class="btn btn-primary" @onclick="AttachBankEntryAsync">Anexar Magia/Habilidade</button>
                </div>
            </div>
            <div class="col-md-6">
                <label class="form-label">Imagem (id)</label>
                <div class="input-group">
                    <input class="form-control" @bind="_attachImageId" />
                    <button type="button" class="btn btn-primary" @onclick="AttachImageAsync">Anexar Imagem</button>
                </div>
            </div>
            <div class="col-md-6">
                <label class="form-label">Ficha de NPC (id)</label>
                <div class="input-group">
                    <input class="form-control" @bind="_attachNpcSheetId" />
                    <button type="button" class="btn btn-primary" @onclick="AttachNpcSheetAsync">Anexar NPC</button>
                </div>
            </div>
            <div class="col-md-6">
                <label class="form-label">Ficha de Criatura (id)</label>
                <div class="input-group">
                    <input class="form-control" @bind="_attachCreatureSheetId" />
                    <button type="button" class="btn btn-primary" @onclick="AttachCreatureSheetAsync">Anexar Criatura</button>
                </div>
            </div>
        </div>
        <ul class="list-unstyled">
            @foreach (var attachment in _attachments)
            {
                <li>
                    [@attachment.Tipo] @attachment.Nome
                    @if (attachment.Tipo == "NpcSheet")
                    {
                        <div class="form-check form-check-inline">
                            <input class="form-check-input" type="checkbox" checked="@attachment.NpcNomePublico" @onchange="@(e => ToggleNpcVisibilityAsync(attachment, (bool)e.Value!, attachment.NpcImagemPublica ?? false))" />
                            <label class="form-check-label">Nome público</label>
                        </div>
                        <div class="form-check form-check-inline">
                            <input class="form-check-input" type="checkbox" checked="@attachment.NpcImagemPublica" @onchange="@(e => ToggleNpcVisibilityAsync(attachment, attachment.NpcNomePublico ?? false, (bool)e.Value!))" />
                            <label class="form-check-label">Imagem pública</label>
                        </div>
                    }
                    else if (attachment.Tipo == "CreatureSheet")
                    {
                        <div class="form-check form-check-inline">
                            <input class="form-check-input" type="checkbox" checked="@attachment.CreatureNomePublico" @onchange="@(e => ToggleCreatureVisibilityAsync(attachment, (bool)e.Value!, attachment.CreatureImagemPublica ?? false))" />
                            <label class="form-check-label">Nome público</label>
                        </div>
                        <div class="form-check form-check-inline">
                            <input class="form-check-input" type="checkbox" checked="@attachment.CreatureImagemPublica" @onchange="@(e => ToggleCreatureVisibilityAsync(attachment, attachment.CreatureNomePublico ?? false, (bool)e.Value!))" />
                            <label class="form-check-label">Imagem pública</label>
                        </div>
                    }
                    else
                    {
                        <div class="form-check form-check-inline">
                            <input class="form-check-input" type="checkbox" checked="@attachment.IsPublic" @onchange="@(e => ToggleVisibilityAsync(attachment, (bool)e.Value!))" />
                            <label class="form-check-label">Público</label>
                        </div>
                    }
                    <button type="button" class="btn btn-outline-danger btn-sm" @onclick="@(() => RemoveAttachmentAsync(attachment.Id))">Remover</button>
                </li>
            }
        </ul>
    </TabPage>

    <TabPage Title="Conceder Ficha">
        <div class="row g-3">
            <div class="col-md-4">
                <label class="form-label">Jogador</label>
                <select class="form-select" @bind="_grantPlayerId">
                    <option value="">(selecione)</option>
                    @foreach (var member in _members)
                    {
                        <option value="@member.UserId">@member.Nickname</option>
                    }
                </select>
            </div>
            <div class="col-md-3">
                <label class="form-label">Tipo</label>
                <select class="form-select" @bind="_grantTipo">
                    <option value="Npc">NPC</option>
                    <option value="Creature">Criatura</option>
                </select>
            </div>
            <div class="col-md-5">
                <label class="form-label">Ficha de origem (id, opcional — em branco cria uma ficha nova)</label>
                <input class="form-control" @bind="_grantSourceSheetId" />
            </div>
        </div>
        <button type="button" class="btn btn-primary mt-3" @onclick="GrantSheetAsync">Conceder</button>
    </TabPage>

    <TabPage Title="Notas Secretas">
        <EditForm Model="_noteForm" OnValidSubmit="AddSecretNoteAsync">
            <div class="mb-3">
                <label class="form-label">Texto</label>
                <InputTextArea class="form-control" @bind-Value="_noteForm.Texto" />
            </div>
            <p>Destinatários:</p>
            <ul class="list-unstyled">
                @foreach (var member in _members)
                {
                    <li>
                        <div class="form-check">
                            <input class="form-check-input" type="checkbox" checked="@_noteRecipients.Contains(member.UserId)" @onchange="@(e => ToggleNoteRecipient(member.UserId, (bool)e.Value!))" />
                            <label class="form-check-label">@member.Nickname</label>
                        </div>
                    </li>
                }
            </ul>
            <button type="submit" class="btn btn-primary">Adicionar Nota Secreta</button>
        </EditForm>
        <ul class="list-unstyled mt-3">
            @foreach (var note in _secretNotes)
            {
                <li>
                    @if (_editingNoteId == note.Id)
                    {
                        <textarea class="form-control" @bind="_editNoteText"></textarea>
                        <ul class="list-unstyled">
                            @foreach (var member in _members)
                            {
                                <li>
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" checked="@_editNoteRecipients.Contains(member.UserId)" @onchange="@(e => ToggleEditNoteRecipient(member.UserId, (bool)e.Value!))" />
                                        <label class="form-check-label">@member.Nickname</label>
                                    </div>
                                </li>
                            }
                        </ul>
                        <button type="button" class="btn btn-outline-primary btn-sm" @onclick="@(() => SaveSecretNoteAsync(note.Id))">Salvar</button>
                        <button type="button" class="btn btn-outline-secondary btn-sm" @onclick="CancelEditSecretNote">Cancelar</button>
                    }
                    else
                    {
                        <span>@note.CreatedAt.ToString("g") — @note.Texto (destinatários: @note.RecipientUserIds.Count)</span>
                        <button type="button" class="btn btn-outline-primary btn-sm" @onclick="@(() => StartEditSecretNote(note))">Editar</button>
                        <button type="button" class="btn btn-outline-danger btn-sm" @onclick="@(() => DeleteSecretNoteAsync(note.Id))">Excluir</button>
                    }
                </li>
            }
        </ul>
    </TabPage>
</TabControl>
```

Keep `<h1>Campanha</h1>` and the `@if (_errorMessage is not null) { <p class="error">@_errorMessage</p> }` block exactly where they are today, above this `<TabControl>`.

- [ ] **Step 2: Verify no `@code` drift**

Diff the task's changes against the original file and confirm: every method name (`SearchPlayersAsync`, `AddMemberAsync`, `CreateSheetForAsync`, `AddDiaryEntryAsync`, `StartEditDiaryEntry`, `CancelEditDiaryEntry`, `SaveDiaryEntryAsync`, `DeleteDiaryEntryAsync`, `CreateEncounterAsync`, `AttachItemAsync`, `AttachBankEntryAsync`, `AttachImageAsync`, `AttachNpcSheetAsync`, `AttachCreatureSheetAsync`, `ToggleNpcVisibilityAsync`, `ToggleCreatureVisibilityAsync`, `ToggleVisibilityAsync`, `RemoveAttachmentAsync`, `GrantSheetAsync`, `ToggleNoteRecipient`, `ToggleEditNoteRecipient`, `AddSecretNoteAsync`, `StartEditSecretNote`, `CancelEditSecretNote`, `SaveSecretNoteAsync`, `DeleteSecretNoteAsync`) and every field (`_memberSearch`, `_searchResults`, `_members`, `_sheets`, `_diaryForm`, `_diaryEntries`, `_editingEntryId`, `_editText`, `_encounters`, `_attachItemId`, `_attachBankEntryId`, `_attachImageId`, `_attachNpcSheetId`, `_attachCreatureSheetId`, `_attachments`, `_grantPlayerId`, `_grantTipo`, `_grantSourceSheetId`, `_noteForm`, `_noteRecipients`, `_editNoteRecipients`, `_secretNotes`, `_editingNoteId`, `_editNoteText`) is referenced exactly as many times, doing exactly the same thing, as before your change. The `@code` block itself (lines 187 onward in the original) must be byte-identical.

- [ ] **Step 3: Build and smoke-test**

```bash
dotnet build
dotnet run --project src/RuinaRPG.Client --urls http://127.0.0.1:5299 &
sleep 8
curl -s http://127.0.0.1:5299/campanhas/test-id | grep -c "nav-tabs\|tab-pane"
kill %1
```
Expected: build 0/0; the grep count > 0 (confirms the tab markup actually renders — the route will likely 404/error on data load since `test-id` isn't real, but the static shell around `@if`/`TabControl` still renders since `OnInitializedAsync` failures don't prevent the markup from being emitted, only the data-dependent inner loops stay empty).

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor
git commit -m "refactor: reorganize CampanhaDetalhe.razor into Bootstrap tabs"
```

---

### Task 3: Tab `FichaDePersonagem.razor`

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`

**Interfaces:**
- Consumes: `TabControl`/`TabPage` from Task 1. Mirror the conversion pattern `CampanhaDetalhe.razor` now uses (Task 2) for buttons/fields/checkboxes you don't see a worked example for below.
- No `@code` changes — the entire `@code` block (everything from `[Parameter] public string SheetId` onward in the current file) stays byte-identical; only the markup above it is restructured.

Read the full current file before starting. Six tabs, titled exactly: Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses, Diário — one `TabPage` per current `<h2>`-delimited region (the `<h2>` itself is dropped; keep every `<h3>`/`<h4>` inside its tab as-is, just add `class="table"` to `<table>` and `class="mb-3"`/`class="row g-3"` wrappers to fields per the spec).

- [ ] **Step 1: Wrap "Informações Básicas" (worked example — apply the same field/button/checkbox conventions to the other five tabs)**

Replace `<h2>Informações Básicas</h2>` through the closing `</EditForm>` of that section with the first `TabPage` of a `TabControl` wrapping all six tabs:

```razor
<TabControl>
    <TabPage Title="Informações Básicas">
        <EditForm Model="_form" OnValidSubmit="SaveAsync">
            <div class="row g-3">
                <div class="col-md-3">
                    <label class="form-label">Nome</label>
                    <InputText class="form-control" @bind-Value="_form.Nome" />
                </div>
                <div class="col-md-3">
                    <label class="form-label">Linhagem</label>
                    <select class="form-select" @bind="_form.Linhagem">
                        <option value="">Escolha uma Linhagem</option>
                        <option value="Humano">Humano</option>
                        <option value="Phylauc">Phylauc</option>
                        <option value="Nephrytes">Nephrytes</option>
                        <option value="Econos">Ecônos</option>
                    </select>
                </div>
                <div class="col-md-3">
                    <label class="form-label">Variante</label>
                    <InputText class="form-control" @bind-Value="_form.Variante" />
                </div>
                <div class="col-md-3">
                    <label class="form-label">Vocação</label>
                    <select class="form-select" @bind="_form.Vocacao">
                        <option value="">Escolha uma Vocação</option>
                        <option value="Campeao">Campeão</option>
                        <option value="Cacador">Caçador</option>
                        <option value="Feiticeiro">Feiticeiro</option>
                        <option value="Adepto">Adepto</option>
                        <option value="Bruxo">Bruxo</option>
                    </select>
                </div>
                <div class="col-md-3">
                    <label class="form-label">Sub-vocação</label>
                    <InputText class="form-control" @bind-Value="_form.SubVocacao" />
                </div>
                <div class="col-md-3">
                    <label class="form-label">Afinidade</label>
                    <InputText class="form-control" @bind-Value="_form.Afinidade" />
                </div>
                <div class="col-md-3">
                    <label class="form-label">Propriedade</label>
                    <InputText class="form-control" @bind-Value="_form.Propriedade" />
                </div>
            </div>

            <div class="row g-3 mt-1">
                <div class="col-md-2">
                    <label class="form-label">Nível</label>
                    <InputNumber class="form-control" @bind-Value="_form.Nivel" />
                </div>
                <div class="col-md-3 pt-4">
                    <p class="mb-0">@_form.GraduacaoLabel: @_form.Graduacao</p>
                </div>
                <div class="col-md-3 pt-4">
                    <div class="form-check">
                        <InputCheckbox class="form-check-input" @bind-Value="_form.PossuiCoracaoDeMana" />
                        <label class="form-check-label">Possui Coração de Mana?</label>
                    </div>
                </div>
                <div class="col-md-2">
                    <label class="form-label">Experiência Atual</label>
                    <InputNumber class="form-control" @bind-Value="_form.ExperienciaAtual" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">EAP Atual</label>
                    <InputNumber class="form-control" @bind-Value="_form.EAPAtual" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Pontos de Ignição Atual</label>
                    <InputNumber class="form-control" @bind-Value="_form.PontosDeIgnicaoAtual" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Pontos de Ignição Total</label>
                    <InputNumber class="form-control" @bind-Value="_form.PontosDeIgnicaoTotal" />
                </div>
            </div>

            <h3>Âmbares Absorvidos</h3>
            <div class="row g-3">
                <div class="col">
                    <label class="form-label">Rank F</label>
                    <InputNumber class="form-control" @bind-Value="_form.NucleosRankF" />
                </div>
                <div class="col">
                    <label class="form-label">Rank E</label>
                    <InputNumber class="form-control" @bind-Value="_form.NucleosRankE" />
                </div>
                <div class="col">
                    <label class="form-label">Rank D</label>
                    <InputNumber class="form-control" @bind-Value="_form.NucleosRankD" />
                </div>
                <div class="col">
                    <label class="form-label">Rank C</label>
                    <InputNumber class="form-control" @bind-Value="_form.NucleosRankC" />
                </div>
                <div class="col">
                    <label class="form-label">Rank B</label>
                    <InputNumber class="form-control" @bind-Value="_form.NucleosRankB" />
                </div>
                <div class="col">
                    <label class="form-label">Rank A</label>
                    <InputNumber class="form-control" @bind-Value="_form.NucleosRankA" />
                </div>
                <div class="col">
                    <label class="form-label">Rank S</label>
                    <InputNumber class="form-control" @bind-Value="_form.NucleosRankS" />
                </div>
            </div>

            <h3>Recursos</h3>
            <div class="row g-3">
                <div class="col-md-3">
                    <label class="form-label">Adrenalina (PA)</label>
                    <div class="input-group">
                        <InputNumber class="form-control" @bind-Value="_form.AdrenalinaAtual" />
                        <span class="input-group-text">/ @_form.AdrenalinaMaximo</span>
                    </div>
                </div>
                <div class="col-md-3">
                    <label class="form-label">Foco (PF)</label>
                    <div class="input-group">
                        <InputNumber class="form-control" @bind-Value="_form.FocoAtual" />
                        <span class="input-group-text">/ @_form.FocoMaximo</span>
                    </div>
                </div>
                <div class="col-md-3">
                    <label class="form-label">Estresse</label>
                    <div class="input-group">
                        <InputNumber class="form-control" @bind-Value="_form.EstresseAtual" />
                        <span class="input-group-text">/ @_form.EstresseMaximo</span>
                    </div>
                </div>
                <div class="col-md-3">
                    <label class="form-label">Vitalidade</label>
                    <div class="input-group">
                        <InputNumber class="form-control" @bind-Value="_form.VitalidadeAtual" />
                        <span class="input-group-text">/ @_form.VitalidadeMaximo</span>
                    </div>
                </div>
            </div>

            <button type="submit" class="btn btn-primary mt-3">Salvar</button>
        </EditForm>
    </TabPage>

    <TabPage Title="Atributos & Perícias">
        <!-- existing content of this section, with <table> -> <table class="table">,
             and the Afinidades <ul>'s Remover button -> class="btn btn-outline-danger btn-sm" -->
    </TabPage>

    <TabPage Title="Combate">
        <!-- existing content: Armas/Armaduras/Escudos/Sub-Atributos <h3> blocks unchanged structurally;
             Equipar/Desequipar button -> class="btn btn-outline-primary btn-sm" -->
    </TabPage>

    <TabPage Title="Magias & Habilidades">
        <!-- existing content: Habilidade Racial / Magias e Habilidades / Runas / Maestrias <h3> blocks;
             apply the field-wrapper convention to every <label> in the three <EditForm>s here,
             "Adicionar Efeito" -> btn btn-outline-primary btn-sm, submit buttons -> btn btn-primary,
             all "Remover" -> btn btn-outline-danger btn-sm -->
    </TabPage>

    <TabPage Title="Posses">
        <!-- existing content: Inventário / Artefatos / Afeições / Características <h3> blocks,
             same field/button conventions -->
    </TabPage>

    <TabPage Title="Diário">
        <!-- existing content, same pattern as CampanhaDetalhe.razor's Diário tab from Task 2 -->
    </TabPage>
</TabControl>
```

The five commented-out tabs above are not placeholders to leave in the code — they mark where you carry over the *existing* markup for that section (read it from the current file) applying the same field/button/table conventions demonstrated in full for "Informações Básicas" and in `CampanhaDetalhe.razor` (Task 2). Every `@foreach`/`@if`/`@for` loop, every interpolated value, and every event handler wire-up in those sections must be preserved exactly — only the surrounding tags change.

Keep `<h1>Ficha de Personagem</h1>`, the `@if (_errorMessage...)` block, and the level-up notice `@if (_levelUpBonuses.Count > 0) { <div class="level-up-notice">...</div> }` exactly where they are today, above this `<TabControl>`. Give the level-up notice's "Fechar" button `class="btn btn-outline-secondary btn-sm"`.

- [ ] **Step 2: Verify no `@code` drift**

Same method as Task 2 Step 2: confirm every field and method referenced in the markup you touched still appears, doing the same thing, the same number of times. The `@code` block must be byte-identical to before.

- [ ] **Step 3: Build and smoke-test**

```bash
dotnet build
dotnet run --project src/RuinaRPG.Client --urls http://127.0.0.1:5299 &
sleep 8
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5299/fichas/test-id
kill %1
```
Expected: build 0/0; HTTP 200. **Note (found during Task 2):** `RuinaRPG.Client` is Blazor WebAssembly standalone with no server prerendering, so curl only ever returns the static `index.html` shell — it can never show the client-rendered tab markup (`nav-tabs`/`tab-pane` will never appear in a curl response, no matter how correct the page is). Don't grep for them; HTTP 200 here only confirms routing/no immediate crash. The real correctness gate for this task is Step 2's exhaustive `@code`-preservation diff check.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "refactor: reorganize FichaDePersonagem.razor into Bootstrap tabs"
```

---

### Task 4: Tab `FichaDeNpc.razor`

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`

**Interfaces:**
- Consumes: `TabControl`/`TabPage` from Task 1.
- No `@code` changes.

`FichaDeNpc.razor`'s markup (lines 1–299 of the current file, reproduced in full in the spec's "why tabs map cleanly" research) is nearly identical to `FichaDePersonagem.razor`'s Informações Básicas / Atributos & Perícias / Combate / Magias & Habilidades / Posses tabs — same field names on `_form`, same `<h3>` sub-sections — with two differences: **no Diário section** (so this page gets 5 tabs, not 6) and **no "Habilidade Racial" `<h3>`** is present either way (it *is* present here, same as Personagem — only Criatura lacks it, see Task 5).

- [ ] **Step 1: Apply the same conversion Task 3 applied to `FichaDePersonagem.razor`, to this file's five sections**

Read the current file in full. Wrap `<h2>Informações Básicas</h2>` through the end of the `<h2>Posses</h2>` section's last `</ul>` in a `<TabControl>` with five `<TabPage>`s titled exactly: Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses. For "Informações Básicas", mirror Task 3's Step 1 worked example field-for-field (the field list is identical: Nome, Linhagem, Variante, Vocação, Sub-vocação, Afinidade, Propriedade, Nível, Possui Coração de Mana, Experiência Atual, EAP Atual, Pontos de Ignição Atual/Total, the Rank F–S row, and the Recursos input-groups — all present here with the same `_form.*` property names). For the other four tabs, apply the field/table/button conventions from the Global Constraints and from `CampanhaDetalhe.razor`/`FichaDePersonagem.razor`'s already-converted equivalents.

Keep `<h1>Ficha de NPC</h1>` and the `@if (_errorMessage...)` block above the `<TabControl>`, unchanged.

- [ ] **Step 2: Verify no `@code` drift** (same method as Task 2/3 Step 2)

- [ ] **Step 3: Build and smoke-test**

```bash
dotnet build
dotnet run --project src/RuinaRPG.Client --urls http://127.0.0.1:5299 &
sleep 8
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5299/npcs/test-id
kill %1
```
Expected: build 0/0; HTTP 200. **Note (found during Task 2):** `RuinaRPG.Client` is Blazor WebAssembly standalone with no server prerendering — curl only ever returns the static `index.html` shell, never client-rendered markup. Don't grep for `nav-tabs`/`tab-pane`. HTTP 200 here only confirms routing/no immediate crash; the real correctness gate is Step 2's exhaustive `@code`-preservation diff check.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "refactor: reorganize FichaDeNpc.razor into Bootstrap tabs"
```

---

### Task 5: Tab `FichaDeCriatura.razor`

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor`

**Interfaces:**
- Consumes: `TabControl`/`TabPage` from Task 1.
- No `@code` changes.

This page's fields differ more from Personagem/NPC than NPC differs from Personagem: Informações Básicas has Nome/Raça/Arquétipo/Sub Arquétipo/Afinidade/Rank (no Linhagem/Vocação/Sub-vocação/Propriedade); Nível e Progressão has Nível/Experiência Atual/Kill/Assistência/Pontos de Ignição (a single field, not current+total); Recursos has only 3 resources (Adrenalina, Arcana — labeled "Arcana (PF)" not "Foco (PF)", Vitalidade — no Estresse); there's no Âmbares Absorvidos block at all (no Rank F–S fields); Combate's Armas tab has an `EditForm` with a conditional Origem selector (Catalogo vs Manual) instead of a plain list; Magias & Habilidades has no "Habilidade Racial" `<h3>`; Posses' first sub-section is "Espólios" (not "Inventário", with an extra DT field).

- [ ] **Step 1: Apply the same conversion approach as Tasks 3–4, adapted to this page's actual field list**

Read the current file in full (reproduced in the spec's research section for reference, but the file is the source of truth). Wrap `<h2>Informações Básicas</h2>` through the end of `<h2>Posses</h2>` in a `<TabControl>` with five `<TabPage>`s titled exactly: Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses.

For "Informações Básicas": row-group Nome/Raça/Arquétipo/Sub Arquétipo/Afinidade/Rank (6 fields, `col-md-2` each fits a row of 6), then a "Nível e Progressão" `<h3>` row with Nível/Experiência Atual (editable) and Kill/Assistência (read-only `<p>`s, keep as `<p>`, don't force into the input row) and Pontos de Ignição, then a "Recursos" `<h3>` row with the 3 input-groups (Adrenalina (PA), Arcana (PF), Vitalidade — keep the "Arcana (PF)" label exactly, don't rename it to "Foco" for consistency with the other pages; it's a deliberate label difference in the source, not a bug for this task to fix).

For Combate's Armas sub-section: keep the `EditForm`'s conditional `@if (_weaponForm.Origem == "Catalogo") { ... } else { ... }` structure exactly, just apply field-wrapper conventions to each `<label>` inside both branches, and `Origem` select → `class="form-select"` in a `<div class="mb-3">`. The weapon list's two buttons (Equipar/Desequipar, Remover) → `btn btn-outline-primary btn-sm` and `btn btn-outline-danger btn-sm` respectively.

For Posses' Espólios sub-section: same conversion as `FichaDePersonagem.razor`/`FichaDeNpc.razor`'s Inventário, just with the extra DT field included in the form and the list's interpolated text.

Keep `<h1>Ficha de Criatura</h1>` and the `@if (_errorMessage...)` block above the `<TabControl>`, unchanged.

- [ ] **Step 2: Verify no `@code` drift** (same method as prior tasks)

- [ ] **Step 3: Build and smoke-test**

```bash
dotnet build
dotnet run --project src/RuinaRPG.Client --urls http://127.0.0.1:5299 &
sleep 8
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5299/criaturas/test-id
kill %1
```
Expected: build 0/0; HTTP 200. **Note (found during Task 2):** `RuinaRPG.Client` is Blazor WebAssembly standalone with no server prerendering — curl only ever returns the static `index.html` shell, never client-rendered markup. Don't grep for `nav-tabs`/`tab-pane`. HTTP 200 here only confirms routing/no immediate crash; the real correctness gate is Step 2's exhaustive `@code`-preservation diff check.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeCriatura.razor
git commit -m "refactor: reorganize FichaDeCriatura.razor into Bootstrap tabs"
```

---

### Task 6: Tab `MinhaCampanha.razor`

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/MinhaCampanha.razor`

**Interfaces:**
- Consumes: `TabControl`/`TabPage` from Task 1.
- No `@code` changes — this file's `@code` block (lines 45–65 of the current file) stays byte-identical.

The full current file (65 lines) is small; the whole markup body is three `<h2>`-delimited, read-only `<ul>` sections with no forms.

- [ ] **Step 1: Wrap the three sections**

Replace everything between the `@if (_errorMessage...)` block and the `@code` block with:

```razor
<TabControl>
    <TabPage Title="Minhas Fichas">
        <ul class="list-unstyled">
            @foreach (var ficha in _view.MinhasFichas)
            {
                <li><a href="@($"fichas/{ficha.Id}")">@(ficha.Nome ?? "(sem nome)")</a> — Nível @ficha.Nivel</li>
            }
        </ul>
    </TabPage>

    <TabPage Title="Meus Companheiros">
        <ul class="list-unstyled">
            @foreach (var companheiro in _view.MeusCompanheiros)
            {
                <li>
                    @(companheiro.Nome ?? "(sem nome)")
                    (@(companheiro.Tipo == "Npc" ? "NPC" : "Criatura"))
                </li>
            }
        </ul>
    </TabPage>

    <TabPage Title="Anexos Públicos">
        <ul class="list-unstyled">
            @foreach (var anexo in _view.AnexosPublicos)
            {
                <li>
                    [@anexo.Tipo] @(anexo.Nome ?? "(sem nome público)")
                    @if (anexo.ImageUrl is not null)
                    {
                        <img src="@anexo.ImageUrl" alt="@anexo.Nome" style="max-width: 120px;" />
                    }
                </li>
            }
        </ul>
    </TabPage>
</TabControl>
```

Keep `<h1>Minha Campanha</h1>` and the `@if (_errorMessage...)` block exactly where they are today, above this `<TabControl>`.

- [ ] **Step 2: Verify no `@code` drift** (same method as prior tasks — `_view`, `_errorMessage`, `LoadAsync` must be untouched)

- [ ] **Step 3: Build and smoke-test**

```bash
dotnet build
dotnet run --project src/RuinaRPG.Client --urls http://127.0.0.1:5299 &
sleep 8
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5299/campanhas/test-id/jogador
kill %1
```
Expected: build 0/0; HTTP 200. **Note (found during Task 2):** `RuinaRPG.Client` is Blazor WebAssembly standalone with no server prerendering — curl only ever returns the static `index.html` shell, never client-rendered markup. Don't grep for `nav-tabs`/`tab-pane`. HTTP 200 here only confirms routing/no immediate crash; the real correctness gate is Step 2's exhaustive `@code`-preservation diff check.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/MinhaCampanha.razor
git commit -m "refactor: reorganize MinhaCampanha.razor into Bootstrap tabs"
```
