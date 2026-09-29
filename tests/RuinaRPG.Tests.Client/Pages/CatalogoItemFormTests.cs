using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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
                          Subcategoria = (string?)null, Descricao = (string?)null, Rank = (string?)null, Empunhadura = (string?)null,
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

    [Fact]
    public async Task A_valid_Preco_change_in_edit_mode_fires_the_PUT()
    {
        // Regression test for Finding 4 of the final review: the existing tests only assert PUT
        // does NOT fire (validation-blocked cases). Nothing proved the @bind-Value:after wiring
        // itself is actually present on the fields — this locks that down with a valid edit.
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("items"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "item-1", Tipo = "ItemGeral", Nome = "Poção", ImageUrl = (string?)null, Peso = 1m, Preco = 10,
                          Subcategoria = (string?)null, Descricao = (string?)null, Rank = (string?)null, Empunhadura = (string?)null,
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
        await cut.InvokeAsync(() => preco.Instance.ValueChanged.InvokeAsync(25));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeTrue("a valid Preço change must trigger the auto-save PUT via the @bind-Value:after wiring");
    }

    [Fact]
    public async Task Blurring_a_field_in_create_mode_never_calls_PUT()
    {
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Put)
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>();
        await Task.Delay(50); // let OnInitializedAsync finish (create mode has nothing to load)

        var preco = cut.FindComponents<MudBlazor.MudNumericField<int>>().Single(c => c.Instance.Label == "Preço (Ciclos)");
        await cut.InvokeAsync(() => preco.Instance.ValueChanged.InvokeAsync(5));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeFalse("create mode has no ItemId to PUT against — field blur must not trigger auto-save there");
    }

    [Fact]
    public async Task Editing_a_missing_item_shows_an_error_instead_of_crashing()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("items"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) }; // no items at all
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        // Regression test: with _editContext assigned late inside OnInitializedAsync (only after
        // an existing item was found and its fields copied), the "item not found" early-return
        // left _editContext null, and <EditForm EditContext="_editContext"> threw
        // InvalidOperationException on render. _editContext must now be built synchronously
        // (constructor / field initializer) so this path renders safely.
        var act = () => Render<CatalogoItemForm>(p => p.Add(x => x.ItemId, "does-not-exist"));
        act.Should().NotThrow();

        var cut = act();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Item não encontrado.");
    }

    [Fact]
    public async Task A_real_non_synchronous_HTTP_round_trip_does_not_crash_first_render()
    {
        // Regression test: FakeHttpMessageHandler completes its Task synchronously, so the whole
        // OnInitializedAsync await-chain used to run to completion before Blazor's first render —
        // masking the fact that _editContext was assigned only after the last await. A handler
        // that genuinely yields forces Blazor to render once while OnInitializedAsync's task is
        // still pending, which is what a real HTTP call over the network would also do.
        var http = AsyncFakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("items"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "item-1", Tipo = "ItemGeral", Nome = "Poção", ImageUrl = (string?)null, Peso = 1m, Preco = 10,
                          Subcategoria = (string?)null, Descricao = (string?)null, Rank = (string?)null, Empunhadura = (string?)null,
                          Dados = (string?)null, Dano = (int?)null, Critico = (string?)null, Alcance = (int?)null, TipoDeDano = (string?)null,
                          RequisitoAtributo = (string?)null, DurabilidadeMaxima = (int?)null, Categoria = (string?)null, Defesa = (int?)null,
                          RF = (int?)null, RM = (int?)null, Penalidade = (string?)null, RequisitoVigor = (int?)null, BonusDefesa = (int?)null,
                          TipoDeAlvo = (string?)null, Alvo = (string?)null, Valor = (int?)null }
                }) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var act = () => Render<CatalogoItemForm>(p => p.Add(x => x.ItemId, "item-1"));
        act.Should().NotThrow();

        var cut = act();
        await Task.Delay(200); // let the async round trips finish populating _form

        cut.Markup.Should().Contain("Poção");
    }

    private CatalogoItemForm RenderNewItemForm()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });
        Services.AddScoped(_ => http);

        return Render<CatalogoItemForm>().Instance;
    }

    // Task 5 ("Client — Rank in the Catálogo and Inquebrável on the sheets"): Arma/Armadura/Escudo
    // no longer take a free-typed Durabilidade — the max comes from the item's Rank via the global
    // Tabela de Durabilidade por Rank (GET durabilidades-por-rank), resolved client-side with
    // RuinaRPG.Domain.Items.DurabilidadeDeItem.Resolver.
    private static readonly object DurabilidadesPorRankTabela = new[]
    {
        new { Rank = "F", Durabilidade = (int?)20, Inquebravel = false },
        new { Rank = "E", Durabilidade = (int?)45, Inquebravel = false },
        new { Rank = "D", Durabilidade = (int?)80, Inquebravel = false },
        new { Rank = "C", Durabilidade = (int?)125, Inquebravel = false },
        new { Rank = "B", Durabilidade = (int?)180, Inquebravel = false },
        new { Rank = "A", Durabilidade = (int?)245, Inquebravel = false },
        new { Rank = "S", Durabilidade = (int?)null, Inquebravel = true },
        new { Rank = "SS", Durabilidade = (int?)null, Inquebravel = true },
    };

    private HttpClient CreateClientWithDurabilidadesPorRank()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("durabilidades-por-rank"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(DurabilidadesPorRankTabela) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
        });
        Services.AddScoped(_ => http);
        return http;
    }

    [Fact]
    public async Task Arma_offers_a_Rank_select_with_no_free_typed_Durabilidade_field()
    {
        CreateClientWithDurabilidadesPorRank();

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, "Arma"));
        await Task.Delay(50);

        cut.FindComponents<MudBlazor.MudSelect<string>>().Should().Contain(c => c.Instance.Label == "Rank");
        cut.FindComponents<MudBlazor.MudNumericField<int?>>().Should().NotContain(c => c.Instance.Label == "Durabilidade");
    }

    [Fact]
    public void RankOptions_lists_F_through_SS_in_order()
    {
        CatalogoItemForm.RankOptions.Should().BeEquivalentTo(
            new[] { "F", "E", "D", "C", "B", "A", "S", "SS" }, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData("Armadura")]
    [InlineData("Escudo")]
    public void Armadura_and_Escudo_also_offer_the_Rank_select_and_no_Durabilidade_field(string tipo)
    {
        CreateClientWithDurabilidadesPorRank();

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, tipo));

        cut.FindComponents<MudBlazor.MudSelect<string>>().Should().Contain(c => c.Instance.Label == "Rank");
        cut.FindComponents<MudBlazor.MudNumericField<int?>>().Should().NotContain(c => c.Instance.Label == "Durabilidade");
    }

    [Theory]
    [InlineData("ItemGeral")]
    [InlineData("Artefato")]
    public void ItemGeral_and_Artefato_do_not_offer_a_Rank_select(string tipo)
    {
        CreateClientWithDurabilidadesPorRank();

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, tipo));

        cut.FindComponents<MudBlazor.MudSelect<string>>().Should().NotContain(c => c.Instance.Label == "Rank");
    }

    [Fact]
    public async Task Choosing_Rank_D_shows_the_resolved_Durabilidade_from_the_table()
    {
        CreateClientWithDurabilidadesPorRank();

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, "Arma"));
        await Task.Delay(50); // let the durabilidades-por-rank GET land before picking a Rank

        var rankSelect = cut.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Rank");
        await cut.InvokeAsync(() => rankSelect.Instance.ValueChanged.InvokeAsync("D"));

        cut.Markup.Should().Contain("Durabilidade: 80");
    }

    [Fact]
    public async Task Choosing_Rank_S_shows_Inquebravel_instead_of_a_number()
    {
        CreateClientWithDurabilidadesPorRank();

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, "Arma"));
        await Task.Delay(50);

        var rankSelect = cut.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Rank");
        await cut.InvokeAsync(() => rankSelect.Instance.ValueChanged.InvokeAsync("S"));

        cut.Markup.Should().Contain("Inquebrável");
    }

    [Fact]
    public async Task Clearing_the_Rank_shows_Sem_durabilidade()
    {
        CreateClientWithDurabilidadesPorRank();

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, "Arma"));
        await Task.Delay(50);

        var rankSelect = cut.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Rank");
        await cut.InvokeAsync(() => rankSelect.Instance.ValueChanged.InvokeAsync("D"));
        cut.Markup.Should().Contain("Durabilidade: 80");

        await cut.InvokeAsync(() => rankSelect.Instance.ValueChanged.InvokeAsync(null));

        cut.Markup.Should().Contain("Sem durabilidade");
    }

    [Fact]
    public async Task Saving_a_new_Arma_sends_the_chosen_Rank()
    {
        CreateEquipmentRequestCapture? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            captured = request.Content!.ReadFromJsonAsync<CreateEquipmentRequestCapture>().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new
            {
                Id = "item-new", Tipo = "Arma", Nome = "Espada", Peso = 1m, Preco = 5,
                ImageUrl = (string?)null, Subcategoria = (string?)null, Descricao = (string?)null, Rank = "D",
                Empunhadura = (string?)null, Dados = (string?)null, Dano = (int?)null, Critico = (string?)null,
                Alcance = (int?)null, TipoDeDano = (string?)null, RequisitoAtributo = (string?)null,
                DurabilidadeMaxima = (int?)80, Categoria = (string?)null, Defesa = (int?)null, RF = (int?)null,
                RM = (int?)null, Penalidade = (string?)null, RequisitoVigor = (int?)null, BonusDefesa = (int?)null,
                TipoDeAlvo = (string?)null, Alvo = (string?)null, Valor = (int?)null, CapacidadeExtra = (decimal?)null,
            }) };
        });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>(p => p
            .Add(x => x.FixedTipo, "Arma")
            .Add(x => x.OnCreated, EventCallback.Factory.Create<RuinaRPG.Contracts.Items.ItemResponse>(this, _ => { })));
        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Espada"));
        var rankSelect = cut.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Rank");
        await cut.InvokeAsync(() => rankSelect.Instance.ValueChanged.InvokeAsync("D"));

        await cut.InvokeAsync(() => cut.Instance.CreateForTestsAsync());

        captured.Should().NotBeNull();
        captured!.Rank.Should().Be("D");
    }

    private record CreateEquipmentRequestCapture(string Tipo, string Nome, decimal Peso, int Preco, string? ImageId,
        string? Subcategoria, string? Descricao, string? Rank, string? Empunhadura, string? Dados, int? Dano,
        string? Critico, int? Alcance, string? TipoDeDano, string? RequisitoAtributo, string? Categoria, int? Defesa,
        int? RF, int? RM, string? Penalidade, int? RequisitoVigor, int? BonusDefesa, string? TipoDeAlvo, string? Alvo,
        int? Valor, decimal? CapacidadeExtra);

    [Fact]
    public void TipoDeAlvo_offers_the_4_fixed_options()
    {
        CatalogoItemForm.TipoDeAlvoOptions.Select(o => o.Value).Should().BeEquivalentTo(new[] { "Atributo", "Pericia", "SubAtributo", "Dano" });
        CatalogoItemForm.TipoDeAlvoOptions.Should().Contain(o => o.Value == "Pericia" && o.Label == "Perícia");
    }

    [Fact]
    public void AlvoOptionsFor_Atributo_lists_the_8_Atributo_names()
    {
        var results = CatalogoItemForm.AlvoOptionsFor("Atributo");

        results.Should().BeEquivalentTo(new[] { "Instinto", "Vontade", "Vigor", "Influencia", "Agilidade", "Destreza", "Astucia", "Forca" });
    }

    [Fact]
    public void AlvoOptionsFor_Pericia_lists_all_39_Pericia_names()
    {
        var results = CatalogoItemForm.AlvoOptionsFor("Pericia");

        results.Should().HaveCount(39);
        results.Should().Contain("ArmasBrancas");
    }

    [Fact]
    public void AlvoOptionsFor_SubAtributo_lists_the_7_canonical_names()
    {
        var results = CatalogoItemForm.AlvoOptionsFor("SubAtributo");

        results.Should().BeEquivalentTo(new[]
        {
            "Iniciativa", "Movimentação", "Esquiva Natural", "Defesa Natural",
            "Redução Física", "Redução Mágica", "Adrenalina",
        });
    }

    [Fact]
    public void AlvoOptionsFor_Dano_lists_the_4_damage_types_including_Arcano()
    {
        var results = CatalogoItemForm.AlvoOptionsFor("Dano");

        results.Should().BeEquivalentTo(new[] { "Cortante", "Perfurante", "Contundente", "Arcano" });
    }

    [Fact]
    public async Task Changing_TipoDeAlvo_clears_the_previously_chosen_Alvo()
    {
        var form = RenderNewItemForm();

        await form.OnTipoDeAlvoChangedForTestsAsync("Forca");

        form.AlvoForTests.Should().BeNull();
    }

    [Theory]
    [InlineData("Arma")]
    [InlineData("Armadura")]
    [InlineData("Escudo")]
    [InlineData("Artefato")]
    public void ItemInicialSubcategoriaField_appears_for_Arma_Armadura_Escudo_and_Artefato(string tipo)
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, tipo));

        cut.FindComponents<RuinaRPG.Client.Shared.ItemInicialSubcategoriaField>().Should().ContainSingle();
    }

    [Fact]
    public void ItemInicialSubcategoriaField_does_not_appear_for_ItemGeral()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.FixedTipo, "ItemGeral"));

        cut.FindComponents<RuinaRPG.Client.Shared.ItemInicialSubcategoriaField>().Should().BeEmpty();
    }

    [Fact]
    public async Task Switching_top_level_Tipo_from_Armadura_to_Escudo_clears_the_mismatched_composed_Subcategoria()
    {
        // Integration regression for Task 10 fix round 1: the Armadura/Escudo branch renders a
        // single ItemInicialSubcategoriaField shared by both Tipos, so switching the top-level Tipo
        // select reuses the same instance. Proves the fix end-to-end through the real @bind-Value
        // wiring, not just at the field's own component level.
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>();

        var tipoSelect = cut.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Tipo");
        await cut.InvokeAsync(() => tipoSelect.Instance.ValueChanged.InvokeAsync("Armadura"));

        var field = cut.FindComponent<RuinaRPG.Client.Shared.ItemInicialSubcategoriaField>();
        var checkbox = field.FindComponents<MudBlazor.MudCheckBox<bool>>().Single(c => c.Instance.Label == "Item Inicial");
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));
        var categoriaSelect = field.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Categoria (Item Inicial)");
        await cut.InvokeAsync(() => categoriaSelect.Instance.ValueChanged.InvokeAsync("Pesada"));
        var familiaSelect = field.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Família (Item Inicial)");
        await cut.InvokeAsync(() => familiaSelect.Instance.ValueChanged.InvokeAsync("Placas"));

        field.Instance.IsCheckedForTests.Should().BeTrue();

        await cut.InvokeAsync(() => tipoSelect.Instance.ValueChanged.InvokeAsync("Escudo"));

        var fieldAfter = cut.FindComponent<RuinaRPG.Client.Shared.ItemInicialSubcategoriaField>();
        fieldAfter.Instance.IsCheckedForTests.Should().BeFalse("a composed Subcategoria built for Armadura is never valid once Tipo becomes Escudo");
        cut.FindComponents<MudBlazor.MudTextField<string>>().Should().Contain(c => c.Instance.Label == "Subcategoria" && c.Instance.Value == null);
    }

    [Fact]
    public async Task Switching_top_level_Tipo_from_Arma_to_Armadura_clears_a_composed_Subcategoria_built_for_Arma()
    {
        // Finding 8 of the final review: unlike the Armadura<->Escudo case (which reuses the same
        // ItemInicialSubcategoriaField instance and is covered by the earlier test above), Arma and
        // Armadura render in different @if branches — switching Tipo unmounts the Arma-branch field
        // and mounts a brand-new instance for the Armadura branch. That new instance's own
        // OnParametersSetAsync has no memory of "the old Tipo", so it never clears the mismatched
        // Value on its own — _form.Subcategoria (owned by CatalogoItemForm, not the field) must be
        // cleared by the parent itself when the top-level Tipo changes.
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>();

        var tipoSelect = cut.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Tipo");
        await cut.InvokeAsync(() => tipoSelect.Instance.ValueChanged.InvokeAsync("Arma"));

        var field = cut.FindComponent<RuinaRPG.Client.Shared.ItemInicialSubcategoriaField>();
        var checkbox = field.FindComponents<MudBlazor.MudCheckBox<bool>>().Single(c => c.Instance.Label == "Item Inicial");
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));
        var categoriaSelect = field.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Categoria (Item Inicial)");
        await cut.InvokeAsync(() => categoriaSelect.Instance.ValueChanged.InvokeAsync("Distância"));
        var familiaSelect = field.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Família (Item Inicial)");
        await cut.InvokeAsync(() => familiaSelect.Instance.ValueChanged.InvokeAsync("Arcos"));

        field.Instance.IsCheckedForTests.Should().BeTrue();

        await cut.InvokeAsync(() => tipoSelect.Instance.ValueChanged.InvokeAsync("Armadura"));

        var fieldAfter = cut.FindComponent<RuinaRPG.Client.Shared.ItemInicialSubcategoriaField>();
        fieldAfter.Instance.IsCheckedForTests.Should().BeFalse("a composed Subcategoria built for Arma is never valid once Tipo becomes Armadura");
        cut.FindComponents<MudBlazor.MudTextField<string>>().Should().Contain(c => c.Instance.Label == "Subcategoria" && c.Instance.Value == null);
    }

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
    public void Standalone_page_with_no_FixedTipo_and_no_OnCreated_shows_the_breadcrumb()
    {
        // Locks down the non-embedded (/catalogo/novo) case: it must keep its live navigation
        // trail. Guards against a fix for the Espólios bug (below) accidentally hiding the
        // breadcrumb everywhere.
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>();

        cut.FindComponents<RuinaRPG.Client.Shared.Breadcrumbs>().Should().ContainSingle();
    }

    [Fact]
    public void Embedded_with_OnCreated_but_no_FixedTipo_hides_the_breadcrumb()
    {
        // Regression test for the final-review finding: Espólios' CatalogoItemPicker call site
        // embeds this form with OnCreated set but FixedTipo left null (any Tipo is allowed there).
        // Gating the breadcrumb on "FixedTipo is null" wrongly showed it in this embedded dialog,
        // exposing the GM to the same accidental full-page-navigation risk the guard exists to
        // prevent. The guard must key off whether the form is embedded at all (OnCreated.HasDelegate),
        // not off FixedTipo's value.
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>(p => p
            .Add(x => x.OnCreated, EventCallback.Factory.Create<RuinaRPG.Contracts.Items.ItemResponse>(this, _ => { })));

        cut.FindComponents<RuinaRPG.Client.Shared.Breadcrumbs>().Should().BeEmpty();
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
                ImageUrl = (string?)null, Subcategoria = (string?)null, Descricao = (string?)null, Rank = (string?)null,
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
        // 2, not 1: OnInitializedAsync now also fetches the durabilidades-por-rank table
        // unconditionally (images/mine + durabilidades-por-rank) — the assertion still proves
        // CreateAsync/OnCreated doesn't trigger a further GET of its own.
        getCount.Should().Be(2, "OnCreated deve substituir a navegação, não disparar uma nova busca");
    }

    /// <summary>
    /// Unlike <see cref="FakeHttpMessageHandler"/>, actually yields before responding, so awaits
    /// on it do not resolve synchronously — reproducing the timing of a real HTTP round trip.
    /// </summary>
    private sealed class AsyncFakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        private AsyncFakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return _respond(request);
        }

        public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) => new(new AsyncFakeHttpMessageHandler(respond))
        {
            BaseAddress = new Uri("http://localhost/api/"),
        };
    }
}
