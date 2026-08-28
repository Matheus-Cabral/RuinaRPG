# Tabbed Multi-Topic Pages — Design

**Status:** Approved by user 2026-08-28 ("pode escrever e executar o plano").

## Goal

The Campanha page and the three Ficha pages (Personagem, NPC, Criatura) each cram several unrelated topics into one long scroll of raw HTML, with almost no visual distinction between actions (every button looks identical after the Round 2 CSS pass). Reorganize each into Bootstrap-style tabs, and while touching that markup, bring forms/tables/buttons onto real Bootstrap conventions so each tab reads as a coherent, usable unit — in service of the app's actual goal: making it easier to play and run the RPG.

This is a markup-only reorganization. No `@code` member is renamed, added, removed, or has its behavior changed by this work — every `@bind`, `@onclick`, `@onchange`, and HTTP call stays wired exactly as it is today, just relocated inside tab wrappers.

## Why tabs map cleanly onto existing structure

Both the requirements and the current code already think in these terms:

- `Docs/Requisitos/Requisitos - Ficha de Personagem.md` **R0001**: *"A ficha deve ser subdividida em abas"* — and the doc's own section numbering (1 through 6) matches the page's current `<h2>` headings exactly.
- `FichaDePersonagem.razor` already has a method literally named `LoadTabs2And3Async`.
- `CampanhaDetalhe.razor`'s six `<h2>` sections (Membros, Diário, Encontros, Anexos, Conceder Ficha, Notas Secretas) are exactly the six the user named as the desired tabs.

So tab boundaries are not a new design decision — they're the existing `<h2>` sections, promoted to tabs.

## Architecture: `TabControl` / `TabPage`

Two new reusable Blazor components in `src/RuinaRPG.Client/Shared/`, state-driven (no JS, no Bootstrap bundle needed — matches the app's existing zero-JS-interop-for-UI pattern). `TabPage` registers itself with its parent `TabControl` via a cascading parameter; `TabControl` renders the Bootstrap nav (`nav nav-tabs`) from its children's `Title`s and shows/hides each `TabPage`'s content by toggling `tab-pane` visibility — all data stays loaded exactly as today (`OnInitializedAsync` still loads everything up front); only display toggles.

**`src/RuinaRPG.Client/Shared/TabControl.razor`:**

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

**`src/RuinaRPG.Client/Shared/TabPage.razor`:**

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

`Register` calls `StateHasChanged()` because `TabControl`'s own `@foreach (var page in _pages)` renders before its children's `OnInitialized` has run (the classic parent-renders-before-child-registers ordering in Blazor) — without it, the nav would be empty on first paint.

**`src/RuinaRPG.Client/Shared/TabControl.razor.css`** (scoped; overrides the Round 2 global `.content button` rule via higher selector specificity — verified: `.tab-control .nav-link[scope]` beats `.content button` since two classes + a scope attribute outrank one class + one element):

```css
.tab-control .nav-tabs {
  border-bottom-color: var(--rr-border);
}

.tab-control ::deep .nav-link {
  color: var(--rr-text-muted);
  border: none;
  border-bottom: 2px solid transparent;
  background: none;
  padding: var(--rr-space-2) var(--rr-space-3);
  font-family: var(--rr-font-body);
  font-weight: 600;
  border-radius: 0;
}

.tab-control ::deep .nav-link:hover {
  color: var(--rr-text);
  border-bottom-color: var(--rr-border);
}

.tab-control ::deep .nav-link.active {
  color: var(--rr-primary);
  border-bottom-color: var(--rr-primary);
  background: none;
}

.tab-control .tab-content {
  padding-top: var(--rr-space-4);
}
```

Note the `::deep` on the `.nav-link` rules: `TabControl.razor`'s own markup renders the `<button class="nav-link">` directly (it's TabControl's own element, not a child component's), so strictly `::deep` is not required there the way it was for `NavMenu`'s `<NavLink>` child-component case — but `.tab-pane`/`.nav-link` classes are also matched from `TabPage.razor`'s own render output in a couple of spots conceptually adjacent to this stylesheet. **Task 1's implementer must verify empirically** (compile and check generated scoped CSS, the same method that caught the Round 1 `NavMenu` bug) whether `::deep` is actually needed here or whether plain scoping suffices, and use whichever is correct — this spec's snippet is a starting point, not gospel.

## Bootstrap conventions for touched forms/tables/buttons

Applied only within the tabs of the five pages this plan touches (Campanha, Personagem, NPC, Criatura, Minha Campanha). Everywhere else, Round 2's bare-element CSS keeps working untouched (real Bootstrap classes have higher specificity, so no conflict).

**Field wrapper** — replace `<label>Campo <InputX .../></label>` with:
```razor
<div class="mb-3">
    <label class="form-label">Campo</label>
    <InputText class="form-control" @bind-Value="_form.Campo" />
</div>
```
`InputNumber`/`InputText`/`InputTextArea` → `class="form-control"`. `<select>`/`InputSelect` → `class="form-select"`. Plain `<input>` (non-Blazor-bound, e.g. `@bind="_memberSearch"`) → `class="form-control"` too.

**Checkboxes:**
```razor
<div class="mb-3 form-check">
    <InputCheckbox class="form-check-input" @bind-Value="_form.PossuiCoracaoDeMana" />
    <label class="form-check-label">Possui Coração de Mana?</label>
</div>
```
Same pattern for the plain `<input type="checkbox">` cases (attachment visibility toggles, note-recipient checklists).

**"Current / Maximum" fields** (Adrenalina, Foco/Arcana, Estresse, Vitalidade — the ones with trailing `/ @_form.XMaximo` text): use an input-group instead of letting the trailing text wrap awkwardly:
```razor
<div class="mb-3">
    <label class="form-label">Adrenalina (PA)</label>
    <div class="input-group">
        <InputNumber class="form-control" @bind-Value="_form.AdrenalinaAtual" />
        <span class="input-group-text">/ @_form.AdrenalinaMaximo</span>
    </div>
</div>
```

**Grouping related fields into a row** — use `<div class="row g-3">` wrapping `<div class="col-md-*">` per field, for these specific clusters (identified because they're already visually/semantically one unit today):
- Identidade block (Nome, Linhagem, Variante, Vocação, Sub-vocação, Afinidade, Propriedade — Personagem/NPC; Nome, Raça, Arquétipo, Sub Arquétipo, Afinidade, Rank — Criatura)
- Nível e Progressão block
- Âmbares Absorvidos (Rank F through Rank S — 7 fields, one compact row, e.g. `col-md-1` or `col`)
- Recursos block (the current/maximum fields above, as input-groups in a row)

Don't invent new groupings beyond these — the goal is "reads as one unit," not a full visual redesign (the user chose the moderate depth option, not the broader redesign one).

**Tables:** `<table>` → `<table class="table">`. No other table markup changes — the `<td><input type="number" ...></td>` editable cells keep the same bare structure (Round 2's `.content td input { max-width: 100px }` still narrows them correctly; adding `.form-control` there is optional polish, not required).

**Buttons — classify by intent, apply consistently:**
| Intent | Example labels | Class |
|---|---|---|
| Primary form submit / create / attach / search | Salvar, Adicionar *, Criar Ficha de Personagem, Conceder, Anexar *, Buscar, Novo Encontro | `btn btn-primary` |
| Inline list-item action (non-destructive) | Editar, Equipar/Desequipar, Adicionar Efeito | `btn btn-outline-primary btn-sm` |
| Destructive | Remover, Excluir | `btn btn-outline-danger btn-sm` |
| Cancel | Cancelar | `btn btn-outline-secondary btn-sm` |

Use `btn-sm` for every button that lives inside a `<li>`/table row (list-context actions); full-size `btn` for the button that submits a `<EditForm>`.

## Per-page tab breakdown

Tab titles below are exactly the current `<h2>` text — do not rename them.

### `CampanhaDetalhe.razor` — 6 tabs
Membros / Diário / Encontros / Anexos / Conceder Ficha / Notas Secretas — each tab body is exactly today's content between one `<h2>` and the next (the `<h2>` itself is dropped since the tab title now carries that label; keep any `<h3>`/`<h4>` inside a tab as-is).

### `FichaDePersonagem.razor` — 6 tabs
Informações Básicas / Atributos & Perícias / Combate / Magias & Habilidades / Posses / Diário — same rule: current `<h2>`-delimited regions become tab bodies, inner `<h3>`/`<h4>` stay.

### `FichaDeNpc.razor` — 5 tabs
Informações Básicas / Atributos & Perícias / Combate / Magias & Habilidades / Posses (no Diário tab — this page has no Diário section today).

### `FichaDeCriatura.razor` — 5 tabs
Same five as NPC (Magias & Habilidades here has no "Habilidade Racial" `<h3>` — NPC/Personagem do, Criatura doesn't; keep that difference, don't add anything).

### `MinhaCampanha.razor` — 3 tabs
Minhas Fichas / Meus Companheiros / Anexos Públicos. This page stays unreachable in the product (no nav link, GM-only `/api/campaigns` blocks a player from discovering their own campaign id — a known gap from a previous round, not addressed by this plan). Tab it anyway per the user's explicit choice, so it's ready once that gap closes.

### Explicitly out of scope (unchanged this round)
`GerenciadorDeEncontros.razor` (single-topic: a list + one add-form, not multiple `<h2>` sections), all list/catalog pages (`NpcsDoGm`, `BestiarioDoGm`, `Catalogo`, `BancoDeMagias`, `Compendio`, `GmConvites`, `GmJogadores`, `Campanhas`), and single-purpose forms (`CatalogoItemForm`, `BancoDeMagiasForm`, `Login`, `Cadastro`). These keep Round 2's bare-element CSS as their only styling.

## Global Constraints

- Zero behavior change: every `@code` member, `@bind`/`@bind-Value` target, `@onclick`/`@onchange` handler, and HTTP call must survive identically — this task only moves markup into tab wrappers and adds Bootstrap classes.
- Tab titles are copied verbatim from the current `<h2>` text (Portuguese, exact casing/accents).
- The `<h1>` page title and the top-level `@if (_errorMessage is not null)` error paragraph stay outside/above the `TabControl` (they're page-level, not tab-specific). `FichaDePersonagem.razor`'s level-up notice block also stays above the `TabControl`, unchanged.
- Button class mapping (table above) applies uniformly — don't improvise a different mapping per page.
- Row/column grouping is limited to the four clusters named above per page (adapted to each page's actual field list) — no broader redesign.
- `dotnet build` must stay at 0 warnings/0 errors after every task.

## Testing / Verification

No Blazor component test infrastructure exists in this repo (established gap from Round 1/2). Verification per task:
1. `dotnet build` clean (0/0).
2. Run `dotnet run --project src/RuinaRPG.Client` standalone and `curl` the served page to confirm the expected tab nav markup and Bootstrap classes are present in the rendered HTML.
3. Manual diff-reading: every `@bind`/`@onclick`/handler name that existed before the task must still appear, unchanged, in the after — this is the primary defense against silently dropping a binding while restructuring hundreds of lines of markup.

No screenshot/headless-browser tool is available in this environment (same limitation noted in Round 2) — the user should spot-check the live result visually, same as before.

## Execution

`docs/superpowers/plans/2026-08-28-tabbed-pages.md`, executed via `superpowers:subagent-driven-development` in an isolated worktree: one task to build `TabControl`/`TabPage`, one task per page (5 pages), one final whole-branch review.
