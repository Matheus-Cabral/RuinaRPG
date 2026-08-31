# Ficha de Personagem — Magias & Habilidades, Posses, Diário Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`'s remaining 3 tabs (Magias & Habilidades, Posses, Diário) onto MudBlazor, and close the one ledgered behavior gap from the Fase 1a review (15 `SheetFormModel` `int` fields silently persisting `0` when cleared instead of blocking submit).

**Architecture:** Tasks 2–4 are markup-only rewrites — same discipline as the Fase 1a plan (`docs/superpowers/plans/2026-08-30-ficha-de-personagem-basicas-atributos-combate.md`): no `@code` method's logic, signature, or DTO changes, only which component renders each field. Task 1 is the plan's one deliberate `@code` change — `SheetFormModel`'s 15 `int` fields become `int?` + `[Required]`, with `<DataAnnotationsValidator />` added to the Informações Básicas `EditForm` — isolated to lines already fully migrated in Fase 1a, non-overlapping with Tasks 2–4's lines.

**Tech Stack:** Blazor WebAssembly (.NET 8), MudBlazor 9.9.0, the Fundação plan's shared components (`Section`, `EntityPicker`), consumed as-is.

**Spec:** `docs/superpowers/specs/2026-08-31-ficha-de-personagem-magias-posses-diario-design.md` — authority for scope. Also see the umbrella spec `docs/superpowers/specs/2026-08-30-frontend-rebuild-fundacao-fichas-design.md`.

## Global Constraints

- No color/hex outside the existing `--rr-*` tokens; no font outside Cormorant Garamond/Inter (nothing in this plan introduces new CSS).
- `dotnet build RuinaRPG.sln` must stay at 0 warnings / 0 errors after every task.
- **No `@code` method's logic, signature, or the request/response DTOs it calls change in Tasks 2–4.** Every `Http.GetAsync`/`PostAsJsonAsync`/`PutAsJsonAsync`/`DeleteAsync` call, every `Load*Async`/reload-after-mutate pattern, and every nested `*FormModel` class (other than `SheetFormModel` in Task 1) stays exactly as it is today — only which UI component renders each field changes.
- All 4 tasks in this plan edit the same file (`FichaDePersonagem.razor`) in **non-overlapping line ranges** — Task 1 touches only the `SheetFormModel` class (~line 1045) and the Informações Básicas `EditForm` block (~lines 36–53); Tasks 2–4 touch only their own `MudTabPanel` (Magias & Habilidades, Posses, Diário respectively, in that order). Dispatch strictly sequentially within one worktree (never in parallel) — same discipline as Fase 1a, even though the ranges don't overlap, to keep diffs reviewable one at a time.
- `EntityPicker` usage (`@bind-Value`/`Value`+`ValueChanged`, `SearchItems`, `Placeholder` parameters) is unchanged — nothing in this plan touches its internals.
- `@using MudBlazor` and `@using RuinaRPG.Client.Shared.Fields` are already present at the top of the file (confirmed at lines 9–10) — no task needs to add them. Task 1 does add a new `@using System.ComponentModel.DataAnnotations` (not yet present in this file — confirmed absent from lines 1–10).

### Conversion rules (apply throughout Tasks 2–4; do not restate per task — identical to the Fase 1a plan's table)

| Old | New |
|---|---|
| `<InputText class="form-control" @bind-Value="X" />` preceded by a `<label>` | `<MudTextField T="string" @bind-Value="X" Label="..." />` (Label = the `<label>` text; drop the old `<label>`) |
| `<InputTextArea class="form-control" @bind-Value="X" />` / a raw `<textarea>` bound via `@bind` | `<MudTextField T="string" @bind-Value="X" Label="..." Lines="3" />` |
| `<InputNumber class="form-control" @bind-Value="X" />` (int, two-way bound to a form model field on a brand-new *add* form — not a persisted-resource field) | `<MudNumericField T="int" @bind-Value="X" Label="..." />` — a fresh add-form has no "cleared silently saves 0" risk the way a persisted resource does, so it stays non-nullable, same rule Fase 1a already used for this file's other add-forms |
| `<select class="form-select" @bind="X">` with `<option>`s | `<MudSelect T="string" @bind-Value="X" Label="...">` with one `<MudSelectItem Value="@("...")">...</MudSelectItem>` per option |
| A raw `<input>`/`<select>` bound via `value="@item.Field" @onchange="@(e => Method(..., e.Value))"` (one-way, live-update, not a form model) | `<MudNumericField T="int?" Value="@item.Field" ValueChanged="@(v => Method(..., v))" />` — `v` arrives already typed (`int?`), passing it into a method whose parameter is `object? value` is a normal implicit boxing conversion, so the method itself needs no change |
| `<button type="submit" class="btn btn-primary">Label</button>` | `<MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">Label</MudButton>` |
| `<button ... class="btn btn-outline-primary btn-sm" @onclick="...">Label</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="...">Label</MudButton>` |
| `<button ... class="btn btn-outline-secondary btn-sm" @onclick="...">Label</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Secondary" Size="Size.Small" OnClick="...">Label</MudButton>` |
| `<button ... class="btn btn-outline-danger btn-sm" @onclick="...">Remover</button>` | `<MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="...">Remover</MudButton>` |
| `<h3>Title</h3>` grouping a whole block | wrap the block in `<Section Title="Title">...</Section>` |
| `<h4>Subtitle</h4>` grouping a sub-block *inside* a `Section` (e.g. per-Tipo-de-Alvo grouping inside Artefatos, Positivas/Negativas inside Características) | `<MudText Typo="Typo.h6" Class="mt-3">Subtitle</MudText>` — no nested `Section` |
| `<table class="table">...` or `<ul class="list-unstyled"><li>...` (a list of items with a per-row action button) | `<MudSimpleTable Dense="true" Hover="true">` with a `<tbody>` `@foreach` producing one `<tr>` per item — one `<td>` per displayed field/text, one final `<td>` for action button(s). Matches the exact shape already used by the migrated Combate tab (`Armas`/`Armaduras`/`Escudos` — see `FichaDePersonagem.razor` lines 134–153 for the reference shape) |
| Page-level `@if (_errorMessage is not null) { <p class="error">@_errorMessage</p> }` | already `<MudAlert Severity="Severity.Error">` — untouched by this plan (Fase 1a already migrated it) |

---

## Task 1: `SheetFormModel` nullable-int fix

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` — top `@using` block (line 1–10), the `<EditForm Model="_form" OnValidSubmit="SaveAsync">` block (lines 36–53, Informações Básicas tab), `SaveAsync` (lines 655–671), `SheetFormModel` class (lines 1045–1079)

**Interfaces:**
- Consumes: `UpdateCharacterSheetRequest` (`src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs`) — unchanged, still takes non-nullable `int` for every one of these 15 positions.
- Produces: `SheetFormModel`'s 15 fields (`Nivel`, `ExperienciaAtual`, `PontosDeIgnicaoAtual`, `PontosDeIgnicaoTotal`, `NucleosRankF/E/D/C/B/A/S`, `AdrenalinaAtual`, `FocoAtual`, `EstresseAtual`, `VitalidadeAtual`) become `int?` — Tasks 2–4 don't reference any of them (they live entirely in Tab 1/Combate, already migrated), so nothing downstream needs updating, but note this for the task reviewer.

- [ ] **Step 1: Read the current state**

Read `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` lines 1–75 (top `@using`s + Informações Básicas tab), lines 655–686 (`SaveAsync`/`UpdateCoberturaAsync`/`UpdateCiclosAsync`), and lines 1045–1079 (`SheetFormModel`). Confirm the line numbers below still match — if Fase 1a's merge shifted them, adjust, but the content itself is exactly as shown.

- [ ] **Step 2: Add the `DataAnnotations` using**

At the top of the file, add one line after the existing `@using MudBlazor` (line 10):

```razor
@using System.ComponentModel.DataAnnotations
```

- [ ] **Step 3: Make the 15 fields nullable + required, in `SheetFormModel`**

Replace the class body (currently lines 1045–1079) with:

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
        public int EAPAtual { get; set; }
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

Note what did **not** change: `Graduacao`, `GraduacaoLabel`, `EAPAtual` (all read-only display fields, never bound via `MudNumericField`), `Cobertura` (string, its own `MudSelect` outside this `EditForm`, in the Combate tab), `Ciclos` (one-way live-update field in the Posses tab, out of scope per the spec), and the 4 `*Maximo` fields (read-only, rendered as `Adornment`/`AdornmentText`, never edited).

- [ ] **Step 4: Add `<DataAnnotationsValidator />` and wire `For=` on each affected `MudNumericField`**

Replace the `<EditForm Model="_form" OnValidSubmit="SaveAsync">` block (currently lines 36–53) with:

```razor
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
                <MudText>EAP Atual: @_form.EAPAtual</MudText>
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
```

- [ ] **Step 5: Update `SaveAsync` to unwrap the now-nullable fields**

Replace the body of `SaveAsync` (currently lines 655–671) with:

```csharp
    private async Task SaveAsync()
    {
        var request = new UpdateCharacterSheetRequest(null, _form.Nome, _form.Linhagem, _form.Variante, _form.Vocacao, _form.SubVocacao,
            _form.Afinidade, _form.Propriedade, _form.Nivel!.Value, _form.PossuiCoracaoDeMana, _form.ExperienciaAtual!.Value, _form.EAPAtual,
            _form.NucleosRankF!.Value, _form.NucleosRankE!.Value, _form.NucleosRankD!.Value, _form.NucleosRankC!.Value, _form.NucleosRankB!.Value, _form.NucleosRankA!.Value, _form.NucleosRankS!.Value,
            _form.PontosDeIgnicaoAtual!.Value, _form.PontosDeIgnicaoTotal!.Value, _form.VitalidadeAtual!.Value, _form.FocoAtual!.Value, _form.AdrenalinaAtual!.Value, _form.EstresseAtual!.Value,
            _form.Cobertura, _form.Ciclos);

        var response = await Http.PutAsJsonAsync($"character-sheets/{SheetId}", request);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a ficha.";
            return;
        }

        await LoadSheetAsync();
    }
```

The `!.Value` is deliberate, not a defensive `?? 0`: `OnValidSubmit` only invokes `SaveAsync` once `DataAnnotationsValidator` has confirmed every `[Required]` field is non-null, and `UpdateCoberturaAsync`/`UpdateCiclosAsync` (the Combate/Posses tabs' own callers of `SaveAsync`, unaffected by this task) only fire after `LoadSheetAsync` has already populated all 15 fields from a real sheet. If that invariant is ever violated, `!.Value` throwing `InvalidOperationException` is the correct failure — it means a real bug elsewhere, and a silent `?? 0` would instead persist wrong data.

`LoadSheetAsync` (lines 596–640) needs **no change**: every assignment like `_form.Nivel = sheet.Nivel;` assigns a non-nullable `int` (from `CharacterSheetResponse`) into now-nullable `int?` fields, which is an implicit widening conversion.

- [ ] **Step 6: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors. This task's edit is self-contained (Tab 1 + `@code` only, no interaction with Tabs 4–6's still-unmigrated markup), so a clean build is expected here, unlike Fase 1a's intermediate tasks.

- [ ] **Step 7: Manual verification**

Run `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000` against a locally-served `dotnet run --project src/RuinaRPG.Client` (or the equivalent already-running dev server) to confirm the app still serves (`200`). If a browser/Playwright tool is available in this environment, load a real character sheet's Informações Básicas tab, clear the "Nível" field, and confirm: (a) a validation message appears under the field, (b) clicking "Salvar" does not fire a PUT request (check dev tools network tab) and does not clear/reset any other field's displayed value. If no browser tool is available, say so explicitly rather than claiming a visual check you didn't do — this is the same disclosure discipline every prior round in this plan sequence has followed.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "fix: make SheetFormModel's 15 resource fields nullable + required

Clearing a MudNumericField T=\"int\" boxes default(int) (0) into
@bind-Value, and OnValidSubmit's happy-path build of
UpdateCharacterSheetRequest would have silently persisted that 0 —
the old raw InputNumber<int> blocked the whole submit via EditContext
validation instead. Ledgered by the Fase 1a final review.

Switch the 15 affected SheetFormModel fields to int? + [Required],
add DataAnnotationsValidator to the EditForm, and wire For= on each
MudNumericField so a cleared field shows a validation message and
blocks submit again, matching the old behavior. SaveAsync uses
!.Value once OnValidSubmit has already guaranteed non-null.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 2: "Magias & Habilidades" tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` — the `<MudTabPanel Text="Magias & Habilidades">` block (currently lines 228–384, including the now-stale migration-note comment at lines 229–231, which this task removes since it's the tab being migrated)

**Interfaces:**
- Consumes: `_racialAbility` (`RacialAbilityResponse?`), `_spellAbilities` (`List<CharacterSpellAbilityResponse>`), `_bankEntries` (`List<SpellAbilityEntryResponse>`), `_spellAbilityForm` (`SpellAbilityFormModel`), `_runes` (`List<CharacterRuneResponse>`), `_runeForm` (`RuneFormModel`), `_masteries` (`List<CharacterMasteryResponse>`), `_masteryForm` (`MasteryFormModel`), `_skills` (`List<CharacterSkillResponse>`, already loaded by Tab 2's `LoadTabs2And3Async`), `_attributes` (`List<CharacterAttributeResponse>`, same) — all unchanged fields/models, all already populated by `OnInitializedAsync`.
- Produces: nothing new — this task only changes markup inside this one `MudTabPanel`.

- [ ] **Step 1: Read the current tab's markup**

Read `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` lines 228–384 (the full current tab) to confirm nothing has shifted since this plan was written.

- [ ] **Step 2: Rewrite the tab**

Replace the entire `<MudTabPanel Text="Magias & Habilidades">...</MudTabPanel>` block with:

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

- [ ] **Step 3: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors — Task 1 already left the file in a clean-building state, and this task doesn't touch the still-unmigrated Posses/Diário tabs' markup, only its own already-`MudTabPanel`-wrapped block.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "feat: rewrite Ficha de Personagem Magias & Habilidades tab on MudBlazor

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 3: "Posses" tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` — the `<MudTabPanel Text="Posses">` block (line range shifted by Task 2's edit — re-read before editing; content is exactly the original lines 386–474)

**Interfaces:**
- Consumes: `_form.Ciclos` (`int`, unchanged), `UpdateCiclosAsync(object? value)` (unchanged signature), `_inventory`/`_inventoryForm`, `_artifacts`/`_artifactForm`, `_affections`/`_affectionForm`, `_traits`/`_traitForm` — all unchanged.
- Produces: nothing new.

- [ ] **Step 1: Read the current tab's markup**

Read the file's current `<MudTabPanel Text="Posses">` block in full (search for `Text="Posses"` — Task 2 will have shifted its line numbers).

- [ ] **Step 2: Rewrite the tab**

Replace the entire `<MudTabPanel Text="Posses">...</MudTabPanel>` block with:

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
```

Note the `Value="@_form.Ciclos"` on the `Ciclos` field: `_form.Ciclos` stays a plain `int` (not one of Task 1's 15 nullable fields, per the spec's explicit exclusion) — assigning it into a `T="int?"` `Value` parameter is an implicit `int` → `int?` widening conversion, no cast needed.

- [ ] **Step 3: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "feat: rewrite Ficha de Personagem Posses tab on MudBlazor

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 4: "Diário" tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` — the `<MudTabPanel Text="Diário">` block (line range shifted by Tasks 2–3's edits — re-read before editing; content is exactly the original lines 476–503)

**Interfaces:**
- Consumes: `_diaryEntries` (`List<DiaryEntryResponse>`), `_diaryForm` (`DiaryFormModel`), `_editingDiaryEntryId` (`string?`), `_editDiaryText` (`string`), `AddDiaryEntryAsync`/`StartEditDiaryEntry`/`CancelEditDiaryEntry`/`SaveDiaryEntryAsync`/`DeleteDiaryEntryAsync` — all unchanged.
- Produces: nothing new. This is the plan's last task — after it, the whole page is on MudBlazor.

- [ ] **Step 1: Read the current tab's markup**

Read the file's current `<MudTabPanel Text="Diário">` block in full (search for `Text="Diário"`).

- [ ] **Step 2: Rewrite the tab**

Replace the entire `<MudTabPanel Text="Diário">...</MudTabPanel>` block (and the immediately-following closing `</MudTabs>` stays exactly where it is, untouched) with:

```razor
    <MudTabPanel Text="Diário">
        <EditForm Model="_diaryForm" OnValidSubmit="AddDiaryEntryAsync">
            <MudTextField T="string" @bind-Value="_diaryForm.Texto" Label="Texto" Lines="3" />
            <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Adicionar Entrada</MudButton>
        </EditForm>
        <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
            <tbody>
                @foreach (var entry in _diaryEntries)
                {
                    <tr>
                        <td>
                            @if (_editingDiaryEntryId == entry.Id)
                            {
                                <MudTextField T="string" @bind-Value="_editDiaryText" Lines="3" />
                                <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="@(() => SaveDiaryEntryAsync(entry.Id))">Salvar</MudButton>
                                <MudButton Variant="Variant.Outlined" Color="Color.Secondary" Size="Size.Small" OnClick="CancelEditDiaryEntry">Cancelar</MudButton>
                            }
                            else
                            {
                                <MudText>@entry.CreatedAt.ToString("g") — @entry.Texto</MudText>
                                <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="@(() => StartEditDiaryEntry(entry))">Editar</MudButton>
                                <MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteDiaryEntryAsync(entry.Id))">Excluir</MudButton>
                            }
                        </td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>
    </MudTabPanel>
```

- [ ] **Step 3: Full-solution build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors — every tab on this page is now on MudBlazor.

- [ ] **Step 4: Manual/serve verification**

Run `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000` against a locally-served `dotnet run --project src/RuinaRPG.Client` (or the equivalent already-running dev server) to confirm the app still serves (`200`) — this only proves the static shell serves, since this is a Blazor WASM app with no server prerendering. If a browser/Playwright tool is available in this environment, load a real character sheet URL, click through all 6 tabs, and confirm: no console errors, Magias & Habilidades' Origem toggle switches its conditional fields correctly, adding/removing an Efeito row in the add-form works, Diário's inline edit doesn't leak `_editDiaryText` state between different entries (edit entry A, click Cancelar, then edit entry B — B's textarea should start with B's own text, not a stale value from A). If no browser tool is available, say so explicitly rather than claiming a visual check you didn't do.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "feat: rewrite Ficha de Personagem Diário tab on MudBlazor, close MudBlazor migration for this page

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Final whole-branch review

After Task 4, dispatch a final whole-branch review (opus, per this session's standing practice for this gate — see `MEMORY.md` → `ruina-plan-queue`) covering the full diff across all 4 tasks, not just each task's own per-task review. If a headless-browser/Playwright tool is available to the reviewing agent, use it to empirically render the page in both themes and drive the interactive bits called out in Task 1 Step 7 and Task 4 Step 4 — two prior rounds on this exact page (Fundação, Fase 1a) found real Critical/Important bugs this way that per-task diff review missed. If no such tool is available, say so explicitly and fall back to `dotnet build` + line-by-line diff review against the conversion-rules table above.
