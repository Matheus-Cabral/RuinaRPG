# Ficha de NPC — MudBlazor rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite `src/RuinaRPG.Client/Pages/FichaDeNpc.razor` from Bootstrap/`TabControl` onto MudBlazor (all 5 tabs), matching the discipline already used to migrate `FichaDePersonagem.razor`. Fix two ledgered "backend wired, UI control missing" gaps discovered while planning this round (Cobertura, Ciclos) and one already-proven behavioral bug class (nullable resource fields silently persisting `0` when cleared).

**Architecture:** Two tasks, both touching the same file sequentially (never in parallel). Task 1 opens the `MudTabs` shell and fully migrates Tabs 1–2 (Informações Básicas, Atributos & Perícias), plus the plan's one `@code` behavior fix (nullable `SheetFormModel` fields + `SaveAsync` guard) — Tabs 3–5 are deliberately left untouched, so Task 1's own build is expected to fail (a known, documented intermediate state, not a defect). Task 2 migrates Tabs 3–5 (Combate, Magias & Habilidades, Posses), closes the shell, and adds the two new small `@code` methods (`UpdateCoberturaAsync`, `UpdateCiclosAsync`) that back the two newly-added UI controls — this task always runs after Task 1, so the guard is already in place before either control exists.

**Tech Stack:** Blazor WebAssembly (.NET 8), MudBlazor 9.9.0, existing shared components (`Section`, `Breadcrumbs`, `EntityPicker`, `AfinidadeSelect`, `LinhagemVarianteFields`, `VocacaoSubVocacaoFields`) consumed as-is, no changes to any of them.

**Spec:** `docs/superpowers/specs/2026-09-02-ficha-de-npc-e-criatura-design.md` — authority for scope. This plan covers only the NPC half; Criatura is a separate later plan.

## Global Constraints

- No color/hex outside the existing `--rr-*` tokens; no font outside Cormorant Garamond/Inter (nothing in this plan introduces new CSS).
- `dotnet build RuinaRPG.sln` must be 0 warnings / 0 errors after Task 2 (Task 1's own build is a documented exception — see Task 1).
- **No `@code` method's logic, signature, or the request/response DTOs it calls change**, except: (a) `SheetFormModel`'s resource-int fields become nullable + `[Required]` (Task 1), (b) `SaveAsync` gains an up-front null guard (Task 1), (c) two new methods, `UpdateCoberturaAsync`/`UpdateCiclosAsync`, are added (Task 2) — both are direct ports of Personagem's already-shipped methods of the same name, not new design. Every `Http.GetAsync`/`PostAsJsonAsync`/`PutAsJsonAsync`/`DeleteAsync` call, every `Load*Async` method, and every nested `*FormModel` class other than `SheetFormModel` stays exactly as it is today.
- Both tasks edit the same file (`FichaDeNpc.razor`) in non-overlapping regions — Task 1 touches the top `@using` block, Tabs 1–2, `Crumbs`, `SaveAsync`, and `SheetFormModel`; Task 2 touches Tabs 3–5 and inserts two new methods right after `SaveAsync`. Dispatch strictly sequentially within one worktree.
- `EntityPicker` usage (`@bind-Value`/`Value`+`ValueChanged`, `SearchItems`, `Placeholder` parameters) is unchanged — nothing in this plan touches its internals.
- `Section`, `Breadcrumbs`, `AfinidadeSelect`, `LinhagemVarianteFields`, `VocacaoSubVocacaoFields` are consumed exactly as `FichaDePersonagem.razor` already consumes them — no changes to any of these components.

### Conversion rules (apply throughout; identical to the Fase 1a/1b plans' table, reproduced here for a fresh reader)

| Old | New |
|---|---|
| `<InputText class="form-control" @bind-Value="X" />` preceded by a `<label>` | `<MudTextField T="string" @bind-Value="X" Label="..." />` (Label = the `<label>` text; drop the old `<label>`) |
| `<InputTextArea class="form-control" @bind-Value="X" />` | `<MudTextField T="string" @bind-Value="X" Label="..." Lines="3" />` |
| `<InputNumber class="form-control" @bind-Value="X" />` (int, two-way bound to a brand-new *add*-form field, not a persisted resource) | `<MudNumericField T="int" @bind-Value="X" Label="..." />` — stays non-nullable, no "cleared silently saves 0" risk on a fresh add-form |
| `<InputNumber class="form-control" @bind-Value="X" />` (int, bound to a persisted resource field on `_form`, submitted via `SaveAsync`) | `<MudNumericField T="int?" @bind-Value="X" For="@(() => X)" Label="..." />` — the field must already be `int?` + `[Required]` on `SheetFormModel` per Task 1 |
| `<select class="form-select" @bind="X">` with `<option>`s | `<MudSelect T="string" @bind-Value="X" Label="...">` with one `<MudSelectItem Value="@("...")">...</MudSelectItem>` per option |
| A raw `<input>`/`<select>` bound via `value="@item.Field" @onchange="@(e => Method(..., e.Value))"` (one-way, live-update, not a form model) | `<MudNumericField T="int?" Value="@item.Field" ValueChanged="@(v => Method(..., v))" />` — `v` arrives already typed (`int?`), passing it into a method whose parameter is `object? value` is a normal implicit boxing conversion |
| `<button type="submit" class="btn btn-primary">Label</button>` | `<MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">Label</MudButton>` |
| `<button ... class="btn btn-outline-primary btn-sm" @onclick="...">Label</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="...">Label</MudButton>` |
| `<button ... class="btn btn-outline-danger btn-sm" @onclick="...">Remover</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="...">Remover</MudButton>` |
| `<h3>Title</h3>` grouping a whole block | wrap the block in `<Section Title="Title">...</Section>` |
| `<h4>Subtitle</h4>` grouping a sub-block *inside* a `Section` | `<MudText Typo="Typo.h6" Class="mt-3">Subtitle</MudText>` — no nested `Section` |
| `<table class="table">...` or `<ul class="list-unstyled"><li>...` (a list of items with a per-row action button) | `<MudSimpleTable Dense="true" Hover="true">` with a `<tbody>` `@foreach` producing one `<tr>` per item |
| `<TabControl>`/`<TabPage Title="...">` | `<MudTabs>`/`<MudTabPanel Text="...">` |
| Page-level `<p class="error">@_errorMessage</p>` | `<MudAlert Severity="Severity.Error" Class="mb-3">@_errorMessage</MudAlert>` |
| Page `<h1>` | left as bare `<h1>` — matches Personagem's established precedent (Fase 1b explicitly left this out of scope), not this plan's job either |

---

## Task 1: Shell open + Informações Básicas + Atributos & Perícias + nullable-int fix

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor` — top `@using` block (original lines 1–7), page header/error block (lines 9–16), `<TabControl>` opening tag through the end of the Atributos & Perícias `TabPage` (lines 18–253), `Crumbs` property (lines 596–601), `SaveAsync` (lines 699–715), `SheetFormModel` class (lines 1021–1055)

**Interfaces:**
- Consumes: `UpdateNpcSheetRequest` (`src/RuinaRPG.Contracts/NpcSheets/UpdateNpcSheetRequest.cs`) — unchanged, still takes non-nullable `int` for every resource field. `NpcSheetResponse` (unchanged).
- Produces: `SheetFormModel`'s resource-int fields (`Nivel`, `ExperienciaAtual`, `EAPAtual`, `NucleosRankF/E/D/C/B/A/S`, `PontosDeIgnicaoAtual`, `PontosDeIgnicaoTotal`, `VitalidadeAtual`, `FocoAtual`, `AdrenalinaAtual`, `EstresseAtual`) become `int?` — 16 fields total. `Cobertura` (`string`) and `Ciclos` (`int`) stay exactly as they are today; Task 2 adds their UI controls without touching their types. Task 2 doesn't reference any of the 16 nullable fields (they live entirely in Tab 1, already migrated), so nothing in Task 2 needs updating for this change, but note it for the task reviewer.

- [ ] **Step 1: Read the current state**

Read `src/RuinaRPG.Client/Pages/FichaDeNpc.razor` in full. Confirm the line numbers referenced below still match (this is the first task in this plan, so they should be exact) — if not, adjust, but the content itself is exactly as shown.

- [ ] **Step 2: Rewrite the header, `@using` block, and Tabs 1–2**

Replace lines 1–253 (from `@page "/npcs/{SheetId}"` through the closing `</TabPage>` of "Atributos & Perícias") with:

```razor
@page "/npcs/{SheetId}"
@inject HttpClient Http
@using RuinaRPG.Contracts.NpcSheets
@using RuinaRPG.Contracts.SpellsAndAbilities
@using RuinaRPG.Contracts.CharacterSheets
@using RuinaRPG.Contracts.Items
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Client.Shared.Fields
@using MudBlazor
@using System.ComponentModel.DataAnnotations

<Breadcrumbs Items="@Crumbs" />
<h1>Ficha de NPC</h1>

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
                <LinhagemVarianteFields @bind-Linhagem="_form.Linhagem" @bind-Variante="_form.Variante" />
                <VocacaoSubVocacaoFields @bind-Vocacao="_form.Vocacao" @bind-SubVocacao="_form.SubVocacao" />
                <AfinidadeSelect @bind-Value="_form.Afinidade" />
                <MudTextField T="string" @bind-Value="_form.Propriedade" Label="Propriedade" />
            </Section>

            <Section Title="Nível e Progressão">
                <MudNumericField T="int?" @bind-Value="_form.Nivel" For="@(() => _form.Nivel)" Label="Nível" />
                <MudText>@_form.GraduacaoLabel: @_form.Graduacao</MudText>
                <MudCheckBox T="bool" @bind-Value="_form.PossuiCoracaoDeMana" Label="Possui Coração de Mana?" />
                <MudNumericField T="int?" @bind-Value="_form.ExperienciaAtual" For="@(() => _form.ExperienciaAtual)" Label="Experiência Atual" />
                <MudNumericField T="int?" @bind-Value="_form.EAPAtual" For="@(() => _form.EAPAtual)" Label="EAP Atual" />
                <MudNumericField T="int?" @bind-Value="_form.PontosDeIgnicaoAtual" For="@(() => _form.PontosDeIgnicaoAtual)" Label="Pontos de Ignição Atual" />
                <MudNumericField T="int?" @bind-Value="_form.PontosDeIgnicaoTotal" For="@(() => _form.PontosDeIgnicaoTotal)" Label="Pontos de Ignição Total" />
            </Section>

            <Section Title="Âmbares Absorvidos">
                <MudNumericField T="int?" @bind-Value="_form.NucleosRankF" For="@(() => _form.NucleosRankF)" Label="Rank F" />
                <MudNumericField T="int?" @bind-Value="_form.NucleosRankE" For="@(() => _form.NucleosRankE)" Label="Rank E" />
                <MudNumericField T="int?" @bind-Value="_form.NucleosRankD" For="@(() => _form.NucleosRankD)" Label="Rank D" />
                <MudNumericField T="int?" @bind-Value="_form.NucleosRankC" For="@(() => _form.NucleosRankC)" Label="Rank C" />
                <MudNumericField T="int?" @bind-Value="_form.NucleosRankB" For="@(() => _form.NucleosRankB)" Label="Rank B" />
                <MudNumericField T="int?" @bind-Value="_form.NucleosRankA" For="@(() => _form.NucleosRankA)" Label="Rank A" />
                <MudNumericField T="int?" @bind-Value="_form.NucleosRankS" For="@(() => _form.NucleosRankS)" Label="Rank S" />
            </Section>

            <Section Title="Recursos">
                <MudNumericField T="int?" @bind-Value="_form.AdrenalinaAtual" For="@(() => _form.AdrenalinaAtual)" Label="Adrenalina (PA)" Adornment="Adornment.End" AdornmentText="@($"/ {_form.AdrenalinaMaximo}")" />
                <MudNumericField T="int?" @bind-Value="_form.FocoAtual" For="@(() => _form.FocoAtual)" Label="Foco (PF)" Adornment="Adornment.End" AdornmentText="@($"/ {_form.FocoMaximo}")" />
                <MudNumericField T="int?" @bind-Value="_form.EstresseAtual" For="@(() => _form.EstresseAtual)" Label="Estresse" Adornment="Adornment.End" AdornmentText="@($"/ {_form.EstresseMaximo}")" />
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
    </MudTabPanel>
```

The very next line in the file after this block is still `    <TabPage Title="Combate">` — leave it and everything through the original closing `</TabControl>` (original line 570) completely untouched in this task.

- [ ] **Step 3: Fully-qualify `Crumbs`' `BreadcrumbItem` type**

The new `@using MudBlazor` added in Step 2 collides with `MudBlazor.BreadcrumbItem` — same collision Personagem hit twice. Replace the `Crumbs` property (currently lines 596–601):

```csharp
    private List<BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("NPCs do GM", "npcs"),
        new(string.IsNullOrWhiteSpace(_form.Nome) ? "Ficha de NPC" : _form.Nome),
    };
```

with:

```csharp
    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("NPCs do GM", "npcs"),
        new(string.IsNullOrWhiteSpace(_form.Nome) ? "Ficha de NPC" : _form.Nome),
    };
```

Do not add a bare `@using RuinaRPG.Client.Shared` anywhere — that would re-trigger the same ambiguity (`MudBlazor.BreadcrumbItem` vs. `RuinaRPG.Client.Shared.BreadcrumbItem`), which is exactly why the fully-qualified form is used here instead.

- [ ] **Step 4: Make the 16 resource fields nullable + required, in `SheetFormModel`**

Replace the class body (currently lines 1021–1055) with:

```csharp
    private class SheetFormModel
    {
        public string? Nome { get; set; }
        public string? Linhagem { get; set; }
        public string? Variante { get; set; }
        public string? Vocacao { get; set; }
        public string? SubVocacao { get; set; }
        public string? Afinidade { get; set; }
        public string? Propriedade { get; set; }
        [Required(ErrorMessage = "Informe o Nível.")]
        public int? Nivel { get; set; } = 1;
        public int Graduacao { get; set; }
        public string GraduacaoLabel { get; set; } = "Grau";
        public bool PossuiCoracaoDeMana { get; set; }
        [Required(ErrorMessage = "Informe a Experiência Atual.")]
        public int? ExperienciaAtual { get; set; }
        [Required(ErrorMessage = "Informe o EAP Atual.")]
        public int? EAPAtual { get; set; }
        [Required(ErrorMessage = "Informe os Núcleos de Rank F.")]
        public int? NucleosRankF { get; set; }
        [Required(ErrorMessage = "Informe os Núcleos de Rank E.")]
        public int? NucleosRankE { get; set; }
        [Required(ErrorMessage = "Informe os Núcleos de Rank D.")]
        public int? NucleosRankD { get; set; }
        [Required(ErrorMessage = "Informe os Núcleos de Rank C.")]
        public int? NucleosRankC { get; set; }
        [Required(ErrorMessage = "Informe os Núcleos de Rank B.")]
        public int? NucleosRankB { get; set; }
        [Required(ErrorMessage = "Informe os Núcleos de Rank A.")]
        public int? NucleosRankA { get; set; }
        [Required(ErrorMessage = "Informe os Núcleos de Rank S.")]
        public int? NucleosRankS { get; set; }
        [Required(ErrorMessage = "Informe os Pontos de Ignição Atual.")]
        public int? PontosDeIgnicaoAtual { get; set; }
        [Required(ErrorMessage = "Informe os Pontos de Ignição Total.")]
        public int? PontosDeIgnicaoTotal { get; set; }
        [Required(ErrorMessage = "Informe a Vitalidade.")]
        public int? VitalidadeAtual { get; set; }
        [Required(ErrorMessage = "Informe o Foco.")]
        public int? FocoAtual { get; set; }
        [Required(ErrorMessage = "Informe a Adrenalina.")]
        public int? AdrenalinaAtual { get; set; }
        [Required(ErrorMessage = "Informe o Estresse.")]
        public int? EstresseAtual { get; set; }
        public string Cobertura { get; set; } = "Nenhuma";
        public int Ciclos { get; set; }
        public int VitalidadeMaximo { get; set; }
        public int FocoMaximo { get; set; }
        public int AdrenalinaMaximo { get; set; }
        public int EstresseMaximo { get; set; }
    }
```

Note what did **not** change: `Graduacao`/`GraduacaoLabel` (read-only display, `NpcSheetsController` never applies them from the request — see `UpdateNpcSheetRequest.cs`, they're absent from it), `Cobertura` (string, its own `MudSelect` added in Task 2), `Ciclos` (its own `MudNumericField` added in Task 2, one-way live-update field, not part of this `EditForm`). `EAPAtual` **is** included here even though Personagem's equivalent field is display-only — confirmed by reading `NpcSheetsController.cs:95` (`sheet.EAPAtual = request.EAPAtual;`) that NPC's EAP is a genuinely live, editable, persisted field, unlike Personagem's (where it's accepted-but-ignored per Épico 1 item 3) — so it carries the same "cleared silently saves 0" risk as every other resource field here and gets the same treatment.

- [ ] **Step 5: Add the `SaveAsync` guard and unwrap the nullable fields**

Replace the body of `SaveAsync` (currently lines 699–715) with:

```csharp
    private async Task SaveAsync()
    {
        if (_form.Nivel is null || _form.ExperienciaAtual is null || _form.EAPAtual is null
            || _form.NucleosRankF is null || _form.NucleosRankE is null || _form.NucleosRankD is null
            || _form.NucleosRankC is null || _form.NucleosRankB is null || _form.NucleosRankA is null || _form.NucleosRankS is null
            || _form.PontosDeIgnicaoAtual is null || _form.PontosDeIgnicaoTotal is null
            || _form.VitalidadeAtual is null || _form.FocoAtual is null || _form.AdrenalinaAtual is null || _form.EstresseAtual is null)
        {
            _errorMessage = "Preencha todos os campos obrigatórios da aba Informações Básicas antes de salvar.";
            return;
        }

        var request = new UpdateNpcSheetRequest(null, _form.Nome, _form.Linhagem, _form.Variante, _form.Vocacao, _form.SubVocacao,
            _form.Afinidade, _form.Propriedade, _form.Nivel!.Value, _form.PossuiCoracaoDeMana, _form.ExperienciaAtual!.Value, _form.EAPAtual!.Value,
            _form.NucleosRankF!.Value, _form.NucleosRankE!.Value, _form.NucleosRankD!.Value, _form.NucleosRankC!.Value, _form.NucleosRankB!.Value, _form.NucleosRankA!.Value, _form.NucleosRankS!.Value,
            _form.PontosDeIgnicaoAtual!.Value, _form.PontosDeIgnicaoTotal!.Value, _form.VitalidadeAtual!.Value, _form.FocoAtual!.Value, _form.AdrenalinaAtual!.Value, _form.EstresseAtual!.Value,
            _form.Cobertura, _form.Ciclos);

        var response = await Http.PutAsJsonAsync($"npc-sheets/{SheetId}", request);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a ficha.";
            return;
        }

        await LoadSheetAsync();
    }
```

The guard exists because Task 2 will add `UpdateCoberturaAsync` and `UpdateCiclosAsync`, both of which call `SaveAsync` directly from a live control with no `DataAnnotationsValidator` in front of it (Cobertura's `MudSelect` lives on the Combate tab, Ciclos' `MudNumericField` on Posses) — the exact cross-tab shape that crashed Personagem's app with an unhandled `InvalidOperationException` when Fase 1b shipped the `!.Value` unwraps without this guard first. Landing the guard here, before either control exists, means there is never a commit in this branch where the crash is reachable. `LoadSheetAsync` (unchanged, not touched by this task) needs no change: every assignment like `_form.Nivel = sheet.Nivel;` assigns a non-nullable `int` into a now-nullable `int?` field, an implicit widening conversion.

- [ ] **Step 6: Build (expected to fail)**

Run: `dotnet build RuinaRPG.sln`
Expected: **compile errors**, all traceable to `<TabPage Title="Combate">` (and the two `TabPage`s after it) now appearing as direct children of the still-open `<MudTabs>`, and/or the closing `</TabControl>` (original line 570) having no matching open tag — RZ1034/RZ9991/RZ9981-class errors. This is the documented intermediate state from the spec, not a defect. Do not attempt to fix it in this task.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "feat: rewrite Ficha de NPC shell + Informações Básicas + Atributos & Perícias on MudBlazor

Also makes SheetFormModel's 16 resource-int fields (including EAPAtual,
which unlike Personagem's is a genuinely live NpcSheetsController field)
nullable + [Required], adds DataAnnotationsValidator, and guards
SaveAsync against null fields — porting the fix Fase 1b already shipped
on FichaDePersonagem.razor, landed here ahead of Task 2's new
Cobertura/Ciclos controls so neither is ever reachable unguarded.

Tabs 3-5 (Combate, Magias & Habilidades, Posses) are deliberately left
as old TabPage markup — dotnet build fails until Task 2 converts and
closes them, matching the same intermediate-state pattern used by the
Fase 1a plan for this page's Personagem counterpart.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 2: Combate + Magias & Habilidades + Posses + close the shell

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor` — the three remaining `TabPage` blocks (search for `Title="Combate"`, `Title="Magias & Habilidades"`, `Title="Posses"` — line numbers shifted by Task 1's edit) through the closing `</TabControl>`, plus two new methods inserted into `@code` immediately after `SaveAsync`

**Interfaces:**
- Consumes: everything Task 1 already established (`_form.Cobertura`, `_form.Ciclos`, `SaveAsync()`, `LoadTabs2And3Async()`), all `_weapons`/`_armorSlots`/`_shields`/`*Form` fields (unchanged), `_racialAbility`/`_spellAbilities`/`_bankEntries`/`_spellAbilityForm`/`_runes`/`_runeForm`/`_masteries`/`_masteryForm` (unchanged), `_inventory`/`_inventoryForm`/`_artifacts`/`_artifactForm`/`_affections`/`_affectionForm`/`_traits`/`_traitForm` (unchanged).
- Produces: `UpdateCoberturaAsync(string? value)` — sets `_form.Cobertura`, calls `SaveAsync()`, then `LoadTabs2And3Async()` (Cobertura feeds `DefesaNatural`, loaded there, not by `LoadSheetAsync`). `UpdateCiclosAsync(object? value)` — parses `value` into `_form.Ciclos`, calls `SaveAsync()`. Neither is consumed by any other task in this plan; both are new, page-terminal methods.

- [ ] **Step 1: Read the current state**

Read the full current file to confirm Task 1 landed as expected and find the current line numbers of the three remaining `TabPage` blocks and the closing `</TabControl>`.

- [ ] **Step 2: Insert the two new methods after `SaveAsync`**

Immediately after the closing `}` of `SaveAsync` (added by Task 1) and before `private async Task LoadTabs2And3Async()`, insert:

```csharp
    private async Task UpdateCoberturaAsync(string? value)
    {
        _form.Cobertura = value ?? "Nenhuma";
        await SaveAsync();
        await LoadTabs2And3Async(); // Cobertura feeds Defesa Natural, loaded here, not by LoadSheetAsync.
    }

    private async Task UpdateCiclosAsync(object? value)
    {
        if (int.TryParse(value?.ToString(), out var ciclos))
            _form.Ciclos = ciclos;
        await SaveAsync();
    }
```

This is a direct, unmodified port of `FichaDePersonagem.razor`'s methods of the same name.

- [ ] **Step 3: Rewrite the "Combate" tab**

Replace the entire `<TabPage Title="Combate">...</TabPage>` block with:

```razor
    <MudTabPanel Text="Combate">
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
                                <MudNumericField T="int?" Value="@weapon.DurabilidadeAtual" ValueChanged="@(v => UpdateWeaponDurabilidadeAsync(weapon.Id, v))" Style="width:5em; display:inline-flex;" />
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

`Cobertura` is a newly-added control — not present in the current file — closing the "backend wired, no UI" gap identified in the spec.

- [ ] **Step 4: Rewrite the "Magias & Habilidades" tab**

Replace the entire `<TabPage Title="Magias & Habilidades">...</TabPage>` block with:

```razor
    <MudTabPanel Text="Magias & Habilidades">
        <Section Title="Habilidade Racial">
            <MudText>@(_racialAbility?.Nome ?? "—")</MudText>
            <MudText>@(_racialAbility?.Descricao ?? "—")</MudText>
        </Section>

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

        <Section Title="Runas">
            <EditForm Model="_runeForm" OnValidSubmit="AddRuneAsync">
                <MudTextField T="string" @bind-Value="_runeForm.Nome" Label="Nome" />
                <MudTextField T="string" @bind-Value="_runeForm.Descricao" Label="Descrição" Lines="3" />
                <MudNumericField T="int" @bind-Value="_runeForm.Grau" Label="Grau" />
                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Runa</MudButton>
            </EditForm>
            <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
                <tbody>
                    @foreach (var rune in _runes)
                    {
                        <tr>
                            <td>@rune.Nome (Grau @rune.Grau) — @rune.Descricao</td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteRuneAsync(rune.Id))">Remover</MudButton></td>
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

- [ ] **Step 5: Rewrite the "Posses" tab and close the shell**

Replace the entire `<TabPage Title="Posses">...</TabPage>` block *and* the file's final `</TabControl>` closing tag with:

```razor
    <MudTabPanel Text="Posses">
        <Section Title="Inventário">
            <MudNumericField T="int?" Value="@_form.Ciclos" ValueChanged="@(v => UpdateCiclosAsync(v))" Label="Ciclos" Style="width:8em;" />
            <EditForm Model="_inventoryForm" OnValidSubmit="AddInventoryItemAsync">
                <EntityPicker @bind-Value="_inventoryForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "ItemGeral"))" Placeholder="Buscar item..." />
                <MudNumericField T="int" @bind-Value="_inventoryForm.Qtd" Label="Quantidade" />
                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Item</MudButton>
            </EditForm>
            <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
                <tbody>
                    @foreach (var item in _inventory)
                    {
                        <tr>
                            <td>@item.Nome — Peso @item.Peso — Qtd @item.Qtd — Total @item.Total</td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteInventoryItemAsync(item.Id))">Remover</MudButton></td>
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

`Ciclos` is a newly-added control — not present in the current file — closing the second "backend wired, no UI" gap identified in the spec. Note `Value="@_form.Ciclos"` binds a plain `int` into a `T="int?"` `Value` parameter — an implicit widening conversion, `_form.Ciclos` is deliberately *not* one of Task 1's nullable fields (the spec explicitly excludes it, matching Personagem's exclusion of its own `Ciclos`).

- [ ] **Step 6: Full-solution build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors — the whole page is now on MudBlazor.

- [ ] **Step 7: Manual/serve verification**

Run `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000` against a locally-served `dotnet run --project src/RuinaRPG.Client` (or an already-running dev server) to confirm the app still serves (`200`) — this only proves the static shell serves, since this is a Blazor WASM app with no server prerendering. If a browser/Playwright tool is available in this environment, load a real NPC sheet URL and confirm: no console errors across all 5 tabs; changing Cobertura on the Combate tab visibly updates Defesa Natural after the round-trip; changing Ciclos on the Posses tab persists (reload the page, confirm the value survived); and specifically reproduce Fase 1b's cross-tab crash shape — clear the "Nível" field on Tab 1 (don't submit), switch to Tab 3, change Cobertura — confirm the app shows the guard's error message instead of crashing. If no browser tool is available, say so explicitly rather than claiming a visual check you didn't do.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "feat: rewrite Ficha de NPC Combate, Magias & Habilidades, Posses on MudBlazor

Closes the MudBlazor migration for this page. Adds the two UI controls
(Cobertura on Combate, Ciclos on Posses) that FichaDeNpc.razor's
SheetFormModel/UpdateNpcSheetRequest already carried and applied but
never rendered anywhere — same gap Personagem already had closed.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Final whole-branch review

After Task 2, dispatch a final whole-branch review (opus, per this session's standing practice for this gate — see `MEMORY.md` → `ruina-plan-queue`) covering the full diff across both tasks, not just each task's own per-task review. If a headless-browser/Playwright tool is available to the reviewing agent, serve the client from the worktree behind a proxy to the running docker stack, provision throwaway GM + NPC + campaign data via the API, and empirically render the page in both light/dark themes, driving all 5 tabs and the two new controls' round-trips (Cobertura → Defesa Natural, Ciclos persistence) exactly as described in Task 2 Step 7 — every round on `FichaDePersonagem.razor` since Fase 1a found a real Critical/Important bug this way that per-task diff review missed, and this plan's two new live-update-calling-`SaveAsync` controls are exactly the shape that caused Fase 1b's crash. Delete the test data after. If no such tool is available, say so explicitly and fall back to `dotnet build` + line-by-line diff review against the conversion-rules table above, paying particular attention to the guard/Cobertura/Ciclos interaction called out in this plan.
