# Ficha de Criatura — MudBlazor rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` from Bootstrap/`TabControl` onto MudBlazor (all 5 tabs) — the last remaining page on the old stack, closing the entire front-end rebuild for the 3 sheet types. Fix one ledgered "backend wired, UI control missing" gap (Cobertura) and port one already-proven behavioral fix (nullable resource fields silently persisting `0` when cleared).

**Architecture:** Two tasks, both touching the same file sequentially (never in parallel) — mirrors `docs/superpowers/plans/2026-09-02-ficha-de-npc.md`'s already-merged (`3a25b02`) structure exactly. Task 1 opens the `MudTabs` shell and fully migrates Tabs 1–2 (Informações Básicas, Atributos & Perícias), plus the plan's one `@code` behavior fix (nullable `SheetFormModel` fields + `SaveAsync` guard) — Tabs 3–5 are deliberately left untouched, so Task 1's own build is expected to fail (a known, documented intermediate state, not a defect). Task 2 migrates Tabs 3–5 (Combate, Magias & Habilidades, Posses), closes the shell, and adds one new small `@code` method (`UpdateCoberturaAsync`) backing the one newly-added UI control — this task always runs after Task 1, so the guard is already in place before the control exists.

**Tech Stack:** Blazor WebAssembly (.NET 8), MudBlazor 9.9.0, existing shared components (`Section`, `Breadcrumbs`, `EntityPicker`, `AfinidadeSelect`) consumed as-is, no changes to any of them. Unlike NPC, this page does NOT use `LinhagemVarianteFields`/`VocacaoSubVocacaoFields` (Criatura has no Linhagem/Vocação/Sub-vocação fields per its Requisitos diff).

**Spec:** `docs/superpowers/specs/2026-09-02-ficha-de-npc-e-criatura-design.md` — authority for scope. Read it in full, **including the "Correction, from NPC's final whole-branch review" paragraph under point 2 and its matching bullet under "Known pitfalls"** — both were added after the NPC plan's final review found a real (non-blocking, inherited) limitation in the Cobertura guard's user-feedback that this plan's Cobertura control will reproduce identically. Port it exactly as documented there; do not attempt an ad-hoc fix.

## Global Constraints

- No color/hex outside the existing `--rr-*` tokens; no font outside Cormorant Garamond/Inter (nothing in this plan introduces new CSS).
- `dotnet build RuinaRPG.sln` must be 0 warnings / 0 errors after Task 2 (Task 1's own build is a documented exception — see Task 1).
- **No `@code` method's logic, signature, or the request/response DTOs it calls change**, except: (a) `SheetFormModel`'s resource-int fields become nullable + `[Required]` (Task 1), (b) `SaveAsync` gains an up-front null guard (Task 1), (c) one new method, `UpdateCoberturaAsync`, is added (Task 2) — a direct port of Personagem's/NPC's already-shipped method of the same name, not new design. Every `Http.GetAsync`/`PostAsJsonAsync`/`PutAsJsonAsync`/`DeleteAsync` call, every `Load*Async` method, and every nested `*FormModel` class other than `SheetFormModel` stays exactly as it is today.
- Both tasks edit the same file (`FichaDeCriatura.razor`) in non-overlapping regions — Task 1 touches the top `@using` block, Tabs 1–2, `Crumbs`, `SaveAsync`, and `SheetFormModel`; Task 2 touches Tabs 3–5 and inserts one new method right after `SaveAsync`. Dispatch strictly sequentially within one worktree.
- `EntityPicker` usage (`@bind-Value`/`Value`+`ValueChanged`, `SearchItems`, `Placeholder` parameters) is unchanged — nothing in this plan touches its internals.
- `Section`, `Breadcrumbs`, `AfinidadeSelect` are consumed exactly as `FichaDeNpc.razor`/`FichaDePersonagem.razor` already consume them — no changes to any of these components.
- **Property-name traps specific to Criatura's response DTOs — preserve verbatim, do not "fix" or unify:** `CreatureWeaponResponse.DurabilidadeMaximo` (masculine, ends "o") and `CreatureArmorSlotResponse.DurabilidadeMaximo` (masculine) vs. `CreatureShieldResponse.DurabilidadeMaxima` (feminine, ends "a") — an inconsistent-but-real naming split in this codebase's contracts, confirmed by reading the actual `.cs` files, not a typo to correct.

### Conversion rules (apply throughout; identical to the NPC plan's table, reproduced here for a fresh reader)

| Old | New |
|---|---|
| `<InputText class="form-control" @bind-Value="X" />` preceded by a `<label>` | `<MudTextField T="string" @bind-Value="X" Label="..." />` (Label = the `<label>` text; drop the old `<label>`) |
| `<InputNumber class="form-control" @bind-Value="X" />` (int, two-way bound to a brand-new *add*-form field, not a persisted resource) | `<MudNumericField T="int" @bind-Value="X" Label="..." />` — stays non-nullable, no "cleared silently saves 0" risk on a fresh add-form |
| `<InputNumber class="form-control" @bind-Value="X" />` (int, bound to a persisted resource field on `_form`, submitted via `SaveAsync`) | `<MudNumericField T="int?" @bind-Value="X" For="@(() => X)" Label="..." />` — the field must already be `int?` + `[Required]` on `SheetFormModel` per Task 1 |
| `<select class="form-select" @bind="X">` with `<option>`s | `<MudSelect T="string" @bind-Value="X" Label="...">` with one `<MudSelectItem Value="@("...")">...</MudSelectItem>` per option |
| A raw `<input>`/`<select>` bound via `value="@item.Field" @onchange="@(e => Method(..., e.Value))"` (one-way, live-update, not a form model) | `<MudNumericField T="int?" Value="@item.Field" ValueChanged="@(v => Method(..., v))" />` — `v` arrives already typed, passing it into a method whose parameter is `object? value` is a normal implicit boxing conversion |
| `<button type="submit" class="btn btn-primary">Label</button>` | `<MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">Label</MudButton>` |
| `<button ... class="btn btn-outline-primary btn-sm" @onclick="...">Label</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="...">Label</MudButton>` |
| `<button ... class="btn btn-outline-danger btn-sm" @onclick="...">Remover</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="...">Remover</MudButton>` |
| `<h3>Title</h3>` grouping a whole block | wrap the block in `<Section Title="Title">...</Section>` |
| `<h4>Subtitle</h4>` grouping a sub-block *inside* a `Section` | `<MudText Typo="Typo.h6" Class="mt-3">Subtitle</MudText>` — no nested `Section` |
| `<table class="table">...` or `<ul class="list-unstyled"><li>...` (a list of items with a per-row action button) | `<MudSimpleTable Dense="true" Hover="true">` with a `<tbody>` `@foreach` producing one `<tr>` per item |
| `<TabControl>`/`<TabPage Title="...">` | `<MudTabs>`/`<MudTabPanel Text="...">` |
| Page-level `<p class="error">@_errorMessage</p>` | `<MudAlert Severity="Severity.Error" Class="mb-3">@_errorMessage</MudAlert>` |
| Page `<h1>` | left as bare `<h1>` — matches established precedent on both other sheet pages, not this plan's job either |
| A read-only display field never bound via any input (e.g. `Kill`, `Assistência`) | `<MudText>Label: @_form.Field</MudText>` |

---

## Task 1: Shell open + Informações Básicas + Atributos & Perícias + nullable-int fix

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` — top `@using` block (original lines 1–7), page header/error block (lines 9–16), `<TabControl>` opening tag through the end of the Atributos & Perícias `TabPage` (lines 18–166), `Crumbs` property (lines 509–514), `SaveAsync` (lines 604–618), `SheetFormModel` class (lines 896–916)

**Interfaces:**
- Consumes: `UpdateCreatureSheetRequest` (`src/RuinaRPG.Contracts/CreatureSheets/UpdateCreatureSheetRequest.cs`) — unchanged, still takes non-nullable types for every field. `CreatureSheetResponse` (unchanged).
- Produces: `SheetFormModel`'s resource-int fields (`Nivel`, `ExperienciaAtual`, `PontosDeIgnicao`, `VitalidadeAtual`, `FocoAtual`, `AdrenalinaAtual`) become `int?` — 6 fields total, confirmed against `UpdateCreatureSheetRequest`'s exact parameter list (unlike NPC's 16 — Criatura has no Núcleos de Rank, no split Pontos de Ignição Atual/Total, no Estresse). `Cobertura` (`string`) stays exactly as it is today; Task 2 adds its UI control without touching its type. Task 2 doesn't reference any of the 6 nullable fields (they live entirely in Tab 1, already migrated), so nothing in Task 2 needs updating for this change, but note it for the task reviewer.

- [ ] **Step 1: Read the current state**

Read `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` in full. Confirm the line numbers referenced below still match (this is the first task in this plan, so they should be exact) — if not, adjust, but the content itself is exactly as shown.

- [ ] **Step 2: Rewrite the header, `@using` block, and Tabs 1–2**

Replace lines 1–166 (from `@page "/criaturas/{SheetId}"` through the closing `</TabPage>` of "Atributos & Perícias") with:

```razor
@page "/criaturas/{SheetId}"
@inject HttpClient Http
@using RuinaRPG.Contracts.CreatureSheets
@using RuinaRPG.Contracts.SpellsAndAbilities
@using RuinaRPG.Contracts.CharacterSheets
@using RuinaRPG.Contracts.Items
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Client.Shared.Fields
@using MudBlazor
@using System.ComponentModel.DataAnnotations

<Breadcrumbs Items="@Crumbs" />
<h1>Ficha de Criatura</h1>

@if (_errorMessage is not null)
{
    <MudAlert Severity="Severity.Error" Class="mb-3">@_errorMessage</MudAlert>
}

<MudTabs>
    <MudTabPanel Text="Informações Básicas">
        <EditForm Model="_form" OnValidSubmit="SaveAsync">
            <DataAnnotationsValidator />
            <Section Title="Identidade">
                <MudTextField T="string" @bind-Value="_form.Nome" Label="Nome" />
                <MudTextField T="string" @bind-Value="_form.Raca" Label="Raça" />
                <MudSelect T="string" @bind-Value="_form.Arquetipo" Label="Arquétipo">
                    <MudSelectItem Value="@("")">Escolha um Arquétipo</MudSelectItem>
                    <MudSelectItem Value="@("Fisico")">Físico</MudSelectItem>
                    <MudSelectItem Value="@("Arcano")">Arcano</MudSelectItem>
                </MudSelect>
                <MudTextField T="string" @bind-Value="_form.SubArquetipo" Label="Sub Arquétipo" />
                <AfinidadeSelect @bind-Value="_form.Afinidade" />
                <MudSelect T="string" @bind-Value="_form.Rank" Label="Rank">
                    <MudSelectItem Value="@("")">Escolha um Rank</MudSelectItem>
                    <MudSelectItem Value="@("F")">F</MudSelectItem>
                    <MudSelectItem Value="@("E")">E</MudSelectItem>
                    <MudSelectItem Value="@("D")">D</MudSelectItem>
                    <MudSelectItem Value="@("C")">C</MudSelectItem>
                    <MudSelectItem Value="@("B")">B</MudSelectItem>
                    <MudSelectItem Value="@("A")">A</MudSelectItem>
                    <MudSelectItem Value="@("S")">S</MudSelectItem>
                </MudSelect>
            </Section>

            <Section Title="Nível e Progressão">
                <MudNumericField T="int?" @bind-Value="_form.Nivel" For="@(() => _form.Nivel)" Label="Nível" />
                <MudNumericField T="int?" @bind-Value="_form.ExperienciaAtual" For="@(() => _form.ExperienciaAtual)" Label="Experiência Atual" />
                <MudText>Kill: @_form.Kill</MudText>
                <MudText>Assistência: @_form.Assistencia</MudText>
                <MudNumericField T="int?" @bind-Value="_form.PontosDeIgnicao" For="@(() => _form.PontosDeIgnicao)" Label="Pontos de Ignição" />
            </Section>

            <Section Title="Recursos">
                <MudNumericField T="int?" @bind-Value="_form.AdrenalinaAtual" For="@(() => _form.AdrenalinaAtual)" Label="Adrenalina (PA)" Adornment="Adornment.End" AdornmentText="@($"/ {_form.AdrenalinaMaximo}")" />
                <MudNumericField T="int?" @bind-Value="_form.FocoAtual" For="@(() => _form.FocoAtual)" Label="Arcana (PF)" Adornment="Adornment.End" AdornmentText="@($"/ {_form.FocoMaximo}")" />
                <MudNumericField T="int?" @bind-Value="_form.VitalidadeAtual" For="@(() => _form.VitalidadeAtual)" Label="Vitalidade" Adornment="Adornment.End" AdornmentText="@($"/ {_form.VitalidadeMaximo}")" />
            </Section>

            <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Salvar</MudButton>
        </EditForm>
    </MudTabPanel>

    <MudTabPanel Text="Atributos & Perícias">
        <MudSimpleTable Dense="true" Hover="true">
            <thead><tr><th>Atributo</th><th>Gasto</th><th>Bônus</th><th>Maestria</th><th>Total</th></tr></thead>
            <tbody>
                @foreach (var attr in _attributes)
                {
                    <tr>
                        <td>@attr.Atributo</td>
                        <td><MudNumericField T="int?" Value="@attr.Gasto" ValueChanged="@(v => UpdateAttributeAsync(attr.Atributo, "Gasto", v))" /></td>
                        <td><MudNumericField T="int?" Value="@attr.Bonus" ValueChanged="@(v => UpdateAttributeAsync(attr.Atributo, "Bonus", v))" /></td>
                        <td><MudCheckBox T="bool" Value="@attr.TemMaestria" ValueChanged="@(v => UpdateAttributeAsync(attr.Atributo, "TemMaestria", v))" Dense="true" /></td>
                        <td>@attr.Total</td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>

        <MudSimpleTable Dense="true" Hover="true">
            <thead><tr><th>Perícia</th><th>Gasto</th><th>Modificador</th></tr></thead>
            <tbody>
                @foreach (var skill in _skills)
                {
                    <tr>
                        <td>@skill.Pericia</td>
                        <td><MudNumericField T="int?" Value="@skill.Gasto" ValueChanged="@(v => UpdateSkillAsync(skill.Pericia, v))" /></td>
                        <td>@skill.Modificador</td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>
    </MudTabPanel>
```

Note: unlike NPC, this tab has **no Afinidades Section** here — Criatura has no Afinidades list per its requirements diff, confirmed absent from the current file. Note also: `@using RuinaRPG.Client.Shared.Fields` is needed here even though this page doesn't use `LinhagemVarianteFields`/`VocacaoSubVocacaoFields` — `AfinidadeSelect` lives in that same namespace (confirmed: `src/RuinaRPG.Client/Shared/Fields/AfinidadeSelect.razor`).

The very next line in the file after this block is still `    <TabPage Title="Combate">` — leave it and everything through the original closing `</TabControl>` (original line 504) completely untouched in this task.

- [ ] **Step 3: Fully-qualify `Crumbs`' `BreadcrumbItem` type**

The new `@using MudBlazor` added in Step 2 collides with `MudBlazor.BreadcrumbItem` — same collision hit twice on Personagem and once on NPC. Replace the `Crumbs` property (currently lines 509–514):

```csharp
    private List<BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Bestiário do GM", "bestiario"),
        new(string.IsNullOrWhiteSpace(_form.Nome) ? "Ficha de Criatura" : _form.Nome),
    };
```

with:

```csharp
    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Bestiário do GM", "bestiario"),
        new(string.IsNullOrWhiteSpace(_form.Nome) ? "Ficha de Criatura" : _form.Nome),
    };
```

Do not add a bare `@using RuinaRPG.Client.Shared` anywhere — that would re-trigger the same ambiguity, which is exactly why the fully-qualified form is used here instead.

- [ ] **Step 4: Make the 6 resource fields nullable + required, in `SheetFormModel`**

Replace the class body (currently lines 896–916) with:

```csharp
    private class SheetFormModel
    {
        public string? Nome { get; set; }
        public string? Raca { get; set; }
        public string? Arquetipo { get; set; }
        public string? SubArquetipo { get; set; }
        public string? Afinidade { get; set; }
        public string? Rank { get; set; }
        [Required(ErrorMessage = "Informe o Nível.")]
        public int? Nivel { get; set; } = 1;
        [Required(ErrorMessage = "Informe a Experiência Atual.")]
        public int? ExperienciaAtual { get; set; }
        public int Kill { get; set; }
        public int Assistencia { get; set; }
        [Required(ErrorMessage = "Informe os Pontos de Ignição.")]
        public int? PontosDeIgnicao { get; set; }
        [Required(ErrorMessage = "Informe a Vitalidade.")]
        public int? VitalidadeAtual { get; set; }
        [Required(ErrorMessage = "Informe o Foco.")]
        public int? FocoAtual { get; set; }
        [Required(ErrorMessage = "Informe a Adrenalina.")]
        public int? AdrenalinaAtual { get; set; }
        public string Cobertura { get; set; } = "Nenhuma";
        public int VitalidadeMaximo { get; set; }
        public int FocoMaximo { get; set; }
        public int AdrenalinaMaximo { get; set; }
    }
```

Note what did **not** change: `Kill`/`Assistencia` (read-only display — confirmed by reading `CreatureSheetsController.cs`: they're computed live via `XpAwardCalculator` and never persisted/never part of `UpdateCreatureSheetRequest`, so there's no "cleared silently saves wrong value" risk since they're never bound to any input at all), `Cobertura` (string, its own `MudSelect` added in Task 2), the 3 `*Maximo` fields (read-only, rendered as `Adornment`/`AdornmentText`, never edited).

- [ ] **Step 5: Add the `SaveAsync` guard and unwrap the nullable fields**

Replace the body of `SaveAsync` (currently lines 604–618) with:

```csharp
    private async Task SaveAsync()
    {
        if (_form.Nivel is null || _form.ExperienciaAtual is null || _form.PontosDeIgnicao is null
            || _form.VitalidadeAtual is null || _form.FocoAtual is null || _form.AdrenalinaAtual is null)
        {
            _errorMessage = "Preencha todos os campos obrigatórios da aba Informações Básicas antes de salvar.";
            return;
        }

        var request = new UpdateCreatureSheetRequest(null, _form.Nome, _form.Raca, _form.Arquetipo, _form.SubArquetipo,
            _form.Afinidade, _form.Rank, _form.Nivel!.Value, _form.ExperienciaAtual!.Value, _form.PontosDeIgnicao!.Value,
            _form.VitalidadeAtual!.Value, _form.FocoAtual!.Value, _form.AdrenalinaAtual!.Value, _form.Cobertura);

        var response = await Http.PutAsJsonAsync($"creature-sheets/{SheetId}", request);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a ficha.";
            return;
        }

        await LoadSheetAsync();
    }
```

The guard exists because Task 2 will add `UpdateCoberturaAsync`, which calls `SaveAsync` directly from a live control with no `DataAnnotationsValidator` in front of it (Cobertura's `MudSelect` lives on the Combate tab) — the exact cross-tab shape that crashed Personagem's app with an unhandled `InvalidOperationException` when its own Fase 1b shipped the `!.Value` unwraps without this guard first. Landing the guard here, before the control exists, means there is never a commit in this branch where the crash is reachable. **Known, already-diagnosed limitation this port inherits (do not attempt to fix it here — see the plan header's note on the spec's correction):** the guard prevents the crash and blocks the write, but its `MudAlert` renders at the page top, above `<MudTabs>`, so when Cobertura (deep in the Combate tab) triggers the guard, the error can render off-screen and give no visible feedback — confirmed on NPC's identical shape by that plan's final review. Port the guard and `UpdateCoberturaAsync` exactly as specified; a real fix is separate future work touching all 3 sheet pages at once.

`LoadSheetAsync` (unchanged, not touched by this task) needs no change: every assignment like `_form.Nivel = sheet.Nivel;` assigns a non-nullable `int` into a now-nullable `int?` field, an implicit widening conversion.

- [ ] **Step 6: Build (expected to fail)**

Run: `dotnet build RuinaRPG.sln`
Expected: **compile errors**, all traceable to `<TabPage Title="Combate">` (and the two `TabPage`s after it) now appearing as direct children of the still-open `<MudTabs>`, and/or the closing `</TabControl>` (original line 504) having no matching open tag — RZ1034/RZ9991/RZ9981-class errors. This is the documented intermediate state from the spec, not a defect. Do not attempt to fix it in this task.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeCriatura.razor
git commit -m "feat: rewrite Ficha de Criatura shell + Informações Básicas + Atributos & Perícias on MudBlazor

Also makes SheetFormModel's 6 resource-int fields (Nivel, ExperienciaAtual,
PontosDeIgnicao, VitalidadeAtual, FocoAtual, AdrenalinaAtual — confirmed
against UpdateCreatureSheetRequest's exact parameter list) nullable +
[Required], adds DataAnnotationsValidator, and guards SaveAsync against
null fields — porting the fix already shipped on FichaDePersonagem.razor
and FichaDeNpc.razor, landed here ahead of Task 2's new Cobertura
control so it is never reachable unguarded.

Tabs 3-5 (Combate, Magias & Habilidades, Posses) are deliberately left
as old TabPage markup — dotnet build fails until Task 2 converts and
closes them, matching the same intermediate-state pattern used by every
prior plan in this rebuild.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 2: Combate + Magias & Habilidades + Posses + close the shell

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` — the three remaining `TabPage` blocks (search for `Title="Combate"`, `Title="Magias & Habilidades"`, `Title="Posses"` — line numbers shifted by Task 1's edit) through the closing `</TabControl>`, plus one new method inserted into `@code` immediately after `SaveAsync`

**Interfaces:**
- Consumes: everything Task 1 already established (`_form.Cobertura`, `SaveAsync()`, `LoadTabs2And3Async()`), all `_weapons`/`_weaponForm`/`_armorSlots`/`_shields`/`_shieldForm` fields (unchanged — `_weaponForm` additionally has `Origem`/`ManualNome`/`ManualTipoDeDano`/`ManualDados`/`ManualDano`, all unchanged), `_spellAbilities`/`_bankEntries`/`_spellAbilityForm`/`_masteries`/`_masteryForm` (unchanged — note: unlike NPC, no `_racialAbility`/`_runes`/`_runeForm` exist on this page at all), `_spoils`/`_spoilForm`/`_artifacts`/`_artifactForm`/`_affections`/`_affectionForm`/`_traits`/`_traitForm` (unchanged).
- Produces: `UpdateCoberturaAsync(string? value)` — sets `_form.Cobertura`, calls `SaveAsync()`, then `LoadTabs2And3Async()` (Cobertura feeds `DefesaNatural`, loaded there, not by `LoadSheetAsync`). Not consumed by any other task in this plan; a new, page-terminal method.

- [ ] **Step 1: Read the current state**

Read the full current file to confirm Task 1 landed as expected and find the current line numbers of the three remaining `TabPage` blocks and the closing `</TabControl>`.

- [ ] **Step 2: Insert the new method after `SaveAsync`**

Immediately after the closing `}` of `SaveAsync` (added by Task 1) and before `private async Task LoadTabs2And3Async()`, insert:

```csharp
    private async Task UpdateCoberturaAsync(string? value)
    {
        _form.Cobertura = value ?? "Nenhuma";
        await SaveAsync();
        await LoadTabs2And3Async(); // Cobertura feeds Defesa Natural, loaded here, not by LoadSheetAsync.
    }
```

This is a direct, unmodified port of `FichaDePersonagem.razor`'s/`FichaDeNpc.razor`'s method of the same name. Unlike NPC, do **not** add an `UpdateCiclosAsync` — Criatura's `SheetFormModel` has no `Ciclos` field at all, confirmed absent from Task 1's Step 4 output.

- [ ] **Step 3: Rewrite the "Combate" tab**

Replace the entire `<TabPage Title="Combate">...</TabPage>` block with:

```razor
    <MudTabPanel Text="Combate">
        <Section Title="Armas">
            <EditForm Model="_weaponForm" OnValidSubmit="AddWeaponAsync">
                <MudSelect T="string" @bind-Value="_weaponForm.Origem" Label="Origem">
                    <MudSelectItem Value="@("Catalogo")">Vincular ao Catálogo</MudSelectItem>
                    <MudSelectItem Value="@("Manual")">Preencher manualmente (ataque natural)</MudSelectItem>
                </MudSelect>

                @if (_weaponForm.Origem == "Catalogo")
                {
                    <EntityPicker @bind-Value="_weaponForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Arma"))" Placeholder="Buscar arma..." />
                }
                else
                {
                    <MudTextField T="string" @bind-Value="_weaponForm.ManualNome" Label="Nome" />
                    <MudSelect T="string" @bind-Value="_weaponForm.ManualTipoDeDano" Label="Tipo de Dano">
                        <MudSelectItem Value="@("")">Escolha um Tipo de Dano</MudSelectItem>
                        <MudSelectItem Value="@("Cortante")">Cortante</MudSelectItem>
                        <MudSelectItem Value="@("Perfurante")">Perfurante</MudSelectItem>
                        <MudSelectItem Value="@("Contundente")">Contundente</MudSelectItem>
                        <MudSelectItem Value="@("Magico")">Mágico</MudSelectItem>
                    </MudSelect>
                    <MudTextField T="string" @bind-Value="_weaponForm.ManualDados" Label="Dados" />
                    <MudNumericField T="int" @bind-Value="_weaponForm.ManualDano" Label="Dano" />
                }

                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Arma</MudButton>
            </EditForm>
            <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
                <tbody>
                    @foreach (var weapon in _weapons)
                    {
                        <tr>
                            <td>@weapon.Nome@(weapon.TipoDeDano is not null ? $" ({weapon.TipoDeDano})" : "")</td>
                            <td>
                                @if (weapon.ItemId is not null)
                                {
                                    <text>Durabilidade <MudNumericField T="int?" Value="@weapon.DurabilidadeAtual" ValueChanged="@(v => UpdateWeaponDurabilidadeAsync(weapon.Id, v))" Style="width:5em; display:inline-flex;" /> / @weapon.DurabilidadeMaximo</text>
                                }
                                else
                                {
                                    <text>Ataque natural</text>
                                }
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

        <Section Title="Armaduras">
            <MudSimpleTable Dense="true" Hover="true">
                <tbody>
                    @foreach (var slot in _armorSlots)
                    {
                        <tr>
                            <td>@slot.Slot: @(slot.Nome ?? "—") @(slot.DurabilidadeAtual is not null ? $"({slot.DurabilidadeAtual}/{slot.DurabilidadeMaximo})" : "")</td>
                            <td>
                                @if (slot.ItemId is not null)
                                {
                                    <MudNumericField T="int?" Value="@slot.DurabilidadeAtual" ValueChanged="@(v => UpdateArmorSlotDurabilidadeAsync(slot.Slot, v))" Style="width:5em; display:inline-flex;" />
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
                                <MudNumericField T="int?" Value="@shield.DurabilidadeAtual" ValueChanged="@(v => UpdateShieldDurabilidadeAsync(shield.Id, v))" Style="width:5em; display:inline-flex;" />
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
    </MudTabPanel>
```

`Cobertura` is a newly-added control — not present in the current file — closing the "backend wired, no UI" gap identified in the spec. Note the `weapon.DurabilidadeMaximo` (masculine) and `slot.DurabilidadeMaximo` (masculine) vs. `shield.DurabilidadeMaxima` (feminine) property names — preserve exactly, per this plan's Global Constraints note. Note also the `@if`/`else` around `_weaponForm.Origem` and around `weapon.ItemId is not null` — both structures unique to this page, not present on Personagem or NPC — must survive the component swap intact.

- [ ] **Step 4: Rewrite the "Magias & Habilidades" tab**

Replace the entire `<TabPage Title="Magias & Habilidades">...</TabPage>` block with:

```razor
    <MudTabPanel Text="Magias & Habilidades">
        <Section Title="Magias e Habilidades">
            <EditForm Model="_spellAbilityForm" OnValidSubmit="AddSpellAbilityAsync">
                <MudSelect T="string" @bind-Value="_spellAbilityForm.Origem" Label="Origem">
                    <MudSelectItem Value="@("Zero")">Montar do zero</MudSelectItem>
                    <MudSelectItem Value="@("Banco")">Escolher do Banco de Magias</MudSelectItem>
                </MudSelect>

                @if (_spellAbilityForm.Origem == "Banco")
                {
                    <MudSelect T="string" @bind-Value="_spellAbilityForm.SourceBankEntryId" Label="Entrada do Banco">
                        <MudSelectItem Value="@("")">Escolha uma entrada</MudSelectItem>
                        @foreach (var entry in _bankEntries)
                        {
                            <MudSelectItem Value="@entry.Id">@entry.Nome (@entry.Tipo, Grau @entry.Grau)</MudSelectItem>
                        }
                    </MudSelect>
                }
                else
                {
                    <MudTextField T="string" @bind-Value="_spellAbilityForm.Nome" Label="Nome" />
                    <MudSelect T="string" @bind-Value="_spellAbilityForm.Tipo" Label="Tipo">
                        <MudSelectItem Value="@("Magia")">Magia</MudSelectItem>
                        <MudSelectItem Value="@("Habilidade")">Habilidade</MudSelectItem>
                        <MudSelectItem Value="@("Racial")">Racial</MudSelectItem>
                    </MudSelect>
                    <MudNumericField T="int" @bind-Value="_spellAbilityForm.Grau" Label="Grau" />
                    <MudTextField T="string" @bind-Value="_spellAbilityForm.Descricao" Label="Descrição" Lines="3" />

                    <MudText Typo="Typo.h6" Class="mt-3">Efeitos</MudText>
                    <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="AddSpellAbilityEffect">Adicionar Efeito</MudButton>
                    <MudSimpleTable Dense="true" Hover="true" Class="mt-2">
                        <thead><tr><th>Nome do Efeito</th><th>Quantidade</th><th>Custo em PI</th><th></th></tr></thead>
                        <tbody>
                            @for (var i = 0; i < _spellAbilityForm.Efeitos.Count; i++)
                            {
                                var index = i; // capture for the closures below
                                <tr>
                                    <td><MudTextField T="string" @bind-Value="_spellAbilityForm.Efeitos[index].EfeitoNome" /></td>
                                    <td><MudNumericField T="int?" @bind-Value="_spellAbilityForm.Efeitos[index].Quantidade" /></td>
                                    <td><MudNumericField T="int" @bind-Value="_spellAbilityForm.Efeitos[index].CustoPI" /></td>
                                    <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => _spellAbilityForm.Efeitos.RemoveAt(index))">Remover</MudButton></td>
                                </tr>
                            }
                        </tbody>
                    </MudSimpleTable>
                }

                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Magia/Habilidade</MudButton>
            </EditForm>
            <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
                <tbody>
                    @foreach (var spellAbility in _spellAbilities)
                    {
                        <tr>
                            <td>@spellAbility.Nome (@spellAbility.Tipo, Grau @spellAbility.Grau) — Gasto em PI @spellAbility.GastoEmPI, Custo @spellAbility.Custo — @spellAbility.Descricao</td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteSpellAbilityAsync(spellAbility.Id))">Remover</MudButton></td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
        </Section>

        <Section Title="Maestrias">
            <EditForm Model="_masteryForm" OnValidSubmit="AddMasteryAsync">
                <MudTextField T="string" @bind-Value="_masteryForm.Nome" Label="Nome" />
                <MudSelect T="string" @bind-Value="_masteryForm.Pericia" Label="Perícia">
                    <MudSelectItem Value="@("")">Escolha uma Perícia</MudSelectItem>
                    @foreach (var skill in _skills)
                    {
                        <MudSelectItem Value="@skill.Pericia">@skill.Pericia</MudSelectItem>
                    }
                </MudSelect>
                <MudSelect T="string" @bind-Value="_masteryForm.Atributo" Label="Atributo">
                    <MudSelectItem Value="@("")">Escolha um Atributo</MudSelectItem>
                    @foreach (var attr in _attributes)
                    {
                        <MudSelectItem Value="@attr.Atributo">@attr.Atributo</MudSelectItem>
                    }
                </MudSelect>
                <MudNumericField T="int" @bind-Value="_masteryForm.GastoMaestria" Label="Gasto Maestria" />
                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Maestria</MudButton>
            </EditForm>
            <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
                <tbody>
                    @foreach (var mastery in _masteries)
                    {
                        <tr>
                            <td>@mastery.Nome — @mastery.Pericia / @mastery.Atributo — Gasto @mastery.GastoMaestria — Total @mastery.Total</td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteMasteryAsync(mastery.Id))">Remover</MudButton></td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
        </Section>
    </MudTabPanel>
```

Unlike NPC, there is **no** "Habilidade Racial" `Section` and **no** "Runas" `Section` — confirmed absent from the current file and absent from `Requisitos - Ficha de Criaturas.md`'s diff against Personagem.

- [ ] **Step 5: Rewrite the "Posses" tab and close the shell**

Replace the entire `<TabPage Title="Posses">...</TabPage>` block *and* the file's final `</TabControl>` closing tag with:

```razor
    <MudTabPanel Text="Posses">
        <Section Title="Espólios">
            <EditForm Model="_spoilForm" OnValidSubmit="AddSpoilAsync">
                <EntityPicker @bind-Value="_spoilForm.ItemId" SearchItems="SearchAllItemsAsync" Placeholder="Buscar item..." />
                <MudNumericField T="int" @bind-Value="_spoilForm.Qtd" Label="Quantidade" />
                <MudNumericField T="int" @bind-Value="_spoilForm.DT" Label="DT" />
                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Espólio</MudButton>
            </EditForm>
            <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
                <tbody>
                    @foreach (var spoil in _spoils)
                    {
                        <tr>
                            <td>@spoil.Nome — Custo @spoil.Custo — Qtd @spoil.Qtd — Total @spoil.CustoTotal — DT @spoil.DT</td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteSpoilAsync(spoil.Id))">Remover</MudButton></td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
        </Section>

        <Section Title="Artefatos">
            <EditForm Model="_artifactForm" OnValidSubmit="AddArtifactAsync">
                <EntityPicker @bind-Value="_artifactForm.ArtifactItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Artefato"))" Placeholder="Buscar artefato..." />
                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Artefato</MudButton>
            </EditForm>
            @foreach (var group in _artifacts.GroupBy(a => a.TipoDeAlvo))
            {
                <MudText Typo="Typo.h6" Class="mt-3">@group.Key</MudText>
                <MudSimpleTable Dense="true" Hover="true">
                    <tbody>
                        @foreach (var artifact in group)
                        {
                            <tr>
                                <td>@artifact.Nome — Alvo @artifact.Alvo — Valor @artifact.Valor</td>
                                <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteArtifactAsync(artifact.Id))">Remover</MudButton></td>
                            </tr>
                        }
                    </tbody>
                </MudSimpleTable>
            }
        </Section>

        <Section Title="Afeições">
            <EditForm Model="_affectionForm" OnValidSubmit="AddAffectionAsync">
                <MudTextField T="string" @bind-Value="_affectionForm.Nome" Label="Nome" />
                <MudNumericField T="int" @bind-Value="_affectionForm.Favorabilidade" Label="Favorabilidade" />
                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Afeição</MudButton>
            </EditForm>
            <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
                <tbody>
                    @foreach (var affection in _affections)
                    {
                        <tr>
                            <td>@affection.Nome — Favorabilidade @affection.Favorabilidade</td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteAffectionAsync(affection.Id))">Remover</MudButton></td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
        </Section>

        <Section Title="Características">
            <EditForm Model="_traitForm" OnValidSubmit="AddTraitAsync">
                <EntityPicker @bind-Value="_traitForm.TraitId" SearchItems="SearchTraitsAsync" Placeholder="Buscar característica..." />
                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Característica</MudButton>
            </EditForm>
            <MudText Typo="Typo.h6" Class="mt-3">Positivas (Total: @(_traits?.TotalPositivas ?? 0))</MudText>
            <MudSimpleTable Dense="true" Hover="true">
                <tbody>
                    @foreach (var trait in _traits?.Positivas ?? new())
                    {
                        <tr>
                            <td>@trait.Nome (@trait.Custo) — @trait.Descricao</td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteTraitAsync(trait.Id))">Remover</MudButton></td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
            <MudText Typo="Typo.h6" Class="mt-3">Negativas (Total: @(_traits?.TotalNegativas ?? 0))</MudText>
            <MudSimpleTable Dense="true" Hover="true">
                <tbody>
                    @foreach (var trait in _traits?.Negativas ?? new())
                    {
                        <tr>
                            <td>@trait.Nome (@trait.Custo) — @trait.Descricao</td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteTraitAsync(trait.Id))">Remover</MudButton></td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
        </Section>
    </MudTabPanel>
</MudTabs>
```

Note: this page's "Inventário"-equivalent section is titled "Espólios" (not "Inventário") and its `EditForm` uses `SearchAllItemsAsync` (unrestricted, no `Tipo` filter — confirmed: `AddSpoil` accepts any catalog item) instead of `SearchItemsByTipoAsync(q, "ItemGeral")` — this is a genuine, deliberate difference from both Personagem's and NPC's Inventário section, not a mistake to "correct." There is no Ciclos control anywhere on this page.

- [ ] **Step 6: Full-solution build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors — the whole page is now on MudBlazor, and this is the last of the 3 sheet pages to complete the front-end rebuild.

- [ ] **Step 7: Manual/serve verification**

Run `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000` against a locally-served `dotnet run --project src/RuinaRPG.Client` (or an already-running dev server) to confirm the app still serves (`200`) — this only proves the static shell serves, since this is a Blazor WASM app with no server prerendering. If a browser/Playwright tool is available in this environment, load a real Criatura sheet URL and confirm: no console errors across all 5 tabs; the weapon Origem toggle correctly switches between the Catálogo picker and the 4 manual fields; changing Cobertura on the Combate tab visibly updates Defesa Natural after the round-trip; and specifically reproduce Fase 1b's cross-tab crash shape — clear the "Nível" field on Tab 1 (don't submit), switch to Tab 3, change Cobertura — confirm the app shows the guard's error message instead of crashing (the guard's message may render off-screen per the known, already-diagnosed limitation described in Step 5 above — check the DOM/page source for the message text even if it isn't visually in the viewport, and don't treat off-screen rendering itself as a new defect to fix in this task). If no browser tool is available, say so explicitly rather than claiming a visual check you didn't do.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeCriatura.razor
git commit -m "feat: rewrite Ficha de Criatura Combate, Magias & Habilidades, Posses on MudBlazor

Closes the MudBlazor migration for this page — the last of the 3 sheet
pages (Personagem, NPC, Criatura) to complete the front-end rebuild.
Adds the Cobertura control on Combate that FichaDeCriatura.razor's
SheetFormModel/UpdateCreatureSheetRequest already carried and applied
but never rendered anywhere — same gap already closed on Personagem
and NPC.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Final whole-branch review

After Task 2, dispatch a final whole-branch review (opus, per this session's standing practice for this gate — see `MEMORY.md` → `ruina-plan-queue`) covering the full diff across both tasks, not just each task's own per-task review. If a headless-browser/Playwright tool is available to the reviewing agent, serve the client from the worktree behind a proxy to the running docker stack, provision throwaway GM + Criatura + campaign data via the API, and empirically render the page in both light/dark themes, driving all 5 tabs and the Cobertura control's round-trip (Defesa Natural) exactly as described in Task 2 Step 7 — the equivalent NPC round's final review used this method to confirm the cross-tab crash does not reproduce, and found the guard's off-screen-alert limitation (already documented above, do not re-flag as a new Critical/Important finding on this branch — it is a known, spec-documented, inherited limitation, not a defect introduced here). Delete the test data after. If no such tool is available, say so explicitly and fall back to `dotnet build` + line-by-line diff review against the conversion-rules table above, paying particular attention to the guard/Cobertura interaction and the weapon Origem toggle's `@if`/`else` structure.
