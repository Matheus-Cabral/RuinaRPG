# Cadastro inline de itens do Catálogo nas Fichas de NPC e Criatura — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a GM cadastrar um item novo do Catálogo sem sair da Ficha de NPC/Criatura, através
de um botão "+" ao lado de cada buscador de item que abre um diálogo com o formulário de
cadastro já existente; ao salvar, o item novo já fica selecionado no picker.

**Architecture:** Um novo componente compartilhado `CatalogoItemPicker` envolve o `EntityPicker`
já existente (que ganha um único método público novo pra aplicar uma seleção vinda de fora) e um
`<MudDialog>` inline hospedando o `CatalogoItemForm` já existente na página `/catalogo/novo`
(que ganha 2 parâmetros opcionais pra funcionar embutido: `FixedTipo` e `OnCreated`). As duas
Fichas trocam `EntityPicker` por `CatalogoItemPicker` nos 5 pontos de item cada (Arma, Armadura,
Escudo, Inventário/Espólios, Artefato) — os pickers de Traço não mudam.

**Tech Stack:** Blazor WebAssembly, MudBlazor, bUnit. Nenhuma mudança de backend/API/banco.

**Spec:** `docs/superpowers/specs/2026-09-15-cadastro-inline-de-itens-design.md`

## Global Constraints

- Ficha de Personagem não é tocada — só Ficha de NPC e Ficha de Criatura.
- Nenhuma mudança de backend: `ItemsController.Create` já existe e já é `[Authorize(Roles = "GM")]`.
- O botão "+" só aparece quando `PodeCriar` é `true` — as duas Fichas passam
  `PodeCriar="@_isGmCaller"`, um campo que ambas já calculam
  (`authState.User.IsInRole("GM")`) — nenhuma lógica de permissão nova.
- `dotnet build` deve ficar em 0 Warning(s), 0 Error(s) após cada tarefa.
- Os pickers de Traço (Características) nas duas Fichas não mudam — continuam `EntityPicker` puro.

---

### Task 1: `EntityPicker` — método público `SelectExternallyAsync`

**Files:**
- Modify: `src/RuinaRPG.Client/Shared/EntityPicker.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/EntityPickerTests.cs`

**Interfaces:**
- Produces: `EntityPicker.SelectExternallyAsync(PickerOption option) : Task` — aplica uma seleção
  vinda de fora do próprio fluxo de busca (mesmo efeito de o usuário escolher no dropdown: define
  `Value`, guarda o rótulo pra exibição, dispara `ValueChanged`). Usado pela Task 3.

Nenhum parâmetro existente do `EntityPicker` muda — este é o único ponto de extensão.

- [ ] **Step 1: Write the failing test**

Em `tests/RuinaRPG.Tests.Client/Shared/EntityPickerTests.cs`, adicione (o arquivo já tem `using
Bunit; using FluentAssertions; using MudBlazor; using RuinaRPG.Client.Shared; using Xunit;` no
topo — não repita):

```csharp
    [Fact]
    public async Task SelectExternallyAsync_applies_a_selection_from_outside_the_search_flow()
    {
        string? boundValue = null;
        var cut = Render<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.ValueChanged, v => boundValue = v)
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>())));

        await cut.InvokeAsync(() => cut.Instance.SelectExternallyAsync(new PickerOption("id-9", "Item Novo")));

        boundValue.Should().Be("id-9");
        cut.Markup.Should().Contain("Item Novo");
        cut.Markup.Should().NotContain("<input");
    }
```

Cole esse método dentro da classe `EntityPickerTests`, junto dos outros `[Fact]`.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~SelectExternallyAsync_applies"`
Expected: FAIL to compile — `SelectExternallyAsync` não existe ainda em `EntityPicker`.

- [ ] **Step 3: Add the method**

Em `src/RuinaRPG.Client/Shared/EntityPicker.razor`, dentro do bloco `@code { ... }`, logo depois do
método `SelectAsync` existente (que fica assim hoje):

```csharp
    private async Task SelectAsync(PickerOption? option)
    {
        if (option is null)
            return;
        _selectedLabel = option.Label;
        Value = option.Id;
        StateHasChanged();
        await ValueChanged.InvokeAsync(Value);
    }
```

Adicione logo abaixo:

```csharp
    /// <summary>
    /// Aplica uma seleção vinda de fora do próprio fluxo de busca — por exemplo, um item que
    /// acabou de ser criado num diálogo. Mesmo efeito de o usuário escolher no dropdown.
    /// </summary>
    public Task SelectExternallyAsync(PickerOption option) => SelectAsync(option);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~SelectExternallyAsync_applies"`
Expected: PASS

- [ ] **Step 5: Run the full Client suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Client && dotnet build RuinaRPG.sln`
Expected: all pass, `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Shared/EntityPicker.razor tests/RuinaRPG.Tests.Client/Shared/EntityPickerTests.cs
git commit -m "feat: EntityPicker.SelectExternallyAsync — apply a selection from outside the search flow"
```

---

### Task 2: `CatalogoItemForm` — `FixedTipo` e `OnCreated`

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`
- Test: `tests/RuinaRPG.Tests.Client/Pages/CatalogoItemFormTests.cs`

**Interfaces:**
- Consumes: nada de tarefas anteriores.
- Produces: `CatalogoItemForm.FixedTipo : string?` (parâmetro), `CatalogoItemForm.OnCreated :
  EventCallback<RuinaRPG.Contracts.Items.ItemResponse>` (parâmetro), `CatalogoItemForm.CreateForTestsAsync()
  : Task` (seam de teste). Usados pela Task 3.

O comportamento atual da página standalone `/catalogo/novo` (sem `FixedTipo`/`OnCreated`) não
muda em nada — os dois parâmetros são opcionais e, sem eles, tudo funciona exatamente como hoje.

- [ ] **Step 1: Write the failing tests**

Em `tests/RuinaRPG.Tests.Client/Pages/CatalogoItemFormTests.cs`, adicione estes dois métodos
dentro da classe `CatalogoItemFormTests` (o arquivo já importa `RuinaRPG.Client.Pages`,
`System.Net`, `System.Net.Http.Json` — não repita os `using`; `ItemResponse` mora em
`RuinaRPG.Contracts.Items`, adicione esse `using` se ainda não estiver lá):

```csharp
    [Fact]
    public void FixedTipo_hides_the_Tipo_selector_and_preselects_it()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, "Arma"));

        cut.FindComponents<MudBlazor.MudSelect<string>>().Should().NotContain(c => c.Instance.Label == "Tipo");
        cut.Markup.Should().Contain("Empunhadura"); // só a seção de campos de Arma renderiza isso — prova que _form.Tipo já veio "Arma"
    }

    [Fact]
    public async Task OnCreated_callback_fires_with_the_created_item_instead_of_navigating()
    {
        RuinaRPG.Contracts.Items.ItemResponse? created = null;
        var getCount = 0;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                getCount++;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            }
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new
            {
                Id = "item-new", Tipo = "ItemGeral", Nome = "Poção Nova", Peso = 1m, Preco = 5,
                ImageUrl = (string?)null, Subcategoria = (string?)null, Descricao = (string?)null, Tier = (string?)null,
                Empunhadura = (string?)null, Dados = (string?)null, Dano = (int?)null, Critico = (string?)null,
                Alcance = (int?)null, TipoDeDano = (string?)null, RequisitoAtributo = (string?)null,
                DurabilidadeMaxima = (int?)null, Categoria = (string?)null, Defesa = (int?)null, RF = (int?)null,
                RM = (int?)null, Penalidade = (string?)null, RequisitoVigor = (int?)null, BonusDefesa = (int?)null,
                TipoDeAlvo = (string?)null, Alvo = (string?)null, Valor = (int?)null, CapacidadeExtra = (decimal?)null,
            }) };
        });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>(p => p
            .Add(x => x.FixedTipo, "ItemGeral")
            .Add(x => x.OnCreated, EventCallback.Factory.Create<RuinaRPG.Contracts.Items.ItemResponse>(this, r => created = r)));
        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Poção Nova"));

        await cut.InvokeAsync(() => cut.Instance.CreateForTestsAsync());

        created.Should().NotBeNull();
        created!.Nome.Should().Be("Poção Nova");
        getCount.Should().Be(1, "OnCreated deve substituir a navegação, não disparar uma nova busca");
    }
```

`EventCallback` mora em `Microsoft.AspNetCore.Components` — se o arquivo ainda não importa esse
namespace, adicione `using Microsoft.AspNetCore.Components;` no topo.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~FixedTipo_hides|FullyQualifiedName~OnCreated_callback_fires"`
Expected: FAIL to compile — `FixedTipo`/`OnCreated`/`CreateForTestsAsync` não existem ainda.

- [ ] **Step 3: Add the `FixedTipo` and `OnCreated` parameters**

Em `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`, no bloco `@code`, logo depois de:

```csharp
    [Parameter] public string? ItemId { get; set; }
```

Adicione:

```csharp
    [Parameter] public string? FixedTipo { get; set; }
    [Parameter] public EventCallback<ItemResponse> OnCreated { get; set; }
```

- [ ] **Step 4: Pre-select `_form.Tipo` when `FixedTipo` is set**

Substitua:

```csharp
    protected override void OnInitialized()
    {
        _autoSave.StateChanged += StateHasChangedFromAutoSave;
    }
```

Por:

```csharp
    protected override void OnInitialized()
    {
        _autoSave.StateChanged += StateHasChangedFromAutoSave;
        if (FixedTipo is not null)
            _form.Tipo = FixedTipo;
    }
```

- [ ] **Step 5: Hide the Tipo selector when `FixedTipo` is set**

Substitua:

```razor
        @if (ItemId is null)
        {
            <MudSelect T="string" @bind-Value="_form.Tipo" Label="Tipo">
```

Por:

```razor
        @if (ItemId is null && FixedTipo is null)
        {
            <MudSelect T="string" @bind-Value="_form.Tipo" Label="Tipo">
```

- [ ] **Step 6: Hide the Breadcrumbs when `FixedTipo` is set**

Substitua:

```razor
<Breadcrumbs Items="@Crumbs" />
```

Por:

```razor
@if (FixedTipo is null)
{
    <Breadcrumbs Items="@Crumbs" />
}
```

- [ ] **Step 7: `CreateAsync` calls `OnCreated` instead of navigating when it has a delegate**

Substitua:

```csharp
    private async Task CreateAsync()
    {
        if (!_editContext.Validate())
            return;

        var request = new CreateItemRequest(_form.Tipo, _form.Nome, _form.Peso, _form.Preco, _form.ImageId,
            _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
            _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
            _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
            _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor, _form.CapacidadeExtra);
        var response = await Http.PostAsJsonAsync("items", request);

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar o item.";
            return;
        }

        Navigation.NavigateTo("/catalogo");
    }
```

Por:

```csharp
    private async Task CreateAsync()
    {
        if (!_editContext.Validate())
            return;

        var request = new CreateItemRequest(_form.Tipo, _form.Nome, _form.Peso, _form.Preco, _form.ImageId,
            _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
            _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
            _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
            _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor, _form.CapacidadeExtra);
        var response = await Http.PostAsJsonAsync("items", request);

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar o item.";
            return;
        }

        if (OnCreated.HasDelegate)
        {
            var created = await response.Content.ReadFromJsonAsync<ItemResponse>();
            if (created is null)
            {
                _errorMessage = "Não foi possível salvar o item.";
                return;
            }
            await OnCreated.InvokeAsync(created);
            return;
        }

        Navigation.NavigateTo("/catalogo");
    }
```

- [ ] **Step 8: Add the test-only seam for `CreateAsync`**

No fim do `@code`, junto dos outros seams de teste (`OnTipoDeAlvoChangedForTestsAsync`,
`AlvoForTests`), adicione:

```csharp
    internal Task CreateForTestsAsync() => CreateAsync();
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~FixedTipo_hides|FullyQualifiedName~OnCreated_callback_fires"`
Expected: PASS

- [ ] **Step 10: Run the full Client suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Client && dotnet build RuinaRPG.sln`
Expected: all pass (incluindo os testes já existentes de `CatalogoItemFormTests` — nenhum deve
quebrar), `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 11: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CatalogoItemForm.razor tests/RuinaRPG.Tests.Client/Pages/CatalogoItemFormTests.cs
git commit -m "feat: CatalogoItemForm gains FixedTipo/OnCreated for embedded (dialog) use"
```

---

### Task 3: Novo componente `CatalogoItemPicker`

**Files:**
- Create: `src/RuinaRPG.Client/Shared/CatalogoItemPicker.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/CatalogoItemPickerTests.cs`

**Interfaces:**
- Consumes: `EntityPicker.SelectExternallyAsync` (Task 1), `CatalogoItemForm.FixedTipo`/`OnCreated`
  (Task 2), `RuinaRPG.Client.Shared.PickerOption`, `RuinaRPG.Contracts.Items.ItemResponse`.
- Produces: `CatalogoItemPicker` — mesmo contrato de parâmetros do `EntityPicker`
  (`Value`/`ValueChanged`/`SearchItems`/`Placeholder`) mais `Tipo : string?` e `PodeCriar : bool`.
  Usado pela Task 4.

- [ ] **Step 1: Write the failing tests**

Create `tests/RuinaRPG.Tests.Client/Shared/CatalogoItemPickerTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Items;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CatalogoItemPickerTests : MudBunitContext
{
    private static HttpClient EmptyListClient() => FakeHttpMessageHandler.CreateClient(request =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });

    [Fact]
    public void PodeCriar_false_hides_the_create_button()
    {
        var cut = Render<CatalogoItemPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>()))
            .Add(x => x.PodeCriar, false));

        cut.FindComponents<MudIconButton>().Should().BeEmpty();
    }

    [Fact]
    public void PodeCriar_true_shows_the_create_button_and_opening_the_dialog_passes_the_Tipo_through()
    {
        Services.AddScoped(_ => EmptyListClient());

        var cut = Render<CatalogoItemPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>()))
            .Add(x => x.PodeCriar, true)
            .Add(x => x.Tipo, "Arma"));

        cut.FindComponents<MudIconButton>().Should().ContainSingle();

        cut.Instance.OpenDialogForTests();
        cut.Render();

        cut.FindComponent<CatalogoItemForm>().Instance.FixedTipo.Should().Be("Arma");
    }

    [Fact]
    public async Task Creating_an_item_closes_the_dialog_and_selects_it_in_the_picker()
    {
        Services.AddScoped(_ => EmptyListClient());
        string? boundValue = null;

        var cut = Render<CatalogoItemPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.ValueChanged, v => boundValue = v)
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>()))
            .Add(x => x.PodeCriar, true)
            .Add(x => x.Tipo, "ItemGeral"));

        cut.Instance.OpenDialogForTests();
        cut.Render();

        var created = new ItemResponse("item-new", "ItemGeral", "Poção Nova", 1m, 5, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        await cut.InvokeAsync(() => cut.Instance.HandleCreatedForTestsAsync(created));

        cut.Instance.DialogOpenForTests.Should().BeFalse();
        boundValue.Should().Be("item-new");
        cut.Markup.Should().Contain("Poção Nova");
    }

    [Fact]
    public void Cancelar_closes_the_dialog_without_creating_anything()
    {
        Services.AddScoped(_ => EmptyListClient());

        var cut = Render<CatalogoItemPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>()))
            .Add(x => x.PodeCriar, true)
            .Add(x => x.Tipo, "Arma"));

        cut.Instance.OpenDialogForTests();
        cut.Instance.CancelForTests();

        cut.Instance.DialogOpenForTests.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~CatalogoItemPickerTests"`
Expected: FAIL to compile — `CatalogoItemPicker` não existe ainda.

- [ ] **Step 3: Create `CatalogoItemPicker.razor`**

Create `src/RuinaRPG.Client/Shared/CatalogoItemPicker.razor`:

```razor
@using MudBlazor
@using RuinaRPG.Client.Pages
@using RuinaRPG.Contracts.Items
@*
    Envolve o EntityPicker com um botão opcional "+" que abre um diálogo pra cadastrar um item
    novo no Catálogo sem sair da tela atual — ver docs/superpowers/specs/2026-09-15-cadastro-inline-de-itens-design.md.
    PodeCriar controla se o botão/diálogo aparecem (gate de GM, decidido pelo chamador); Tipo trava
    o Tipo do item criado (null pra Espólios, que aceita qualquer Tipo — o CatalogoItemForm então
    mostra o seletor de Tipo normalmente dentro do diálogo).
*@
<div style="display:flex; align-items:center; gap:0.5em;">
    <div style="flex:1;">
        <EntityPicker @ref="_picker" Value="Value" ValueChanged="ValueChanged" SearchItems="SearchItems" Placeholder="Placeholder" />
    </div>
    @if (PodeCriar)
    {
        <MudIconButton Icon="@Icons.Material.Filled.Add" OnClick="@(() => _dialogOpen = true)" title="Cadastrar novo item" />
    }
</div>

@if (PodeCriar)
{
    <MudDialog @bind-Visible="_dialogOpen">
        <DialogContent>
            <CatalogoItemForm FixedTipo="@Tipo" OnCreated="HandleCreatedAsync" />
        </DialogContent>
        <DialogActions>
            <MudButton OnClick="Cancel">Cancelar</MudButton>
        </DialogActions>
    </MudDialog>
}

@code {
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter, EditorRequired] public Func<string, Task<List<PickerOption>>> SearchItems { get; set; } = null!;
    [Parameter] public string Placeholder { get; set; } = "Buscar...";
    [Parameter] public string? Tipo { get; set; }
    [Parameter] public bool PodeCriar { get; set; }

    private EntityPicker _picker = null!;
    private bool _dialogOpen;

    private void Cancel() => _dialogOpen = false;

    private async Task HandleCreatedAsync(ItemResponse created)
    {
        _dialogOpen = false;
        await _picker.SelectExternallyAsync(new PickerOption(created.Id, created.Nome));
    }

    // Test-only seams: bUnit não consegue clicar de forma confiável no botão MudIconButton nem no
    // diálogo do MudDialog — mesma justificativa dos seams já existentes em EntityPicker/CatalogoItemForm.
    internal void OpenDialogForTests() => _dialogOpen = true;
    internal bool DialogOpenForTests => _dialogOpen;
    internal void CancelForTests() => Cancel();
    internal Task HandleCreatedForTestsAsync(ItemResponse created) => HandleCreatedAsync(created);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~CatalogoItemPickerTests"`
Expected: PASS (4 tests)

- [ ] **Step 5: Run the full Client suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Client && dotnet build RuinaRPG.sln`
Expected: all pass, `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Shared/CatalogoItemPicker.razor tests/RuinaRPG.Tests.Client/Shared/CatalogoItemPickerTests.cs
git commit -m "feat: CatalogoItemPicker — EntityPicker + inline create-item dialog"
```

---

### Task 4: Trocar `EntityPicker` por `CatalogoItemPicker` nos 5 pontos de item de cada Ficha

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`

**Interfaces:**
- Consumes: `CatalogoItemPicker` (Task 3).
- Produces: nada — esta é a última tarefa do plano.

Cada uma das 10 trocas abaixo é a mesma mudança mecânica: troca de tag `EntityPicker` →
`CatalogoItemPicker`, mais `Tipo="..."` (omitido = `null`, só para o Espólios da Criatura) e
`PodeCriar="@_isGmCaller"`. Nenhuma outra linha muda. Os pickers de Traço (`SearchTraitsAsync`),
em ambos os arquivos, **não** mudam.

- [ ] **Step 1: `FichaDeCriatura.razor` — Arma**

Find (linha 164):

```razor
                    <EntityPicker @bind-Value="_weaponForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Arma"))" Placeholder="Buscar arma..." />
```

Replace with:

```razor
                    <CatalogoItemPicker @bind-Value="_weaponForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Arma"))" Placeholder="Buscar arma..." Tipo="Arma" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 2: `FichaDeCriatura.razor` — Armadura (por slot)**

Find (linha 283):

```razor
                                    <EntityPicker Value="@_armorSlotForm[slot.Slot]" ValueChanged="@(v => _armorSlotForm[slot.Slot] = v ?? "")" SearchItems="@(q => SearchItemsByTipoAsync(q, "Armadura"))" Placeholder="Buscar armadura..." />
```

Replace with:

```razor
                                    <CatalogoItemPicker Value="@_armorSlotForm[slot.Slot]" ValueChanged="@(v => _armorSlotForm[slot.Slot] = v ?? "")" SearchItems="@(q => SearchItemsByTipoAsync(q, "Armadura"))" Placeholder="Buscar armadura..." Tipo="Armadura" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 3: `FichaDeCriatura.razor` — Escudo**

Find (linha 296):

```razor
                <EntityPicker @bind-Value="_shieldForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Escudo"))" Placeholder="Buscar escudo..." />
```

Replace with:

```razor
                <CatalogoItemPicker @bind-Value="_shieldForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Escudo"))" Placeholder="Buscar escudo..." Tipo="Escudo" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 4: `FichaDeCriatura.razor` — Espólios (aceita qualquer Tipo, sem `Tipo=`)**

Find (linha 466):

```razor
                <EntityPicker @bind-Value="_spoilForm.ItemId" SearchItems="SearchAllItemsAsync" Placeholder="Buscar item..." />
```

Replace with:

```razor
                <CatalogoItemPicker @bind-Value="_spoilForm.ItemId" SearchItems="SearchAllItemsAsync" Placeholder="Buscar item..." PodeCriar="@_isGmCaller" />
```

- [ ] **Step 5: `FichaDeCriatura.razor` — Artefato**

Find (linha 500):

```razor
                <EntityPicker @bind-Value="_artifactForm.ArtifactItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Artefato"))" Placeholder="Buscar artefato..." />
```

Replace with:

```razor
                <CatalogoItemPicker @bind-Value="_artifactForm.ArtifactItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Artefato"))" Placeholder="Buscar artefato..." Tipo="Artefato" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 6: `FichaDeNpc.razor` — Arma**

Find (linha 246):

```razor
                <EntityPicker @bind-Value="_weaponForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Arma"))" Placeholder="Buscar arma..." />
```

Replace with:

```razor
                <CatalogoItemPicker @bind-Value="_weaponForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Arma"))" Placeholder="Buscar arma..." Tipo="Arma" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 7: `FichaDeNpc.razor` — Armadura (por slot)**

Find (linha 346):

```razor
                                    <EntityPicker Value="@_armorSlotForm[slot.Slot]" ValueChanged="@(v => _armorSlotForm[slot.Slot] = v ?? "")" SearchItems="@(q => SearchItemsByTipoAsync(q, "Armadura"))" Placeholder="Buscar armadura..." />
```

Replace with:

```razor
                                    <CatalogoItemPicker Value="@_armorSlotForm[slot.Slot]" ValueChanged="@(v => _armorSlotForm[slot.Slot] = v ?? "")" SearchItems="@(q => SearchItemsByTipoAsync(q, "Armadura"))" Placeholder="Buscar armadura..." Tipo="Armadura" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 8: `FichaDeNpc.razor` — Escudo**

Find (linha 359):

```razor
                <EntityPicker @bind-Value="_shieldForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Escudo"))" Placeholder="Buscar escudo..." />
```

Replace with:

```razor
                <CatalogoItemPicker @bind-Value="_shieldForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Escudo"))" Placeholder="Buscar escudo..." Tipo="Escudo" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 9: `FichaDeNpc.razor` — Inventário**

Find (linha 575):

```razor
                <EntityPicker @bind-Value="_inventoryForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "ItemGeral"))" Placeholder="Buscar item..." />
```

Replace with:

```razor
                <CatalogoItemPicker @bind-Value="_inventoryForm.ItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "ItemGeral"))" Placeholder="Buscar item..." Tipo="ItemGeral" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 10: `FichaDeNpc.razor` — Artefato**

Find (linha 606):

```razor
                <EntityPicker @bind-Value="_artifactForm.ArtifactItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Artefato"))" Placeholder="Buscar artefato..." />
```

Replace with:

```razor
                <CatalogoItemPicker @bind-Value="_artifactForm.ArtifactItemId" SearchItems="@(q => SearchItemsByTipoAsync(q, "Artefato"))" Placeholder="Buscar artefato..." Tipo="Artefato" PodeCriar="@_isGmCaller" />
```

- [ ] **Step 11: Clean-rebuild the whole client**

Run: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 12: Run the full Client suite**

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: all pass (nenhum teste dedicado existe pra `FichaDeCriatura.razor`/`FichaDeNpc.razor`
hoje, então nenhuma regressão de teste é esperada nessas duas páginas — só confirma que nada mais
quebrou).

- [ ] **Step 13: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeCriatura.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "feat: wire CatalogoItemPicker into all 5 item sections of Ficha de NPC/Criatura"
```

---

## Final verification (after all 4 tasks)

- [ ] Clean-rebuild: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln` → 0 Warning(s), 0 Error(s).
- [ ] `dotnet test tests/RuinaRPG.Tests.Unit` → all pass (nenhuma mudança de Domain/Infrastructure/Api neste plano — regressão pura).
- [ ] `dotnet test tests/RuinaRPG.Tests.Client` → all pass.
- [ ] Nenhum teste de Integration precisa rodar — `ItemsController`/backend não mudam.
- [ ] Confirmar manualmente (sem navegador headless disponível neste ambiente, mesma limitação já
      documentada em rounds anteriores): numa Ficha de Criatura como GM, o botão "+" aparece nas 5
      seções de item e abre um diálogo com o Tipo certo já travado (exceto Espólios, que mostra o
      seletor de Tipo); criar um item fecha o diálogo e o item já aparece selecionado no picker;
      numa ficha concedida a um jogador (não-GM), o botão "+" não aparece em nenhuma seção.
