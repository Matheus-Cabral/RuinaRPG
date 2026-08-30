# Ficha de Personagem — Informações Básicas, Atributos & Perícias, Combate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`'s markup for its first 3 tabs (Informações Básicas, Atributos & Perícias, Combate) onto the MudBlazor foundation built in the "Fundação" plan, without changing any existing `@code` business logic — this is a UI-layer transcription of already-correct code onto new components, not new functionality.

**Architecture:** This is a **rewrite, not a port from scratch** — the file's `@code` block (state fields, `Load*Async`/`Add*Async`/`Update*Async`/`Delete*Async` methods, nested form-model classes) is already correct, already talks to the right endpoints, and does not change in this plan except where a MudBlazor component's binding shape genuinely differs from the raw HTML element it replaces (documented per-case below, never silently). Every task instead transforms *markup*: raw `<input>`/`<select>`/`<table>`/`<ul>` → the MudBlazor components and shared Fundação components (`Section`, `EntityPicker`, `LinhagemVarianteFields`, `VocacaoSubVocacaoFields`, `AfinidadeSelect`) already built and tested.

**Tech Stack:** Blazor WebAssembly (.NET 8), MudBlazor 9.9.0, the Fundação plan's shared components (unchanged, consumed as-is).

**Spec:** `docs/superpowers/specs/2026-08-30-frontend-rebuild-fundacao-fichas-design.md` — its Fase 1 section is the authority for scope (behavior preserved 1:1, no new audit against Requisitos needed since the backend/functional gap-audit already closed everything — see the spec's "Importante — o backend já está correto" note).

## Global Constraints

- No color/hex outside the existing `--rr-*` tokens; no font outside Cormorant Garamond/Inter (unchanged from Fundação — nothing in this plan introduces new CSS).
- `dotnet build` must stay at 0 warnings / 0 errors after every task.
- **No `@code` method's logic, signature, or the request/response DTOs it calls change in this plan.** Every `Http.GetAsync`/`PostAsJsonAsync`/`PutAsJsonAsync`/`DeleteAsync` call, every `Load*Async`/reload-after-mutate pattern, and every nested `*FormModel` class stays exactly as it is today — only which UI component renders each field changes. If a component's call site needs a small adapter (see the `object? value` note below), that adapter goes at the call site, not inside the method it calls.
- All 3 tasks in this plan edit the same file (`FichaDePersonagem.razor`) in **non-overlapping line ranges, in order** — Task 1 finishes the page shell + Tab 1 before Task 2 starts Tab 2, and so on. This is intentional, not a conflict: dispatch tasks strictly sequentially (never in parallel) within one worktree.
- `EntityPicker` usage (`@bind-Value`/`Value`+`ValueChanged`, `SearchItems`, `Placeholder` parameters) is unchanged from how the file already uses it — Fundação already rebuilt its internals on `MudAutocomplete`; nothing here touches it further.

### Conversion rules (apply throughout; do not restate per task)

| Old | New |
|---|---|
| `<InputText class="form-control" @bind-Value="X" />` | `<MudTextField T="string" @bind-Value="X" Label="..." />` (Label = the `<label>` text that preceded it; drop the old `<label>`) |
| `<InputTextArea class="form-control" @bind-Value="X" />` | `<MudTextField T="string" @bind-Value="X" Label="..." Lines="3" />` |
| `<InputNumber class="form-control" @bind-Value="X" />` (int, two-way bound to a form model field) | `<MudNumericField T="int" @bind-Value="X" Label="..." />` |
| `<InputCheckbox class="form-check-input" @bind-Value="X" />` + a separate `<label class="form-check-label">` | `<MudCheckBox T="bool" @bind-Value="X" Label="..." />` (label folds into the component) |
| `<select class="form-select" @bind="X">` with `<option>`s | `<MudSelect T="string" @bind-Value="X" Label="...">` with `<MudSelectItem Value="@("...")">...</MudSelectItem>` per option |
| A raw `<input>`/`<select>` bound via `value="@item.Field" @onchange="@(e => Method(..., e.Value))"` (one-way, list-item-scoped, not a form model) | `<MudNumericField T="int" Value="@item.Field" ValueChanged="@(v => Method(..., v))" />` (or `MudCheckBox`/`MudSelect` analogously) — `v` arrives already typed (`int`/`bool`/`string`), not `object?`; passing it into a method whose parameter is `object? value` is a normal implicit boxing/widening conversion, so the method itself needs no change |
| `<button type="submit" class="btn btn-primary">Label</button>` | `<MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">Label</MudButton>` |
| `<button ... class="btn btn-outline-primary btn-sm" @onclick="...">Label</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="...">Label</MudButton>` |
| `<button ... class="btn btn-outline-danger btn-sm" @onclick="...">Remover</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="...">Remover</MudButton>` |
| `<h3>Title</h3>` grouping a block, or `<div class="row g-3">...</div>` grouping related fields | wrap the block in `<Section Title="Title">...</Section>` (Fundação component — every `<h3>` in this file becomes a `Section` boundary; a `<div class="row g-3">` with no preceding `<h3>` gets a `Section` too if it's a distinct field group, per the per-task breakdown below) |
| `<table class="table">...<tr><td><input .../></td></tr>...` or `<ul class="list-unstyled"><li>...` (a list of items with per-row actions) | `<MudSimpleTable Dense="true" Hover="true">` with the same `@foreach`, one `<td>` per displayed field plus a final `<td>` for action buttons — `MudSimpleTable` is a lightweight, unvirtualized table wrapper that needs no `Items=`/`RowTemplate=` machinery, so the existing `@foreach` body moves in with minimal change |
| Linhagem + Variante fields | `<LinhagemVarianteFields @bind-Linhagem="_form.Linhagem" @bind-Variante="_form.Variante" />` (deletes ~25 lines of hardcoded `<select>` + the file's own now-redundant `VariantesPorLinhagem` dictionary — see Task 1 Step 3) |
| Vocação + Sub-vocação fields | `<VocacaoSubVocacaoFields @bind-Vocacao="_form.Vocacao" @bind-SubVocacao="_form.SubVocacao" />` (deletes the file's own now-redundant `SubVocacoesPorVocacao` dictionary — see Task 1 Step 3) |
| Afinidade field | `<AfinidadeSelect @bind-Value="_form.Afinidade" />` |
| Page-level `@if (_errorMessage is not null) { <p class="error">@_errorMessage</p> }` | `<MudAlert Severity="Severity.Error" Class="mb-3">@_errorMessage</MudAlert>` inside the same `@if` |
| Page-level `<div class="level-up-notice">...</div>` | `<MudAlert Severity="Severity.Success" Class="mb-3">` wrapping the same heading/list/dismiss-button content, `Dismiss` button becomes a `MudButton` per the button rules above |
| `<TabControl>` / `<TabPage Title="...">` | `<MudTabs>` / `<MudTabPanel Text="...">` — same nesting, same tab order |

Every new/rewritten section of this file needs `@using MudBlazor` — it's already present at the top of `FichaDePersonagem.razor` today? **Check `head -5 src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` before Task 1's first step** — if it's missing, Task 1 adds it (this file, unlike the Fundação components, has no risk of colliding with `RuinaRPG.Client.Shared.BreadcrumbItem`, since it never references `BreadcrumbItem` by its bare name — it constructs `Breadcrumbs`' `Items` via the shared `BreadcrumbItem` type through its own `Crumbs` property, which already exists and is untouched by this plan).

---

## Task 1: Page shell + "Informações Básicas" tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (lines 1–223 of the current file: everything from the top through the end of the `Informações Básicas` `TabPage`, plus the `<TabControl>` opening tag; leave `Atributos & Perícias` onward completely untouched — Tasks 2/3 own those)

**Interfaces:**
- Consumes: `LinhagemVarianteFields`, `VocacaoSubVocacaoFields`, `AfinidadeSelect` (Fundação, `Shared/Fields/`), `Section` (Fundação, `Shared/`), all with the exact public APIs already built and tested there — do not modify any of them.
- Produces: nothing new — this task only changes markup inside an existing page.

- [ ] **Step 1: Read the current file's first 223 lines and its `@code` block's top (state fields + `LoadSheetAsync`/`SaveAsync`)**

Read `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` lines 1–223 (markup) and lines 633–819 (the `@code` block through `SaveAsync`) before writing anything — this task's rewrite must match the real current file, not a stale assumption. Confirm the file has `@using MudBlazor` already (Fundação's other pages needed to add it per-file; this one may not have it yet) — add it right after the existing `@page`/`@inject`/`@using` directives at the top if missing.

- [ ] **Step 2: Rewrite the page shell (error/level-up alerts, breadcrumbs, tab wrapper open)**

Per the conversion table: `<Breadcrumbs Items="@Crumbs" />` and the `<h1>` stay as they are (no MudBlazor component involved). Replace:
```razor
@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}
```
with:
```razor
@if (_errorMessage is not null)
{
    <MudAlert Severity="Severity.Error" Class="mb-3">@_errorMessage</MudAlert>
}
```
Replace the `_levelUpBonuses` block:
```razor
@if (_levelUpBonuses.Count > 0)
{
    <div class="level-up-notice">
        <h2>Novo Nível!</h2>
        <ul>
            @foreach (var bonus in _levelUpBonuses)
            {
                <li>@bonus</li>
            }
        </ul>
        <button type="button" class="btn btn-outline-secondary btn-sm" @onclick="DismissLevelUpAsync">Fechar</button>
    </div>
}
```
with:
```razor
@if (_levelUpBonuses.Count > 0)
{
    <MudAlert Severity="Severity.Success" Class="mb-3">
        <MudText Typo="Typo.h6">Novo Nível!</MudText>
        <ul>
            @foreach (var bonus in _levelUpBonuses)
            {
                <li>@bonus</li>
            }
        </ul>
        <MudButton Variant="Variant.Outlined" Color="Color.Secondary" Size="Size.Small" OnClick="DismissLevelUpAsync">Fechar</MudButton>
    </MudAlert>
}
```
Replace `<TabControl>` with `<MudTabs>` (its closing `</TabControl>` at the very end of the file — line 631 today — is Task 3's responsibility to close out; for this step just open `<MudTabs>` and leave a comment `@* closed in Task 3 *@` if that helps you track it, or just trust the line-range discipline).

- [ ] **Step 3: Rewrite the "Informações Básicas" `TabPage` → `MudTabPanel`**

Replace `<TabPage Title="Informações Básicas">` with `<MudTabPanel Text="Informações Básicas">`. Keep the existing `<EditForm Model="_form" OnValidSubmit="SaveAsync">` wrapper exactly (MudBlazor form components are fully `EditForm`-aware — no change needed to make them cooperate with it).

Group the existing fields into 4 `Section`s, matching the file's own existing `<h3>`/blank-line groupings (Identidade has no `<h3>` today — the file just starts the fields directly after the `EditForm` opens; give it one for consistency with the other 3 groups, matching the spec's §7 naming: "Identidade"):

**Section "Identidade"** — replace the current Nome/Linhagem/Variante/Vocação/Sub-vocação/Afinidade/Propriedade block (today's `col-md-3` grid of raw `<select>`s and one `<InputText>`) with:
```razor
<Section Title="Identidade">
    <MudTextField T="string" @bind-Value="_form.Nome" Label="Nome" />
    <LinhagemVarianteFields @bind-Linhagem="_form.Linhagem" @bind-Variante="_form.Variante" />
    <VocacaoSubVocacaoFields @bind-Vocacao="_form.Vocacao" @bind-SubVocacao="_form.SubVocacao" />
    <AfinidadeSelect @bind-Value="_form.Afinidade" />
    <MudTextField T="string" @bind-Value="_form.Propriedade" Label="Propriedade" />
</Section>
```
This replaces the file's own ~90-line hardcoded Linhagem/Variante/Vocação/Sub-vocação/Afinidade block (today's lines ~38–119) with the 4 lines above — delete the file's own `VariantesPorLinhagem` and `SubVocacoesPorVocacao` dictionaries too (today's lines 638–655 in `@code`), since `LinhagemVarianteFields`/`VocacaoSubVocacaoFields` now own that data (confirm nothing else in this file's `@code` still reads those two dictionaries before deleting them — a quick grep for `VariantesPorLinhagem`/`SubVocacoesPorVocacao` across the file should show zero other uses once Section "Identidade" is rewritten, since the only other historical reader was the markup you just replaced).

**Section "Nível e Progressão"** — the `Nível`/`Graduação` (read-only display)/`Coração de Mana` checkbox/`Experiência Atual`/`EAP Atual` (read-only display)/`Pontos de Ignição Atual`/`Pontos de Ignição Total` block (today's lines 125–155):
```razor
<Section Title="Nível e Progressão">
    <MudNumericField T="int" @bind-Value="_form.Nivel" Label="Nível" />
    <MudText>@_form.GraduacaoLabel: @_form.Graduacao</MudText>
    <MudCheckBox T="bool" @bind-Value="_form.PossuiCoracaoDeMana" Label="Possui Coração de Mana?" />
    <MudNumericField T="int" @bind-Value="_form.ExperienciaAtual" Label="Experiência Atual" />
    <MudText>EAP Atual: @_form.EAPAtual</MudText>
    <MudNumericField T="int" @bind-Value="_form.PontosDeIgnicaoAtual" Label="Pontos de Ignição Atual" />
    <MudNumericField T="int" @bind-Value="_form.PontosDeIgnicaoTotal" Label="Pontos de Ignição Total" />
</Section>
```
(`Graduação`/`EAP Atual` are computed/read-only per the file's own existing comments elsewhere in this codebase — they were already plain `<p>` text, not inputs, in the current file; keep them that way as `MudText`, don't turn them into editable fields.)

**Section "Âmbares Absorvidos"** — the 7 `NucleosRank{F,E,D,C,B,A,S}` fields (today's lines 157–187):
```razor
<Section Title="Âmbares Absorvidos">
    <MudNumericField T="int" @bind-Value="_form.NucleosRankF" Label="Rank F" />
    <MudNumericField T="int" @bind-Value="_form.NucleosRankE" Label="Rank E" />
    <MudNumericField T="int" @bind-Value="_form.NucleosRankD" Label="Rank D" />
    <MudNumericField T="int" @bind-Value="_form.NucleosRankC" Label="Rank C" />
    <MudNumericField T="int" @bind-Value="_form.NucleosRankB" Label="Rank B" />
    <MudNumericField T="int" @bind-Value="_form.NucleosRankA" Label="Rank A" />
    <MudNumericField T="int" @bind-Value="_form.NucleosRankS" Label="Rank S" />
</Section>
```

**Section "Recursos"** — PA/PF/Estresse/Vitalidade, each atual + a read-only máximo (today's lines 189–219, `input-group` pairs):
```razor
<Section Title="Recursos">
    <MudNumericField T="int" @bind-Value="_form.AdrenalinaAtual" Label="Adrenalina (PA)" Adornment="Adornment.End" AdornmentText="@($"/ {_form.AdrenalinaMaximo}")" />
    <MudNumericField T="int" @bind-Value="_form.FocoAtual" Label="Foco (PF)" Adornment="Adornment.End" AdornmentText="@($"/ {_form.FocoMaximo}")" />
    <MudNumericField T="int" @bind-Value="_form.EstresseAtual" Label="Estresse" Adornment="Adornment.End" AdornmentText="@($"/ {_form.EstresseMaximo}")" />
    <MudNumericField T="int" @bind-Value="_form.VitalidadeAtual" Label="Vitalidade" Adornment="Adornment.End" AdornmentText="@($"/ {_form.VitalidadeMaximo}")" />
</Section>
```
(`Adornment`/`AdornmentText` is MudBlazor's built-in way to show a suffix like "/ 40" inside the field — replaces the old `input-group` + `<span class="input-group-text">` pairing with one component instead of two elements.)

Close with the submit button, replacing:
```razor
<button type="submit" class="btn btn-primary mt-3">Salvar</button>
```
with:
```razor
<MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Salvar</MudButton>
```
then close `</EditForm>` and `</MudTabPanel>` exactly where `</EditForm>` and `</TabPage>` closed before.

- [ ] **Step 4: Build**

Run: `dotnet build RuinaRPG.sln`
Run it and report the actual result rather than assuming either outcome: `Atributos & Perícias` onward still uses the old `<TabPage>`/`<TabControl>` tags below this task's edit, and `<MudTabs>` (opened in Step 2) isn't closed until Task 3. Whether that produces a build ERROR (if `MudTabs` type-constrains its children to `MudTabPanel`) or a build that succeeds but wouldn't render/switch tabs correctly at runtime (if it doesn't) isn't something to guess at — run the build and record what actually happens. **Either way, a non-clean or behaviorally-incomplete result at this intermediate point is expected, not a Task 1 defect** — the plan's "build after every task" bar is met at Task 3, once the shell is fully closed. State the actual build outcome in your report so the task reviewer doesn't misjudge it.

- [ ] **Step 5: Verify Task 1's own edit is syntactically self-consistent**

Since a full build isn't meaningful until Task 3 finishes the shell, verify this task's own contribution directly: confirm every `<Section>`/`<MudTabPanel>`/`<EditForm>` tag you opened in lines 1–223 has a matching close, and that `<MudTabs>` (opened, not yet closed) is the only deliberately-unclosed tag in your diff. Read back your own edit once, end to end, before committing.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: rewrite Ficha de Personagem shell + Informações Básicas tab on MudBlazor"
```

---

## Task 2: "Atributos & Perícias" tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (the `Atributos & Perícias` `TabPage`/`MudTabPanel` only — today's lines 225–273; do not touch anything before or after this range)

**Interfaces:**
- Consumes: nothing new from other tasks.
- Produces: nothing new — markup-only change.

- [ ] **Step 1: Read the current tab's markup and its `@code` methods**

Read the current `Atributos & Perícias` section (today's lines 225–273) and its backing `@code` methods `UpdateAttributeAsync`/`UpdateSkillAsync`/`DeleteAffinityAsync` (today's lines 847–873) — confirm they still match this description before transcribing (Task 1 may have shifted line numbers slightly by the time this task runs; use the method names to locate them, not the line numbers, which are this plan's reference point only).

- [ ] **Step 2: Rewrite the tab**

Replace `<TabPage Title="Atributos & Perícias">` with `<MudTabPanel Text="Atributos & Perícias">`.

The budget line:
```razor
@if (_attributeBudget is not null)
{
    <p class="@(_attributeBudget.GastoTotal > _attributeBudget.PontosDisponiveis ? "text-danger" : "")">
        Pontos de Atributo: @_attributeBudget.GastoTotal / @_attributeBudget.PontosDisponiveis
    </p>
}
```
becomes:
```razor
@if (_attributeBudget is not null)
{
    <MudText Color="@(_attributeBudget.GastoTotal > _attributeBudget.PontosDisponiveis ? Color.Error : Color.Default)">
        Pontos de Atributo: @_attributeBudget.GastoTotal / @_attributeBudget.PontosDisponiveis
    </MudText>
}
```

The attributes table (today's `<table class="table">` with 5 columns) becomes a `MudSimpleTable`, same `@foreach`, same `attr.Atributo`/`attr.Total` display cells, replacing only the 3 editable cells' input elements:
```razor
<MudSimpleTable Dense="true" Hover="true">
    <thead><tr><th>Atributo</th><th>Gasto</th><th>Bônus</th><th>Maestria</th><th>Total</th></tr></thead>
    <tbody>
        @foreach (var attr in _attributes)
        {
            <tr>
                <td>@attr.Atributo</td>
                <td><MudNumericField T="int" Value="@attr.Gasto" ValueChanged="@(v => UpdateAttributeAsync(attr.Atributo, "Gasto", v))" Dense="true" /></td>
                <td><MudNumericField T="int" Value="@attr.Bonus" ValueChanged="@(v => UpdateAttributeAsync(attr.Atributo, "Bonus", v))" Dense="true" /></td>
                <td><MudCheckBox T="bool" Value="@attr.TemMaestria" ValueChanged="@(v => UpdateAttributeAsync(attr.Atributo, "TemMaestria", v))" Dense="true" /></td>
                <td>@attr.Total</td>
            </tr>
        }
    </tbody>
</MudSimpleTable>
```
(`UpdateAttributeAsync(string atributo, string field, object? value)` is unchanged — `v` above is `int`/`int`/`bool` respectively per component, each a valid implicit `object?` argument.)

The skills table, same treatment:
```razor
<MudSimpleTable Dense="true" Hover="true">
    <thead><tr><th>Perícia</th><th>Gasto</th><th>Modificador</th></tr></thead>
    <tbody>
        @foreach (var skill in _skills)
        {
            <tr>
                <td>@skill.Pericia</td>
                <td><MudNumericField T="int" Value="@skill.Gasto" ValueChanged="@(v => UpdateSkillAsync(skill.Pericia, v))" Dense="true" /></td>
                <td>@skill.Modificador</td>
            </tr>
        }
    </tbody>
</MudSimpleTable>
```

Affinities, wrapped in a `Section` (the file has an `<h3>Afinidades</h3>` today marking this as its own group) and converted from `<ul><li>` to a `MudSimpleTable`:
```razor
<Section Title="Afinidades">
    <MudSimpleTable Dense="true" Hover="true">
        <tbody>
            @foreach (var affinity in _affinities)
            {
                <tr>
                    <td>@affinity.Elemento / @affinity.SubElemento — @affinity.CaminhoNome (@affinity.Experiencia)</td>
                    <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteAffinityAsync(affinity.Id))">Remover</MudButton></td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>
</Section>
```

Close `</MudTabPanel>` where `</TabPage>` closed before.

- [ ] **Step 3: Build**

Run: `dotnet build RuinaRPG.sln`
Same caveat as Task 1 Step 4: `<MudTabs>` (opened in Task 1) still isn't closed until Task 3, and everything from `Combate` onward is still the old `<TabPage>`/`<TabControl>` markup, so run the build and report the actual outcome rather than assuming — an error or an incomplete-but-compiling result are both expected here, not a Task 2 defect. Verify this task's own edit is internally tag-balanced (every `Section`/`MudTabPanel`/`MudSimpleTable` opened here is closed here) regardless of what the full-solution build reports.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: rewrite Ficha de Personagem Atributos & Perícias tab on MudBlazor"
```

---

## Task 3: "Combate" tab + close the page shell

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (the `Combate` `TabPage`/`MudTabPanel`, today's lines 275–356, plus the final `</TabControl>` → `</MudTabs>` at the old line 631)

**Interfaces:**
- Consumes: `EntityPicker` (Fundação) — already used by this tab today (weapon/armor/shield pickers), API unchanged.
- Produces: nothing new. **This task is also responsible for closing `<MudTabs>`, opened in Task 1** — after this task, the full file should build cleanly (this plan's 3 tasks together finish the whole page shell; Tabs 4–6 remain old `<TabPage>`/Bootstrap markup for now, which is expected and explicitly out of this plan's scope — see the Fundação spec's "no transition strategy" ruling: an un-migrated tab inside an otherwise-migrated page is the same accepted tradeoff as an un-migrated page, not a defect).

Wait — re-read that carefully before starting: **Tabs 4–6 (`Magias & Habilidades`, `Posses`, `Diário`) still use the literal string `<TabPage Title="...">` tags, but this task closes the file's `<TabControl>` wrapper as `</MudTabs>`.** Since `<TabPage>` and `<MudTabPanel>` are different Blazor components, you cannot close a `<MudTabs>` around unconverted `<TabPage>` children — Blazor's component model requires a `<MudTabs>`'s children to actually be `<MudTabPanel>`s (or compatible child content), not a mix. **Resolve this before writing any code**: either (a) this task also converts Tabs 4–6's *outer* `<TabPage Title="...">`/`</TabPage>` tags to `<MudTabPanel Text="...">`/`</MudTabPanel>` (a 2-line, zero-risk mechanical change per tab, 6 lines total) while leaving every line *inside* those 3 tabs completely untouched (still raw Bootstrap markup — that content's real MudBlazor rewrite is a separate future plan), or (b) escalate to the controller if you find a reason (a) doesn't work once you're looking at the real file. Option (a) is the expected resolution — do it, and say so explicitly in your report as a deliberate small addition beyond this task's Files list, not a silent scope change.

- [ ] **Step 1: Read the current tab's markup and its `@code` methods**

Read the current `Combate` section (find it by content, not line number — Tasks 1–2 may have shifted lines) and its backing `@code` methods: `AddWeaponAsync`/`DeleteWeaponAsync`/`EquipWeaponAsync`/`UpdateWeaponDurabilidadeAsync`, `AssignArmorSlotAsync`/`UpdateArmorSlotDurabilidadeAsync`, `AddShieldAsync`/`EquipShieldAsync`/`DeleteShieldAsync`/`UpdateShieldDurabilidadeAsync`, `UpdateCoberturaAsync`. Also locate the closing `</TabControl>` at the end of the file and the 3 remaining `<TabPage Title="...">` opening tags (`Magias & Habilidades`, `Posses`, `Diário`).

- [ ] **Step 2: Rewrite the "Combate" tab**

Replace `<TabPage Title="Combate">` with `<MudTabPanel Text="Combate">`.

**Section "Armas"**:
```razor
<Section Title="Armas">
    <EditForm Model="_weaponForm" OnValidSubmit="AddWeaponAsync">
        <EntityPicker @bind-Value="_weaponForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Arma"))" Placeholder="Buscar arma..." />
        <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">Adicionar Arma</MudButton>
    </EditForm>
    <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
        <tbody>
            @foreach (var weapon in _weapons)
            {
                <tr>
                    <td>@weapon.Nome</td>
                    <td>
                        Durabilidade
                        <MudNumericField T="int" Value="@weapon.DurabilidadeAtual" ValueChanged="@(v => UpdateWeaponDurabilidadeAsync(weapon.Id, v))" Dense="true" Style="width:5em; display:inline-flex;" />
                        / @weapon.DurabilidadeMaxima
                    </td>
                    <td>@(weapon.IsEquipped ? "Equipada" : "")</td>
                    <td>
                        <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="@(() => EquipWeaponAsync(weapon.Id, !weapon.IsEquipped))">@(weapon.IsEquipped ? "Desequipar" : "Equipar")</MudButton>
                        <MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteWeaponAsync(weapon.Id))">Remover</MudButton>
                    </td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>
</Section>
```

**Section "Armaduras"** — the 3 fixed slots (Capacete/Superior/Inferior), each either showing an assigned item + durability input, or an `EntityPicker` + "Atribuir" button:
```razor
<Section Title="Armaduras">
    <MudSimpleTable Dense="true" Hover="true">
        <tbody>
            @foreach (var slot in _armorSlots)
            {
                <tr>
                    <td>@slot.Slot: @(slot.Nome ?? "—") @(slot.DurabilidadeAtual is not null ? $"({slot.DurabilidadeAtual}/{slot.DurabilidadeMaxima})" : "")</td>
                    <td>
                        @if (slot.ItemId is not null)
                        {
                            <MudNumericField T="int?" Value="@slot.DurabilidadeAtual" ValueChanged="@(v => UpdateArmorSlotDurabilidadeAsync(slot.Slot, v))" Dense="true" Style="width:5em; display:inline-flex;" />
                            <MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => AssignArmorSlotAsync(slot.Slot, null))">Remover</MudButton>
                        }
                        else
                        {
                            <span style="display: inline-block; width: 16em; vertical-align: middle;">
                                <EntityPicker Value="@_armorSlotForm[slot.Slot]" ValueChanged="@(v => _armorSlotForm[slot.Slot] = v ?? "")" SearchItems="@(q => SearchItemsByTipoAsync(q, "Armadura"))" Placeholder="Buscar armadura..." />
                            </span>
                            <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="@(() => AssignArmorSlotAsync(slot.Slot, _armorSlotForm.GetValueOrDefault(slot.Slot)))">Atribuir</MudButton>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>
</Section>
```

**Section "Escudos"** — same pattern as Armas:
```razor
<Section Title="Escudos">
    <EditForm Model="_shieldForm" OnValidSubmit="AddShieldAsync">
        <EntityPicker @bind-Value="_shieldForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Escudo"))" Placeholder="Buscar escudo..." />
        <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">Adicionar Escudo</MudButton>
    </EditForm>
    <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
        <tbody>
            @foreach (var shield in _shields)
            {
                <tr>
                    <td>@shield.Nome</td>
                    <td>
                        Durabilidade
                        <MudNumericField T="int" Value="@shield.DurabilidadeAtual" ValueChanged="@(v => UpdateShieldDurabilidadeAsync(shield.Id, v))" Dense="true" Style="width:5em; display:inline-flex;" />
                        / @shield.DurabilidadeMaxima
                    </td>
                    <td>@(shield.IsEquipped ? "Equipado" : "")</td>
                    <td>
                        <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="@(() => EquipShieldAsync(shield.Id, !shield.IsEquipped))">@(shield.IsEquipped ? "Desequipar" : "Equipar")</MudButton>
                        <MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteShieldAsync(shield.Id))">Remover</MudButton>
                    </td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>
</Section>
```

**Section "Sub-Atributos"** (only rendered `@if (_subAttributes is not null)`, unchanged condition):
```razor
@if (_subAttributes is not null)
{
    <Section Title="Sub-Atributos">
        <MudText>Iniciativa: @_subAttributes.Iniciativa</MudText>
        <MudText>Movimentação: @_subAttributes.Movimentacao</MudText>
        <MudText>Esquiva Natural: @_subAttributes.EsquivaNatural</MudText>
        <MudText>Defesa Natural: @_subAttributes.DefesaNatural</MudText>
        <MudText>Redução Física: @_subAttributes.ReducaoFisica</MudText>
        <MudText>Redução Mágica: @_subAttributes.ReducaoMagica</MudText>
        <MudSelect T="string" Value="@_form.Cobertura" ValueChanged="@(v => UpdateCoberturaAsync(v))" Label="Cobertura">
            <MudSelectItem Value="@("Nenhuma")">Nenhuma (+0)</MudSelectItem>
            <MudSelectItem Value="@("Parcial")">Parcial (+5)</MudSelectItem>
            <MudSelectItem Value="@("Completa")">Completa (+10)</MudSelectItem>
        </MudSelect>
    </Section>
}
```

Close `</MudTabPanel>`.

- [ ] **Step 3: Convert Tabs 4–6's outer tags only (per the note above the task's step list)**

For each of the 3 remaining tabs, change only the opening/closing tag pair — nothing inside:
- `<TabPage Title="Magias & Habilidades">` → `<MudTabPanel Text="Magias & Habilidades">`, its matching `</TabPage>` → `</MudTabPanel>`
- `<TabPage Title="Posses">` → `<MudTabPanel Text="Posses">`, its matching `</TabPage>` → `</MudTabPanel>`
- `<TabPage Title="Diário">` → `<MudTabPanel Text="Diário">`, its matching `</TabPage>` → `</MudTabPanel>`

Everything between each pair (the `Section`/`EditForm`/`<ul>` Bootstrap markup already in the file) stays byte-for-byte identical — a future plan rewrites those 3 tabs' insides.

- [ ] **Step 4: Close the page shell**

Replace the file's final `</TabControl>` with `</MudTabs>`.

- [ ] **Step 5: Full-solution build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors — this is the first point in this plan where a clean build is actually expected, since the shell is now fully closed and every tab (migrated or not) is a real `MudTabPanel`.

- [ ] **Step 6: Manual/serve verification**

```bash
dotnet run --project src/RuinaRPG.Client --urls http://localhost:5299 &
sleep 8
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5299/
kill %1
```
Expected: `200`. As with the Fundação plan, this only proves the static shell serves — client-rendered markup needs a real browser to see (no server prerendering in this Blazor WASM app). If a browser/Playwright tool is available in this environment, load a real character sheet URL, click through all 6 tabs, and confirm: no console errors, Tabs 1–3 render with the new MudBlazor components, Tabs 4–6 still render (with their old Bootstrap look, expected), and saving a change in Tab 1/2/3 round-trips correctly. If no browser tool is available, say so explicitly rather than claiming a visual check you didn't do.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: rewrite Ficha de Personagem Combate tab, close MudTabs shell"
```

---

## After this plan

Tabs 1–3 of `FichaDePersonagem.razor` are on MudBlazor, behavior-identical to before. Tabs 4–6 (`Magias & Habilidades`, `Posses`, `Diário` — the content-heavy tabs: spells/runas/maestrias, inventário/artefatos/afeições/características, diary entries) are a separate future plan (`ficha-de-personagem-magias-posses-diario`, not written yet), reusing the same conversion rules and shared components established here. After both land, `FichaDeNpc.razor`/`FichaDeCriatura.razor` (diffs against this page per their own Requisitos docs) follow as their own plans.
