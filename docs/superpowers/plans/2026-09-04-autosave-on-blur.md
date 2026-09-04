# Auto-save on Blur Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the explicit "Salvar" button on six edit forms (character/NPC/creature sheet "Informações Básicas" tabs, the Catálogo de Itens and Banco de Magias edit forms, and the Campanha "Detalhes" tab) with automatic saving triggered when a field commits its value (loses focus, or — for selects/checkboxes — changes), via a short shared debounce.

**Architecture:** Two new, small, reusable pieces (`AutoSaveCoordinator` — a plain per-page state/debounce class — and `AutoSaveIndicator` — a presentation-only Razor component) get wired into the six forms. Every field's existing `@bind-X="_form.Y"` syntax gains a matching `@bind-X:after="NotifySavedAsync"`, which debounces 400ms and then calls the page's existing save method (reshaped to validate-then-PUT-then-return-`bool`/throw). No backend change: every form keeps calling the exact endpoint and DTO it calls today.

**Tech Stack:** Blazor WebAssembly 8, MudBlazor 9.9.0, xUnit + bUnit (`RuinaRPG.Tests.Client`).

**Spec:** `docs/superpowers/specs/2026-09-04-autosave-on-blur-design.md`

## Global Constraints

- No backend/API changes of any kind — same endpoints, same request DTOs, same response shapes, for all six forms.
- Debounce window is 400ms, implemented as `Task.Delay(400, token)` + a stored `CancellationTokenSource` that gets cancelled and replaced on every call — the same idiom this codebase's search boxes already use (e.g. `BancoDeMagias.razor`'s `DebouncedLoadAsync`), not reinvented.
- `AutoSaveCoordinator` is a plain C# class, **not** registered in DI. Each page instantiates its own instance: `private readonly AutoSaveCoordinator _autoSave = new();`.
- Every field wired to auto-save uses `@bind-X:after="NotifySavedAsync"` on its existing `@bind-X` binding (works uniformly for native inputs and component parameters using the `XChanged` convention) — **not** a manually-added `OnBlur` parameter. This works because none of the six forms' fields set `Immediate="true"` (confirmed by grep — every place in this codebase that needs per-keystroke updates sets `Immediate="true"` explicitly, e.g. every search box; these forms don't, so MudBlazor's default already commits `@bind-Value` only on blur/Enter, which is exactly the trigger point required).
- `NotifySavedAsync` is one small helper method per page: `private Task NotifySavedAsync() { _autoSave.NotifyChanged(SaveIfValidAsync); return Task.CompletedTask; }` (exact name of the save delegate varies per page, see each task).
- The save delegate (`Func<Task<bool>>`) contract, identical across all six forms:
  - Returns `false` if validation blocks the save (no network call made, `AutoSaveCoordinator.State` returns to `Idle`).
  - Throws (any exception) if the PUT's `response.IsSuccessStatusCode` is `false` — `AutoSaveCoordinator` catches this and sets `State = Error`.
  - Returns `true` after a successful PUT — `AutoSaveCoordinator` sets `State = Saved` and `LastSavedAt = DateTime.Now`.
- Each page subscribes to `_autoSave.StateChanged` in `OnInitialized`/`OnInitializedAsync` to call `StateHasChanged()`, and unsubscribes in `Dispose()` (`@implements IDisposable`, same pattern `MainLayout.razor` already uses for its own event subscription).
- `<AutoSaveIndicator State="_autoSave.State" LastSavedAt="_autoSave.LastSavedAt" />` is placed directly below the converted form's `<DataAnnotationsValidator />` (or, where none exists, directly inside the `<EditForm>` before the first field).
- The `<MudButton ButtonType="Submit">Salvar</MudButton>` and the `EditForm`'s `OnValidSubmit="..."` attribute are removed from all six converted forms. The `<EditForm Model="_form">` tag itself stays (it still hosts `EditContext`/`DataAnnotationsValidator`/`ValidationMessage`).
- `CatalogoItemForm.razor` and `BancoDeMagiasForm.razor`'s local form models gain `DataAnnotations` attributes that are an exact copy of the constraints already declared on `UpdateItemRequest`/`UpdateSpellAbilityEntryRequest`/`SpellAbilityEffectRequest` in `RuinaRPG.Contracts` — no new validation rule is invented anywhere in this plan. `FichaDePersonagem`/`FichaDeNpc`/`FichaDeCriatura`'s form models and `CampanhaDetalhe`'s `CampaignDetailsFormModel` get **no** new attributes (their corresponding `Update*Request` DTOs carry none today); their only pre-save gate stays whatever manual check already exists in their current save method.
- `Navigation.NavigateTo(...)` is removed **only** from the edit-mode (PUT) branch of `CatalogoItemForm.razor`'s and `BancoDeMagiasForm.razor`'s save methods — navigating away after the very first field's auto-save would kick the user out of the page they're still editing. The create-mode (POST) branch is untouched and keeps navigating away after its own explicit "Salvar" click.
- Verification for tasks 3, 6, 7 and 8 (no new bUnit harness exists for these pages today, and standing one up from scratch for the sheet pages is out of proportion to this change): `dotnet build` must stay at 0 warnings/0 errors, and the existing integration tests for the corresponding controller (`tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`, `CharacterSheetsControllerTests.cs`, `NpcSheetsControllerTests.cs`, `CreatureSheetsControllerTests.cs` — confirm exact file names when you get to that task) must still pass unchanged, proving the endpoint contract these forms call is unaffected. Tasks 4 and 5 additionally get one new targeted bUnit test each (see those tasks) because they introduce genuinely new client-side validation logic worth locking down.

---

## Task 1: `AutoSaveCoordinator`

**Files:**
- Create: `src/RuinaRPG.Client/Services/AutoSaveCoordinator.cs`
- Test: `tests/RuinaRPG.Tests.Client/Services/AutoSaveCoordinatorTests.cs`

**Interfaces:**
- Produces: `namespace RuinaRPG.Client.Services;` → `public enum AutoSaveState { Idle, Saving, Saved, Error }` and `public class AutoSaveCoordinator` with public members `AutoSaveState State { get; }`, `DateTime? LastSavedAt { get; }`, `event Action? StateChanged;`, `void NotifyChanged(Func<Task<bool>> validateAndSaveAsync)`. Every later task consumes this exact surface.

- [ ] **Step 1: Write the failing tests**

```csharp
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class AutoSaveCoordinatorTests
{
    [Fact]
    public async Task Rapid_repeated_calls_collapse_into_a_single_save_after_the_debounce_window()
    {
        var coordinator = new AutoSaveCoordinator();
        var callCount = 0;
        Task<bool> Save() { callCount++; return Task.FromResult(true); }

        coordinator.NotifyChanged(Save);
        await Task.Delay(100);
        coordinator.NotifyChanged(Save);
        await Task.Delay(100);
        coordinator.NotifyChanged(Save);

        await Task.Delay(700);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task A_delegate_returning_false_leaves_state_Idle_and_does_not_set_LastSavedAt()
    {
        var coordinator = new AutoSaveCoordinator();

        coordinator.NotifyChanged(() => Task.FromResult(false));
        await Task.Delay(700);

        Assert.Equal(AutoSaveState.Idle, coordinator.State);
        Assert.Null(coordinator.LastSavedAt);
    }

    [Fact]
    public async Task A_delegate_that_throws_sets_state_Error()
    {
        var coordinator = new AutoSaveCoordinator();

        coordinator.NotifyChanged(() => throw new InvalidOperationException("boom"));
        await Task.Delay(700);

        Assert.Equal(AutoSaveState.Error, coordinator.State);
    }

    [Fact]
    public async Task A_delegate_returning_true_sets_state_Saved_and_LastSavedAt()
    {
        var coordinator = new AutoSaveCoordinator();
        var before = DateTime.Now;

        coordinator.NotifyChanged(() => Task.FromResult(true));
        await Task.Delay(700);

        Assert.Equal(AutoSaveState.Saved, coordinator.State);
        Assert.NotNull(coordinator.LastSavedAt);
        Assert.True(coordinator.LastSavedAt >= before);
    }

    [Fact]
    public async Task StateChanged_fires_at_least_once_as_the_save_completes()
    {
        var coordinator = new AutoSaveCoordinator();
        var fired = 0;
        coordinator.StateChanged += () => fired++;

        coordinator.NotifyChanged(() => Task.FromResult(true));
        await Task.Delay(700);

        Assert.True(fired > 0);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~AutoSaveCoordinatorTests`
Expected: build error (`AutoSaveCoordinator`/`AutoSaveState` don't exist yet) or FAIL.

- [ ] **Step 3: Write the implementation**

```csharp
namespace RuinaRPG.Client.Services;

public enum AutoSaveState { Idle, Saving, Saved, Error }

/// <summary>
/// Debounces a burst of field-commit events into one save call, 400ms after the last one. One
/// instance per page/form — not a DI service. See docs/superpowers/specs/2026-09-04-autosave-on-blur-design.md.
/// </summary>
public class AutoSaveCoordinator
{
    private const int DebounceMilliseconds = 400;
    private CancellationTokenSource? _debounceCts;

    public AutoSaveState State { get; private set; } = AutoSaveState.Idle;
    public DateTime? LastSavedAt { get; private set; }
    public event Action? StateChanged;

    /// <summary>
    /// Call from every in-scope field's commit (blur, or selection change for non-text controls).
    /// Resets the debounce window; the delegate only actually runs once no further call arrives
    /// within DebounceMilliseconds.
    /// </summary>
    public void NotifyChanged(Func<Task<bool>> validateAndSaveAsync)
    {
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = DebounceAndSaveAsync(validateAndSaveAsync, cts.Token);
    }

    private async Task DebounceAndSaveAsync(Func<Task<bool>> validateAndSaveAsync, CancellationToken token)
    {
        try
        {
            await Task.Delay(DebounceMilliseconds, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        State = AutoSaveState.Saving;
        StateChanged?.Invoke();

        try
        {
            var attempted = await validateAndSaveAsync();
            if (attempted)
            {
                State = AutoSaveState.Saved;
                LastSavedAt = DateTime.Now;
            }
            else
            {
                State = AutoSaveState.Idle;
            }
        }
        catch
        {
            State = AutoSaveState.Error;
        }

        StateChanged?.Invoke();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~AutoSaveCoordinatorTests`
Expected: PASS, 5/5.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Services/AutoSaveCoordinator.cs tests/RuinaRPG.Tests.Client/Services/AutoSaveCoordinatorTests.cs
git commit -m "feat: add AutoSaveCoordinator (debounced blur-triggered save)"
```

---

## Task 2: `AutoSaveIndicator.razor`

**Files:**
- Create: `src/RuinaRPG.Client/Shared/AutoSaveIndicator.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/AutoSaveIndicatorTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Client.Services.AutoSaveState` (Task 1).
- Produces: `<AutoSaveIndicator State="AutoSaveState" LastSavedAt="DateTime?" />`, consumed by every task from Task 3 onward.

- [ ] **Step 1: Write the failing tests**

```csharp
using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class AutoSaveIndicatorTests : MudBunitContext
{
    [Fact]
    public void Idle_renders_nothing()
    {
        var cut = Render<AutoSaveIndicator>(p => p.Add(x => x.State, AutoSaveState.Idle));

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void Saving_shows_the_in_progress_label()
    {
        var cut = Render<AutoSaveIndicator>(p => p.Add(x => x.State, AutoSaveState.Saving));

        cut.Markup.Should().Contain("Salvando...");
    }

    [Fact]
    public void Saved_shows_the_last_saved_time()
    {
        var cut = Render<AutoSaveIndicator>(p => p
            .Add(x => x.State, AutoSaveState.Saved)
            .Add(x => x.LastSavedAt, new DateTime(2026, 1, 1, 14, 32, 0)));

        cut.Markup.Should().Contain("Salvo às 14:32");
    }

    [Fact]
    public void Error_shows_the_failure_label()
    {
        var cut = Render<AutoSaveIndicator>(p => p.Add(x => x.State, AutoSaveState.Error));

        cut.Markup.Should().Contain("Erro ao salvar");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~AutoSaveIndicatorTests`
Expected: build error (component doesn't exist) or FAIL.

- [ ] **Step 3: Write the implementation**

```razor
@using RuinaRPG.Client.Services

@if (State != AutoSaveState.Idle)
{
    <MudText Typo="Typo.body2" Color="Color.Secondary" Class="rr-autosave-indicator">@Label</MudText>
}

@code {
    [Parameter, EditorRequired] public AutoSaveState State { get; set; }
    [Parameter] public DateTime? LastSavedAt { get; set; }

    private string Label => State switch
    {
        AutoSaveState.Saving => "Salvando...",
        AutoSaveState.Saved => $"Salvo às {LastSavedAt:HH:mm}",
        AutoSaveState.Error => "Erro ao salvar",
        _ => "",
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~AutoSaveIndicatorTests`
Expected: PASS, 4/4.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Shared/AutoSaveIndicator.razor tests/RuinaRPG.Tests.Client/Shared/AutoSaveIndicatorTests.cs
git commit -m "feat: add AutoSaveIndicator presentational component"
```

---

## Task 3: Convert `CampanhaDetalhe.razor`'s "Detalhes" tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`

**Interfaces:**
- Consumes: `AutoSaveCoordinator`, `AutoSaveState`, `AutoSaveIndicator` (Tasks 1-2).

This tab has no `DataAnnotationsValidator` today and `UpdateCampaignRequest` carries no `DataAnnotations` — no validation is added here, per Global Constraints.

- [ ] **Step 1: Add the field to `@code`, subscribe/unsubscribe, add `@implements IDisposable` if not already present**

Check first: `grep -n "@implements IDisposable" src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`. If absent, add `@implements IDisposable` near the top of the file (with the other `@using`/`@inject` lines) and add these members near `_detailsForm`:

```csharp
private readonly RuinaRPG.Client.Services.AutoSaveCoordinator _autoSave = new();

protected override void OnInitialized()
{
    _autoSave.StateChanged += StateHasChangedFromAutoSave;
}

private void StateHasChangedFromAutoSave() => InvokeAsync(StateHasChanged);

public void Dispose()
{
    _autoSave.StateChanged -= StateHasChangedFromAutoSave;
}
```

If the file already has an `OnInitialized`/`Dispose`/`IDisposable` (check first — some pages in this codebase already implement `IDisposable` for other reasons), add these lines into the existing methods instead of creating duplicates.

- [ ] **Step 2: Wire the two text fields and the image picker**

Replace:
```razor
                <MudTextField T="string" @bind-Value="_detailsForm.Nome" Label="Nome" />
                <MudTextField T="string" @bind-Value="_detailsForm.Descricao" Label="Descrição" Lines="3" />

                <ImageAttachmentField AvailableImages="_myImages" SelectedIds="_detailsImageIds"
                                      SelectedIdsChanged="@(v => { _detailsImageIds = v; _detailsForm.ImageId = v.FirstOrDefault(); })"
                                      OnError="@(e => _errorMessage = e)" Multiple="false" />

                <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Salvar</MudButton>
```
with:
```razor
                <AutoSaveIndicator State="_autoSave.State" LastSavedAt="_autoSave.LastSavedAt" />
                <MudTextField T="string" @bind-Value="_detailsForm.Nome" Label="Nome" @bind-Value:after="NotifySavedAsync" />
                <MudTextField T="string" @bind-Value="_detailsForm.Descricao" Label="Descrição" Lines="3" @bind-Value:after="NotifySavedAsync" />

                <ImageAttachmentField AvailableImages="_myImages" SelectedIds="_detailsImageIds"
                                      SelectedIdsChanged="@(v => { _detailsImageIds = v; _detailsForm.ImageId = v.FirstOrDefault(); NotifySavedAsync(); })"
                                      OnError="@(e => _errorMessage = e)" Multiple="false" />
```
and remove the `OnValidSubmit="SaveDetailsAsync"` attribute from the enclosing `<EditForm Model="_detailsForm">` tag (keep the tag itself).

- [ ] **Step 3: Reshape `SaveDetailsAsync` into `SaveIfValidAsync` and add `NotifySavedAsync`**

Replace:
```csharp
    private async Task SaveDetailsAsync()
    {
        var response = await Http.PutAsJsonAsync($"campaigns/{CampaignId}", new UpdateCampaignRequest(_detailsForm.Nome, _detailsForm.Descricao, _detailsForm.ImageId));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a campanha.";
            return;
        }

        _errorMessage = null;
        await LoadCampaignNameAsync();
    }
```
with:
```csharp
    private async Task<bool> SaveIfValidAsync()
    {
        var response = await Http.PutAsJsonAsync($"campaigns/{CampaignId}", new UpdateCampaignRequest(_detailsForm.Nome, _detailsForm.Descricao, _detailsForm.ImageId));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Não foi possível salvar a campanha.");

        await LoadCampaignNameAsync();
        return true;
    }

    private Task NotifySavedAsync()
    {
        _autoSave.NotifyChanged(SaveIfValidAsync);
        return Task.CompletedTask;
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: 0 Warning(s), 0 Error(s).

- [ ] **Step 5: Verify the endpoint contract is unaffected**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~CampaignsController` (confirm the exact test class name for the campaigns controller with `grep -rl "class.*ControllerTests" tests/RuinaRPG.Tests.Integration/Controllers | grep -i campaign` if this filter doesn't match).
Expected: PASS, unchanged from before this task.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor
git commit -m "feat: auto-save the campaign Detalhes tab on blur"
```

---

## Task 4: Convert `CatalogoItemForm.razor` (edit mode only)

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`
- Test: `tests/RuinaRPG.Tests.Client/Pages/CatalogoItemFormTests.cs` (new — first bUnit test for this page)

**Interfaces:**
- Consumes: `AutoSaveCoordinator`, `AutoSaveState`, `AutoSaveIndicator` (Tasks 1-2), `RuinaRPG.Tests.Client.Shared.FakeHttpMessageHandler` (existing).

**Global Constraints reminder:** create mode (`ItemId is null`) keeps its explicit "Salvar" button, its `POST` call, and its `Navigation.NavigateTo("/catalogo")`. Only the edit-mode (`ItemId is not null`) path converts. Since both modes share one `<EditForm>`, the button and `OnValidSubmit` are only removed for the case that matters — see Step 3.

**Side effect worth naming, not asking about:** this file has no `<DataAnnotationsValidator />` at all today, so adding one (Step 2) and calling `_editContext.Validate()` in `CreateAsync` (Step 3) means create mode starts validating too, which it never did before. This is intentional and in-bounds: it enforces the exact same `[Range]` constraints the server already rejects a bad `Peso`/`Preco`/`DurabilidadeMaxima` with today (via `[ApiController]`'s automatic model validation on `CreateItemRequest`) — just earlier, in the UI, with the same field highlighting edit mode gets. No new constraint value is introduced; don't treat this as scope creep to revert.

- [ ] **Step 1: Add `[Range]` to `ItemFormModel`, mirroring `UpdateItemRequest` exactly**

`UpdateItemRequest`/`CreateItemRequest` declare: `Peso` → `[Range(typeof(decimal), "0", "79228162514264337593543950335")]`, `Preco` → `[Range(0, int.MaxValue)]`, `DurabilidadeMaxima` → `[Range(0, int.MaxValue)]` (nullable). `Nome` has **no** attribute in either DTO — do not add one.

```csharp
    private class ItemFormModel
    {
        public string Tipo { get; set; } = "ItemGeral";
        public string Nome { get; set; } = "";
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal Peso { get; set; }
        [Range(0, int.MaxValue)]
        public int Preco { get; set; }
        public string? ImageId { get; set; }
        public string? Subcategoria { get; set; }
        public string? Descricao { get; set; }
        public string? Tier { get; set; }
        public string? Empunhadura { get; set; }
        public string? Dados { get; set; }
        public int? Dano { get; set; }
        public string? Critico { get; set; }
        public int? Alcance { get; set; }
        public string? TipoDeDano { get; set; }
        public string? RequisitoAtributo { get; set; }
        [Range(0, int.MaxValue)]
        public int? DurabilidadeMaxima { get; set; }
        public string? Categoria { get; set; }
        public int? Defesa { get; set; }
        public int? RF { get; set; }
        public int? RM { get; set; }
        public string? Penalidade { get; set; }
        public int? RequisitoVigor { get; set; }
        public int? BonusDefesa { get; set; }
        public string? TipoDeAlvo { get; set; }
        public string? Alvo { get; set; }
        public int? Valor { get; set; }
    }
```
Add `@using System.ComponentModel.DataAnnotations` near the top of the file if not already present (check first).

- [ ] **Step 2: Add `<DataAnnotationsValidator />`, `<AutoSaveIndicator>`, `For="..."` on the annotated fields, and `:after` on every field**

Replace the `<EditForm>` opening and the "Geral" section:
```razor
<EditForm Model="_form" OnValidSubmit="SubmitAsync">
    <Section Title="Geral">
        @if (ItemId is null)
        {
            <MudSelect T="string" @bind-Value="_form.Tipo" Label="Tipo">
```
with:
```razor
<EditForm EditContext="_editContext">
    <DataAnnotationsValidator />
    @if (ItemId is not null)
    {
        <AutoSaveIndicator State="_autoSave.State" LastSavedAt="_autoSave.LastSavedAt" />
    }
    <Section Title="Geral">
        @if (ItemId is null)
        {
            <MudSelect T="string" @bind-Value="_form.Tipo" Label="Tipo">
```
**Important:** use `EditContext="_editContext"`, not `Model="_form"`. `<DataAnnotationsValidator />` and every field's `For="..."` read validation errors from whichever `EditContext` instance is cascaded down from `<EditForm>` — if `<EditForm Model="_form">` is left as-is, it silently creates its own internal `EditContext` that is a *different instance* from the `_editContext` field `SaveIfValidAsync`/`CreateAsync` call `.Validate()` on in Step 3, so field-level red error text would never appear even though the save is correctly blocked. `EditForm` accepts exactly one of `Model` or `EditContext` — passing `EditContext` here makes the manually-constructed instance in Step 3 the single source of truth used everywhere (validator, fields, and code).
Then, in every section, append `@bind-Value:after="NotifySavedAsync"` to the existing `@bind-Value` on: `_form.Nome`, `_form.Peso` (also add `For="@(() => _form.Peso)"`), `_form.Preco` (also add `For="@(() => _form.Preco)"`), `_form.Subcategoria`, `_form.Descricao`, `_form.Tier`, `_form.Empunhadura`, `_form.Dados`, `_form.Dano`, `_form.Critico`, `_form.Alcance`, `_form.TipoDeDano`, `_form.RequisitoAtributo`, `_form.DurabilidadeMaxima` (also add `For="@(() => _form.DurabilidadeMaxima)"`), `_form.Categoria`, `_form.Defesa`, `_form.RF`, `_form.RM`, `_form.Penalidade`, `_form.RequisitoVigor`, `_form.BonusDefesa`, `_form.TipoDeAlvo`, `_form.Alvo`, `_form.Valor`. Example for `Peso`:
```razor
<MudNumericField T="decimal" @bind-Value="_form.Peso" For="@(() => _form.Peso)" Label="Peso" @bind-Value:after="NotifySavedAsync" />
```
Update the `ImageAttachmentField` callback:
```razor
        <ImageAttachmentField AvailableImages="_myImages" SelectedIds="_imageIds"
                              SelectedIdsChanged="@(v => { _imageIds = v; _form.ImageId = v.FirstOrDefault(); if (ItemId is not null) NotifySavedAsync(); })"
                              OnError="@(e => _errorMessage = e)" Multiple="false" />
```
Replace the trailing button:
```razor
    @if (ItemId is null)
    {
        <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Salvar</MudButton>
    }
</EditForm>
```
Since `OnValidSubmit` is gone from the `EditForm` but create mode still needs an explicit submit action, give the create-mode button an `OnClick` instead of relying on form submission:
```razor
    @if (ItemId is null)
    {
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-3" OnClick="CreateAsync">Salvar</MudButton>
    }
</EditForm>
```

- [ ] **Step 3: Split `SubmitAsync` into `CreateAsync` (create mode, unchanged behavior) and `SaveIfValidAsync` (edit mode, auto-save target)**

Replace:
```csharp
    private async Task SubmitAsync()
    {
        HttpResponseMessage response;
        if (ItemId is null)
        {
            var request = new CreateItemRequest(_form.Tipo, _form.Nome, _form.Peso, _form.Preco, _form.ImageId,
                _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
                _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
                _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
                _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor);
            response = await Http.PostAsJsonAsync("items", request);
        }
        else
        {
            var request = new UpdateItemRequest(_form.Nome, _form.Peso, _form.Preco, _form.ImageId,
                _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
                _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
                _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
                _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor);
            response = await Http.PutAsJsonAsync($"items/{ItemId}", request);
        }

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar o item.";
            return;
        }

        Navigation.NavigateTo("/catalogo");
    }
```
with:
```csharp
    private async Task CreateAsync()
    {
        if (!_editContext.Validate())
            return;

        var request = new CreateItemRequest(_form.Tipo, _form.Nome, _form.Peso, _form.Preco, _form.ImageId,
            _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
            _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
            _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
            _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor);
        var response = await Http.PostAsJsonAsync("items", request);

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar o item.";
            return;
        }

        Navigation.NavigateTo("/catalogo");
    }

    private async Task<bool> SaveIfValidAsync()
    {
        if (!_editContext.Validate())
            return false;

        var request = new UpdateItemRequest(_form.Nome, _form.Peso, _form.Preco, _form.ImageId,
            _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
            _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
            _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
            _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor);
        var response = await Http.PutAsJsonAsync($"items/{ItemId}", request);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Não foi possível salvar o item.");

        return true;
    }

    private Task NotifySavedAsync()
    {
        _autoSave.NotifyChanged(SaveIfValidAsync);
        return Task.CompletedTask;
    }
```
`_editContext` needs to exist: add `private EditContext _editContext = null!;` and, in `OnInitializedAsync` (after `_form` is fully populated for edit mode — for create mode there's nothing to load, so set it immediately), `_editContext = new EditContext(_form);`. This must run before first render reaches the markup from Step 2 (i.e. before the `@if`/`<EditForm>` block), which `OnInitializedAsync` already satisfies. Also add `private readonly AutoSaveCoordinator _autoSave = new();`, `@implements IDisposable`, and the same `OnInitialized`/`Dispose` subscribe/unsubscribe pattern as Task 3's Step 1.

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: 0 Warning(s), 0 Error(s).

- [ ] **Step 5: Write the new bUnit test**

```csharp
using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class CatalogoItemFormTests : MudBunitContext
{
    [Fact]
    public async Task An_out_of_range_Preco_blocks_the_save_call_in_edit_mode()
    {
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("items"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "item-1", Tipo = "ItemGeral", Nome = "Poção", ImageUrl = (string?)null, Peso = 1m, Preco = 10,
                          Subcategoria = (string?)null, Descricao = (string?)null, Tier = (string?)null, Empunhadura = (string?)null,
                          Dados = (string?)null, Dano = (int?)null, Critico = (string?)null, Alcance = (int?)null, TipoDeDano = (string?)null,
                          RequisitoAtributo = (string?)null, DurabilidadeMaxima = (int?)null, Categoria = (string?)null, Defesa = (int?)null,
                          RF = (int?)null, RM = (int?)null, Penalidade = (string?)null, RequisitoVigor = (int?)null, BonusDefesa = (int?)null,
                          TipoDeAlvo = (string?)null, Alvo = (string?)null, Valor = (int?)null }
                }) };
            if (request.Method == HttpMethod.Put)
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.ItemId, "item-1"));
        await Task.Delay(50); // let OnInitializedAsync finish populating _form

        var preco = cut.FindComponents<MudBlazor.MudNumericField<int>>().Single(c => c.Instance.Label == "Preço (Ciclos)");
        await cut.InvokeAsync(() => preco.Instance.ValueChanged.InvokeAsync(-5));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeFalse("a negative Preço violates [Range(0, int.MaxValue)] and must not reach the server");
    }
}
```

Note for whoever implements this: `RuinaRPG.Tests.Client` doesn't reference `RuinaRPG.Client.Pages` types today only because no page has been tested yet — confirm the namespace/using compiles, and adjust the exact `ItemResponse` anonymous-object shape above to match the real `ItemResponse` contract in `src/RuinaRPG.Contracts/Items/ItemResponse.cs` if its field list differs from what's listed here (read that file before finalizing this test).

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~CatalogoItemFormTests`
Expected: PASS.

- [ ] **Step 7: Verify the endpoint contract is unaffected**

Run the existing integration tests for the items controller (find the exact file with `grep -rl "class.*ControllerTests" tests/RuinaRPG.Tests.Integration/Controllers | grep -i item`).
Expected: PASS, unchanged.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CatalogoItemForm.razor tests/RuinaRPG.Tests.Client/Pages/CatalogoItemFormTests.cs
git commit -m "feat: auto-save Catálogo item edits on blur, keep manual create"
```

---

## Task 5: Convert `BancoDeMagiasForm.razor` (edit mode only)

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`
- Test: `tests/RuinaRPG.Tests.Client/Pages/BancoDeMagiasFormTests.cs` (new)

**Interfaces:**
- Consumes: `AutoSaveCoordinator`, `AutoSaveState`, `AutoSaveIndicator` (Tasks 1-2).

Same create/edit split as Task 4. `UpdateSpellAbilityEntryRequest` already declares `[Required(AllowEmptyStrings = false)]` on `Nome` and `[Range(0, int.MaxValue)]` on `Grau`; `SpellAbilityEffectRequest` declares `[Range(0, int.MaxValue)]` on `CustoPI`. `Tipo`, `Descricao`, and `EfeitoNome`/`Quantidade` carry no attribute in either DTO — do not add one.

- [ ] **Step 1: Add attributes to `EntryFormModel`/`EffectFormModel`**

```csharp
    private class EntryFormModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Nome { get; set; } = "";
        public string Tipo { get; set; } = "Magia";
        [Range(0, int.MaxValue)]
        public int Grau { get; set; }
        public string Descricao { get; set; } = "";
        public List<EffectFormModel> Efeitos { get; set; } = new();
    }

    private class EffectFormModel
    {
        public string EfeitoNome { get; set; } = "";
        public int? Quantidade { get; set; }
        [Range(0, int.MaxValue)]
        public int CustoPI { get; set; }
    }
```
This file does not have `@using System.ComponentModel.DataAnnotations` yet (confirmed by grep during planning — `<DataAnnotationsValidator />`'s own type doesn't require it, only the `[Range]`/`[Required]` attributes being added here do) — add it near the top of the file.

- [ ] **Step 2: Add `<AutoSaveIndicator>`, `For="..."`, and `:after` wiring**

`<DataAnnotationsValidator />` is already present — add the indicator right after it (only in edit mode), and switch to `EditContext="_editContext"` for the same reason explained in Task 4's Step 2 (a `Model="_form"` `EditForm` would otherwise create its own separate `EditContext` instance that `SaveIfValidAsync`/`CreateAsync`'s `.Validate()` calls in Step 3 never touch, silently breaking field-level error display):
```razor
<EditForm EditContext="_editContext">
    <DataAnnotationsValidator />
    @if (EntryId is not null)
    {
        <AutoSaveIndicator State="_autoSave.State" LastSavedAt="_autoSave.LastSavedAt" />
    }
    <Section Title="Geral">
        <MudTextField T="string" @bind-Value="_form.Nome" For="@(() => _form.Nome)" Label="Nome" @bind-Value:after="NotifySavedAsync" />
        <MudSelect T="string" @bind-Value="_form.Tipo" Label="Tipo" @bind-Value:after="NotifySavedAsync">
            <MudSelectItem Value="@("Magia")">Magia</MudSelectItem>
            <MudSelectItem Value="@("Habilidade")">Habilidade</MudSelectItem>
            <MudSelectItem Value="@("Racial")">Racial</MudSelectItem>
        </MudSelect>
        <MudNumericField T="int" @bind-Value="_form.Grau" For="@(() => _form.Grau)" Label="Grau" @bind-Value:after="NotifySavedAsync" />
        <MudTextField T="string" @bind-Value="_form.Descricao" Label="Descrição" Lines="3" @bind-Value:after="NotifySavedAsync" />
    </Section>
```
Remove `OnValidSubmit="SubmitAsync"` from the `<EditForm>` tag and switch `Model="_form"` to `EditContext="_editContext"` as shown above.

For the dynamic Efeitos rows, wire each cell and the Remover button:
```razor
                @for (var i = 0; i < _form.Efeitos.Count; i++)
                {
                    var index = i; // capture for the closures below
                    <tr>
                        <td><MudTextField T="string" @bind-Value="_form.Efeitos[index].EfeitoNome" @bind-Value:after="NotifySavedAsync" /></td>
                        <td><MudNumericField T="int?" @bind-Value="_form.Efeitos[index].Quantidade" @bind-Value:after="NotifySavedAsync" /></td>
                        <td><MudNumericField T="int" @bind-Value="_form.Efeitos[index].CustoPI" For="@(() => _form.Efeitos[index].CustoPI)" @bind-Value:after="NotifySavedAsync" /></td>
                        <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => RemoveEffect(index))">Remover</MudButton></td>
                    </tr>
                }
```
(Note: `For="@(...)"` on an indexer expression like `_form.Efeitos[index].CustoPI` may not compile as a `MemberExpression` MudBlazor's validation can walk — if `dotnet build` fails on that specific `For` attribute in Step 4, remove that one `For` and keep only the `Range` attribute + `@bind-Value:after`; the `<DataAnnotationsValidator/>`'s summary-level validation still blocks the save either way, it just won't highlight that specific cell.)

Replace the trailing button, same shape as Task 4:
```razor
    @if (EntryId is null)
    {
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-3" OnClick="CreateAsync">Salvar</MudButton>
    }
</EditForm>
```

- [ ] **Step 3: Add `RemoveEffect`, split `SubmitAsync`, add `NotifySavedAsync`/`_autoSave`/`_editContext`**

Replace:
```csharp
    private void AddEffect() => _form.Efeitos.Add(new EffectFormModel());

    private async Task SubmitAsync()
    {
        var efeitos = _form.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();

        HttpResponseMessage response = EntryId is null
            ? await Http.PostAsJsonAsync("spell-ability-bank", new CreateSpellAbilityEntryRequest(_form.Nome, _form.Tipo, _form.Grau, _form.Descricao, efeitos))
            : await Http.PutAsJsonAsync($"spell-ability-bank/{EntryId}", new UpdateSpellAbilityEntryRequest(_form.Nome, _form.Tipo, _form.Grau, _form.Descricao, efeitos));

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a entrada.";
            return;
        }

        Navigation.NavigateTo("/banco-de-magias");
    }
```
with:
```csharp
    private void AddEffect() => _form.Efeitos.Add(new EffectFormModel());

    private void RemoveEffect(int index)
    {
        _form.Efeitos.RemoveAt(index);
        if (EntryId is not null)
            NotifySavedAsync();
    }

    private async Task CreateAsync()
    {
        if (!_editContext.Validate())
            return;

        var efeitos = _form.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();
        var response = await Http.PostAsJsonAsync("spell-ability-bank", new CreateSpellAbilityEntryRequest(_form.Nome, _form.Tipo, _form.Grau, _form.Descricao, efeitos));

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a entrada.";
            return;
        }

        Navigation.NavigateTo("/banco-de-magias");
    }

    private async Task<bool> SaveIfValidAsync()
    {
        if (!_editContext.Validate())
            return false;

        var efeitos = _form.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();
        var response = await Http.PutAsJsonAsync($"spell-ability-bank/{EntryId}", new UpdateSpellAbilityEntryRequest(_form.Nome, _form.Tipo, _form.Grau, _form.Descricao, efeitos));

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Não foi possível salvar a entrada.");

        return true;
    }

    private Task NotifySavedAsync()
    {
        _autoSave.NotifyChanged(SaveIfValidAsync);
        return Task.CompletedTask;
    }
```
Add `private EditContext _editContext = null!;` (set to `new EditContext(_form)` at the end of `OnInitializedAsync`, after `_form` is populated for edit mode — for create mode, set it right away since there's nothing to load — this must happen before first render, which `OnInitializedAsync` satisfies), `private readonly AutoSaveCoordinator _autoSave = new();`, `@implements IDisposable`, and the same subscribe/unsubscribe pattern as Task 3's Step 1.

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: 0 Warning(s), 0 Error(s). If the `For` on `_form.Efeitos[index].CustoPI` fails to compile, apply the fallback described in Step 2's note and rebuild.

- [ ] **Step 5: Write the new bUnit test**

```csharp
using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class BancoDeMagiasFormTests : MudBunitContext
{
    [Fact]
    public async Task Clearing_the_required_Nome_field_blocks_the_save_call_in_edit_mode()
    {
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "entry-1", Nome = "Bola de Fogo", Tipo = "Magia", Grau = 1, Descricao = "",
                          Efeitos = new List<object>() }
                }) };
            if (request.Method == HttpMethod.Put)
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeMagiasForm>(p => p.Add(x => x.EntryId, "entry-1"));
        await Task.Delay(50); // let OnInitializedAsync finish populating _form

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync(""));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeFalse("an empty Nome violates [Required] and must not reach the server");
    }
}
```

Note for whoever implements this: confirm the real `SpellAbilityEntryResponse` shape in `src/RuinaRPG.Contracts/SpellsAndAbilities/SpellAbilityEntryResponse.cs` before finalizing the anonymous object above — adjust field names/types to match exactly.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~BancoDeMagiasFormTests`
Expected: PASS.

- [ ] **Step 7: Verify the endpoint contract is unaffected**

Run the existing integration tests for the spell-ability-bank controller (`grep -rl "class.*ControllerTests" tests/RuinaRPG.Tests.Integration/Controllers | grep -i spell`).
Expected: PASS, unchanged.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor tests/RuinaRPG.Tests.Client/Pages/BancoDeMagiasFormTests.cs
git commit -m "feat: auto-save Banco de Magias entry edits on blur, keep manual create"
```

---

## Task 6: Convert `FichaDePersonagem.razor`'s "Informações Básicas" tab (+ the "Combate" tab's 4 fields that share the same PUT)

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`

**Interfaces:**
- Consumes: `AutoSaveCoordinator`, `AutoSaveState`, `AutoSaveIndicator` (Tasks 1-2).

**Important scope note, confirmed by reading the file:** `_form.VitalidadeAtual`, `_form.FocoAtual`, `_form.AdrenalinaAtual`, and `_form.EstresseAtual` are edited in the **separate "Combate" tab** (around line 274), not inside the "Informações Básicas" `<EditForm>` — but they're part of the exact same `_form` object and the exact same `UpdateCharacterSheetRequest`/PUT that "Informações Básicas" saves. Today, editing them only actually persists when the user goes back to "Informações Básicas" and clicks Salvar there. Removing that button without also wiring these 4 fields would silently break saving them — so this task wires them too, even though they're outside the tab most of this task's changes live in. (This asymmetry doesn't exist in `FichaDeNpc.razor`/`FichaDeCriatura.razor` — their equivalent fields already live inside their own "Informações Básicas" `<EditForm>`; Tasks 7 and 8 don't need this extra step.)

- [ ] **Step 1: Add `_autoSave`, `NotifySavedAsync`, `IDisposable`, subscribe/unsubscribe**

Same pattern as Task 3 Step 1 — add `@implements IDisposable` (check it's not already present), `private readonly AutoSaveCoordinator _autoSave = new();`, subscribe in `OnInitialized`/`OnInitializedAsync` (check which already exists in this file and add to it rather than duplicating), unsubscribe in `Dispose()`, and:
```csharp
    private Task NotifySavedAsync()
    {
        _autoSave.NotifyChanged(SaveIfValidAsync);
        return Task.CompletedTask;
    }
```

- [ ] **Step 2: Add the indicator and remove the button**

Replace:
```razor
        <EditForm Model="_form" OnValidSubmit="SaveAsync">
            <DataAnnotationsValidator />
```
with:
```razor
        <EditForm Model="_form">
            <DataAnnotationsValidator />
            <AutoSaveIndicator State="_autoSave.State" LastSavedAt="_autoSave.LastSavedAt" />
```
Remove the line `<MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary" Class="mt-3">Salvar</MudButton>` that currently sits right before this tab's `</EditForm>` (it is followed by the separate "Características" `<Section>`, which stays untouched — do not remove anything past the `</EditForm>`).

- [ ] **Step 3: Wire every field in the "Informações Básicas" tab**

Add `@bind-Value:after="NotifySavedAsync"` (or the equivalent `@bind-X:after` for the three custom field components) to:
- `<MudTextField T="string" @bind-Value="_form.Nome" Label="Nome" />`
- `<LinhagemVarianteFields @bind-Linhagem="_form.Linhagem" @bind-Variante="_form.Variante" />` → add `@bind-Linhagem:after="NotifySavedAsync" @bind-Variante:after="NotifySavedAsync"`
- `<VocacaoSubVocacaoFields @bind-Vocacao="_form.Vocacao" @bind-SubVocacao="_form.SubVocacao" />` → add `@bind-Vocacao:after="NotifySavedAsync" @bind-SubVocacao:after="NotifySavedAsync"`
- `<AfinidadeSelect @bind-Value="_form.Afinidade" />` → add `@bind-Value:after="NotifySavedAsync"`
- `<MudTextField T="string" @bind-Value="_form.Propriedade" Label="Propriedade" />`
- `<MudCheckBox T="bool" @bind-Value="_form.PossuiCoracaoDeMana" Label="Possui Coração de Mana?" />`
- `<MudNumericField T="int?" @bind-Value="_form.ExperienciaAtual" For="@(() => _form.ExperienciaAtual)" Label="Experiência Atual" />`
- `<MudNumericField T="int?" @bind-Value="_form.PontosDeIgnicaoAtual" For="@(() => _form.PontosDeIgnicaoAtual)" Label="Pontos de Ignição Atual" />`
- The 7 rank fields: `_form.NucleosRankF`, `NucleosRankE`, `NucleosRankD`, `NucleosRankC`, `NucleosRankB`, `NucleosRankA`, `NucleosRankS`.

Do **not** wire `_xpDelta`/`_piDelta` or their "Aplicar" buttons — those apply a one-off delta via an explicit action, not a field-edit, and are out of scope (they already have their own working "Aplicar" click handler; leave `ApplyXpDeltaAsync`/`ApplyPiDeltaAsync` untouched).

- [ ] **Step 4: Wire the avatar handlers**

The avatar dialog's `SelectExistingImage`/`RemoveImage` calls and `UploadImageAsync`'s continuation only mutate `_form.ImageId`/`_form.ImageUrl` locally today — with the button gone, they need to trigger the save too:
```csharp
    private void SelectExistingImage(string? id)
    {
        _form.ImageId = id;
        _form.ImageUrl = id is null ? null : _myImages.FirstOrDefault(i => i.Id == id)?.Url;
        _autoSave.NotifyChanged(SaveIfValidAsync);
    }

    private void RemoveImage()
    {
        _form.ImageId = null;
        _form.ImageUrl = null;
        _autoSave.NotifyChanged(SaveIfValidAsync);
    }
```
And at the end of `UploadImageAsync` (after it sets `_form.ImageId`/`_form.ImageUrl` from the upload response — read the method to find exactly where that assignment happens), add `_autoSave.NotifyChanged(SaveIfValidAsync);` right after.

- [ ] **Step 5: Wire the "Combate" tab's 4 fields (see the scope note above)**

Add `@bind-Value:after="NotifySavedAsync"` to the 4 fields at the "Combate" `<Section>` (around line 274): `_form.VitalidadeAtual`, `_form.FocoAtual`, `_form.AdrenalinaAtual`, `_form.EstresseAtual`. These are not inside any `<EditForm>` today — `@bind-Value:after` still works on a bare MudBlazor component outside an `EditForm`, since it's a Blazor compiler feature independent of `EditContext`.

- [ ] **Step 6: Reshape `SaveAsync` into `SaveIfValidAsync`**

Replace:
```csharp
    private async Task SaveAsync()
    {
        if (_form.ExperienciaAtual is null
            || _form.NucleosRankF is null || _form.NucleosRankE is null || _form.NucleosRankD is null
            || _form.NucleosRankC is null || _form.NucleosRankB is null || _form.NucleosRankA is null || _form.NucleosRankS is null
            || _form.PontosDeIgnicaoAtual is null
            || _form.VitalidadeAtual is null || _form.FocoAtual is null || _form.AdrenalinaAtual is null || _form.EstresseAtual is null)
        {
            _errorMessage = "Preencha todos os campos obrigatórios da aba Informações Básicas antes de salvar.";
            return;
        }

        var request = new UpdateCharacterSheetRequest(_form.ImageId, _form.Nome, _form.Linhagem, _form.Variante, _form.Vocacao, _form.SubVocacao,
            _form.Afinidade, _form.Propriedade, _form.PossuiCoracaoDeMana, _form.ExperienciaAtual!.Value, _form.EAPAtual,
            _form.NucleosRankF!.Value, _form.NucleosRankE!.Value, _form.NucleosRankD!.Value, _form.NucleosRankC!.Value, _form.NucleosRankB!.Value, _form.NucleosRankA!.Value, _form.NucleosRankS!.Value,
            _form.PontosDeIgnicaoAtual!.Value, _form.PontosDeIgnicaoBonusManual, _form.VitalidadeAtual!.Value, _form.FocoAtual!.Value, _form.AdrenalinaAtual!.Value, _form.EstresseAtual!.Value,
            _form.Cobertura, _form.Ciclos, _form.PontosDePericiaBonusCritico);

        var response = await Http.PutAsJsonAsync($"character-sheets/{SheetId}", request);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a ficha.";
            return;
        }

        await LoadSheetAsync();
    }
```
with:
```csharp
    private async Task<bool> SaveIfValidAsync()
    {
        if (_form.ExperienciaAtual is null
            || _form.NucleosRankF is null || _form.NucleosRankE is null || _form.NucleosRankD is null
            || _form.NucleosRankC is null || _form.NucleosRankB is null || _form.NucleosRankA is null || _form.NucleosRankS is null
            || _form.PontosDeIgnicaoAtual is null
            || _form.VitalidadeAtual is null || _form.FocoAtual is null || _form.AdrenalinaAtual is null || _form.EstresseAtual is null)
        {
            _errorMessage = "Preencha todos os campos obrigatórios da aba Informações Básicas antes de salvar.";
            return false;
        }

        _errorMessage = null;
        var request = new UpdateCharacterSheetRequest(_form.ImageId, _form.Nome, _form.Linhagem, _form.Variante, _form.Vocacao, _form.SubVocacao,
            _form.Afinidade, _form.Propriedade, _form.PossuiCoracaoDeMana, _form.ExperienciaAtual!.Value, _form.EAPAtual,
            _form.NucleosRankF!.Value, _form.NucleosRankE!.Value, _form.NucleosRankD!.Value, _form.NucleosRankC!.Value, _form.NucleosRankB!.Value, _form.NucleosRankA!.Value, _form.NucleosRankS!.Value,
            _form.PontosDeIgnicaoAtual!.Value, _form.PontosDeIgnicaoBonusManual, _form.VitalidadeAtual!.Value, _form.FocoAtual!.Value, _form.AdrenalinaAtual!.Value, _form.EstresseAtual!.Value,
            _form.Cobertura, _form.Ciclos, _form.PontosDePericiaBonusCritico);

        var response = await Http.PutAsJsonAsync($"character-sheets/{SheetId}", request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Não foi possível salvar a ficha.");

        await LoadSheetAsync();
        return true;
    }
```
(The `await LoadSheetAsync()` re-fetches and repopulates `_form` from the server after every successful auto-save — same as today's post-Salvar behavior, now just running more often. If this causes any visible flicker/cursor-jump during rapid edits, that's a pre-existing consequence of this method's existing shape, not a new defect introduced by this task; leave it as-is unless `dotnet build`/manual testing surfaces an actual regression.)

- [ ] **Step 7: Build**

Run: `dotnet build`
Expected: 0 Warning(s), 0 Error(s).

- [ ] **Step 8: Verify the endpoint contract is unaffected**

Run the existing integration tests for the character sheets controller (`grep -rl "class.*ControllerTests" tests/RuinaRPG.Tests.Integration/Controllers | grep -i "charactersheet"`).
Expected: PASS, unchanged.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "feat: auto-save the character sheet's Informações Básicas + Combate fields on blur"
```

---

## Task 7: Convert `FichaDeNpc.razor`'s "Informações Básicas" tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`

**Interfaces:**
- Consumes: `AutoSaveCoordinator`, `AutoSaveState`, `AutoSaveIndicator` (Tasks 1-2).

Same shape as Task 6, but this page's "Recursos" section (`AdrenalinaAtual`/`FocoAtual`/`EstresseAtual`/`VitalidadeAtual`) is already inside the same "Informações Básicas" `<EditForm>` — no separate-tab step is needed here.

- [ ] **Step 1: Add `_autoSave`, `NotifySavedAsync`, `IDisposable`, subscribe/unsubscribe** — identical pattern to Task 6 Step 1.

- [ ] **Step 2: Add the indicator, remove the button** — identical pattern to Task 6 Step 2, applied to this file's `<EditForm Model="_form" OnValidSubmit="SaveAsync">` (line 24) and its `Salvar` button (line 106).

- [ ] **Step 3: Wire every field in the "Informações Básicas" tab**

Add `@bind-Value:after="NotifySavedAsync"` (or `@bind-X:after` for the component fields) to every bound field listed at lines 72-103 of the file as read during planning:
- `_form.Nome` (MudTextField)
- `LinhagemVarianteFields`: `@bind-Linhagem:after="NotifySavedAsync" @bind-Variante:after="NotifySavedAsync"`
- `VocacaoSubVocacaoFields`: `@bind-Vocacao:after="NotifySavedAsync" @bind-SubVocacao:after="NotifySavedAsync"`
- `AfinidadeSelect`: `@bind-Value:after="NotifySavedAsync"`
- `_form.Propriedade` (MudTextField)
- `_form.Nivel` (MudNumericField)
- `_form.PossuiCoracaoDeMana` (MudCheckBox)
- `_form.ExperienciaAtual`, `_form.EAPAtual`, `_form.PontosDeIgnicaoAtual`, `_form.PontosDeIgnicaoTotal` (MudNumericFields)
- The 7 rank fields: `NucleosRankF` through `NucleosRankS`
- `_form.AdrenalinaAtual`, `_form.FocoAtual`, `_form.EstresseAtual`, `_form.VitalidadeAtual` (the "Recursos" section, still inside this same `<EditForm>`)

- [ ] **Step 4: Wire the avatar handlers**

Same as Task 6 Step 4 — `SelectExistingImage`, `RemoveImage`, and `UploadImageAsync`'s continuation in this file each need `_autoSave.NotifyChanged(SaveIfValidAsync);` added after they mutate `_form.ImageId`/`_form.ImageUrl`.

- [ ] **Step 5: Reshape `SaveAsync` (lines 704+) into `SaveIfValidAsync`**

Same transformation as Task 6 Step 6 — keep the existing null-check exactly as-is (setting `_errorMessage` and returning `false` instead of returning), keep the existing `UpdateNpcSheetRequest` construction exactly as-is, and change the failure branch to `throw new InvalidOperationException("Não foi possível salvar a ficha.");` instead of setting `_errorMessage` and returning. End with `return true;` after whatever the current success path does (check whether `SaveAsync` calls a reload method like `LoadSheetAsync()` at the end — if so, keep that call before `return true;`).

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: 0 Warning(s), 0 Error(s).

- [ ] **Step 7: Verify the endpoint contract is unaffected**

Run the existing integration tests for the NPC sheets controller (`grep -rl "class.*ControllerTests" tests/RuinaRPG.Tests.Integration/Controllers | grep -i npc`).
Expected: PASS, unchanged.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "feat: auto-save the NPC sheet's Informações Básicas fields on blur"
```

---

## Task 8: Convert `FichaDeCriatura.razor`'s "Informações Básicas" tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor`

**Interfaces:**
- Consumes: `AutoSaveCoordinator`, `AutoSaveState`, `AutoSaveIndicator` (Tasks 1-2).

Same shape as Task 7 — this page's "Recursos" section is also already inside the "Informações Básicas" `<EditForm>`.

- [ ] **Step 1: Add `_autoSave`, `NotifySavedAsync`, `IDisposable`, subscribe/unsubscribe** — identical pattern to Task 6 Step 1.

- [ ] **Step 2: Add the indicator, remove the button** — identical pattern to Task 6 Step 2, applied to this file's `<EditForm Model="_form" OnValidSubmit="SaveAsync">` (line 24) and its `Salvar` button (line 107).

- [ ] **Step 3: Wire every field in the "Informações Básicas" tab**

Add `@bind-Value:after="NotifySavedAsync"` to every bound field read during planning (lines 72-104 of the file):
- `_form.Nome`, `_form.Raca` (MudTextFields)
- `_form.Arquetipo` (MudSelect, including its two `MudSelectItem`s — the `:after` goes on the `MudSelect`'s own `@bind-Value`, not the items)
- `_form.SubArquetipo` (MudTextField)
- `AfinidadeSelect`: `@bind-Value:after="NotifySavedAsync"`
- `_form.Rank` (MudSelect)
- `_form.Nivel`, `_form.ExperienciaAtual`, `_form.PontosDeIgnicao` (MudNumericFields)
- `_form.AdrenalinaAtual`, `_form.FocoAtual`, `_form.VitalidadeAtual` (the "Recursos" section, still inside this same `<EditForm>`)

Note: `_form.Cobertura`, edited in this file's separate "Combate" tab, is bound via `Value`/`ValueChanged` to `UpdateCoberturaAsync` — that's the already-existing instant-PUT sub-resource mechanism (out of scope, per Global Constraints and the spec's Scope section), not a plain `@bind-Value` — leave it untouched.

- [ ] **Step 4: Wire the avatar handlers**

Same as Task 6 Step 4 — `SelectExistingImage`, `RemoveImage`, and `UploadImageAsync`'s continuation in this file each need `_autoSave.NotifyChanged(SaveIfValidAsync);` added.

- [ ] **Step 5: Reshape `SaveAsync` (lines 642+) into `SaveIfValidAsync`**

Same transformation as Task 6 Step 6, applied to this file's actual null-check and `UpdateCreatureSheetRequest` construction (read the current method body first — it was captured during planning as checking `_form.Nivel`, `_form.ExperienciaAtual`, `_form.PontosDeIgnicao`, `_form.VitalidadeAtual`, `_form.FocoAtual`, `_form.AdrenalinaAtual` for null, and building `new UpdateCreatureSheetRequest(_form.ImageId, _form.Nome, _form.Raca, _form.Arquetipo, _form.SubArquetipo, _form.Afinidade, _form.Rank, _form.Nivel!.Value, _form.ExperienciaAtual!.Value, _form.PontosDeIgnicao!.Value, _form.VitalidadeAtual!.Value, _form.FocoAtual!.Value, _form.AdrenalinaAtual!.Value, _form.Cobertura)` then `PUT creature-sheets/{SheetId}`).

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: 0 Warning(s), 0 Error(s).

- [ ] **Step 7: Verify the endpoint contract is unaffected**

Run the existing integration tests for the creature sheets controller (`grep -rl "class.*ControllerTests" tests/RuinaRPG.Tests.Integration/Controllers | grep -i creature`).
Expected: PASS, unchanged.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeCriatura.razor
git commit -m "feat: auto-save the creature sheet's Informações Básicas fields on blur"
```

---

## Final check (after all 8 tasks)

Run the **full** suite before finishing the branch: `dotnet build` (0 Warning(s), 0 Error(s)) and `dotnet test` (all three test projects — Unit, Client, Integration; Integration requires Docker). Then follow `superpowers:finishing-a-development-branch` on the `feat/autosave-on-blur` branch.
