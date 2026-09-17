using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class AddEfeitoFormTests : MudBunitContext
{
    private static HttpClient FakeCatalogClient(object efeitos)
    {
        return FakeHttpMessageHandler.CreateClient(request =>
            request.RequestUri!.AbsolutePath.EndsWith("efeitos")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(efeitos) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static object FixoEfeito(string nome, int grau, int custo, params string[][] preRequisitos) => new
    {
        Id = Guid.NewGuid().ToString(), Nome = nome, Grau = grau, Descricao = "Descrição.", TipoDeCusto = "Fixo",
        CustoFixo = custo, CustoPorUnidade = (int?)null, UnidadeLabel = (string?)null, QuantidadeDerivadaDeEfeito = (string?)null,
        MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
        CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
        PreRequisitos = preRequisitos,
    };

    private static object PorUnidadeEfeito(string nome, int grau, int custoPorUnidade, params string[][] preRequisitos) => new
    {
        Id = Guid.NewGuid().ToString(), Nome = nome, Grau = grau, Descricao = "Descrição.", TipoDeCusto = "PorUnidade",
        CustoFixo = (int?)null, CustoPorUnidade = custoPorUnidade, UnidadeLabel = "Dado", QuantidadeDerivadaDeEfeito = (string?)null,
        MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
        CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
        PreRequisitos = preRequisitos,
    };

    /// <summary>
    /// MudSelect's dropdown is a real portal: its items render only inside a MudPopoverProvider
    /// (absent by default outside MainLayout — only <see cref="AddEfeitoForm"/> is under test here)
    /// and only once the select is actually open — mirroring the same MudAutocomplete popover
    /// limitation EntityPickerTests documents for a sibling component. A bare
    /// <c>Render&lt;AddEfeitoForm&gt;()</c> therefore never shows item text in <c>cut.Markup</c>
    /// regardless of the component's filtering logic. This helper renders AddEfeitoForm alongside a
    /// MudPopoverProvider in one composite fragment (so both land in the one returned markup) and
    /// opens the dropdown by dispatching the same MouseDown MudSelect's own input listens for,
    /// instead of asserting against a closed, unpopulated popover.
    /// </summary>
    private async Task<IRenderedComponent<ContainerFragment>> RenderOpenAsync(int grau, IReadOnlyList<(string Nome, int? Quantidade)> efeitosExistentes,
        List<EfeitoAdicionadoResult>? loteCapturado = null)
    {
        var root = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AddEfeitoForm>(1);
            builder.AddAttribute(2, nameof(AddEfeitoForm.Grau), grau);
            builder.AddAttribute(3, nameof(AddEfeitoForm.EfeitosExistentes), efeitosExistentes);
            if (loteCapturado is not null)
            {
                builder.AddAttribute(4, nameof(AddEfeitoForm.OnAdicionar),
                    EventCallback.Factory.Create<List<EfeitoAdicionadoResult>>(this, r => loteCapturado.AddRange(r)));
            }
            builder.CloseComponent();
        });
        await Task.Delay(50); // let the fake HTTP fetch + OnParametersSetAsync settle before opening
        root.Find(".mud-input-control").MouseDown();
        return root;
    }

    private static void Selecionar(IRenderedComponent<ContainerFragment> cut, string nome)
    {
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(nome));
        cut.FindAll(".mud-list-item").First(li => li.TextContent.Trim() == nome).Click();
    }

    [Fact]
    public async Task Only_effects_whose_Grau_is_at_or_below_the_parents_Grau_are_offered()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Contrato Mágico", 1, 3),
            FixoEfeito("Atordoamento", 8, 10, ["Duração"]),
        });
        Services.AddScoped(_ => http);

        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>());

        cut.Markup.Should().Contain("Contrato Mágico");
        cut.Markup.Should().NotContain("Atordoamento");
    }

    [Fact]
    public async Task Every_Grau_eligible_effect_is_offered_regardless_of_unmet_prerequisites()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Duração", 1, 0),
            FixoEfeito("Cura", 1, 2, ["Dano"]),
        });
        Services.AddScoped(_ => http);

        // Nem "Dano" nem "Duração" estão presentes — Cura era escondida antes desta mudança.
        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>());

        cut.Markup.Should().Contain("Cura");
    }

    [Fact]
    public async Task Confirming_an_effect_without_its_prerequisite_present_auto_adds_the_prerequisite_first()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Duração", 1, 0),
            PorUnidadeEfeito("Aumentar Armadura", 1, 2, ["Duração"]),
        });
        Services.AddScoped(_ => http);
        var lote = new List<EfeitoAdicionadoResult>();

        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>(), lote);
        Selecionar(cut, "Aumentar Armadura");

        var quantidade = cut.FindComponents<MudNumericField<int?>>().Single();
        await cut.InvokeAsync(() => quantidade.Instance.ValueChanged.InvokeAsync(3));

        cut.Find("button:contains('Adicionar Efeito')").Click();

        lote.Should().HaveCount(2);
        lote[0].Should().Be(new EfeitoAdicionadoResult("Duração", 0, 0));
        lote[1].Should().Be(new EfeitoAdicionadoResult("Aumentar Armadura", 3, 6));
    }

    [Fact]
    public async Task Confirming_a_Fixo_prerequisite_target_auto_adds_it_already_fully_resolved()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Dano", 1, 2),
            FixoEfeito("Cura", 1, 2, ["Dano"]),
        });
        Services.AddScoped(_ => http);
        var lote = new List<EfeitoAdicionadoResult>();

        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>(), lote);
        Selecionar(cut, "Cura");

        cut.Find("button:contains('Adicionar Efeito')").Click();

        lote.Should().HaveCount(2);
        lote[0].Should().Be(new EfeitoAdicionadoResult("Dano", null, 2));
        lote[1].Should().Be(new EfeitoAdicionadoResult("Cura", null, 2));
    }

    [Fact]
    public async Task Confirming_an_effect_whose_prerequisite_group_has_multiple_candidates_prompts_a_choice()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Duração", 1, 0),
            FixoEfeito("Congelar", 1, 4, ["Duração"]),
            FixoEfeito("Enraizar", 1, 2, ["Duração"]),
            FixoEfeito("Detrito", 1, 2, ["Duração"], ["Congelar", "Enraizar"]),
        });
        Services.AddScoped(_ => http);
        var lote = new List<EfeitoAdicionadoResult>();

        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>(), lote);
        Selecionar(cut, "Detrito");

        cut.Find("button:contains('Adicionar Efeito')").Click();

        cut.Markup.Should().Contain("Congelar");
        cut.Markup.Should().Contain("Enraizar");
        lote.Should().BeEmpty("a escolha ainda não foi feita — nada deve ser emitido ainda");

        cut.Find("button:contains('Congelar')").Click();

        lote.Should().HaveCount(3);
        lote[0].Should().Be(new EfeitoAdicionadoResult("Duração", 0, 0));
        lote[1].Should().Be(new EfeitoAdicionadoResult("Congelar", null, 4));
        lote[2].Should().Be(new EfeitoAdicionadoResult("Detrito", null, 2));
    }

    [Fact]
    public async Task Selecting_a_DerivadoDeOutroEfeito_effect_without_its_source_present_computes_a_zero_cost_instead_of_blocking()
    {
        var http = FakeCatalogClient(new[]
        {
            new
            {
                Id = Guid.NewGuid().ToString(), Nome = "Dreno de Vitalidade", Grau = 3, Descricao = "Descrição.", TipoDeCusto = "DerivadoDeOutroEfeito",
                CustoFixo = (int?)null, CustoPorUnidade = (int?)2, UnidadeLabel = "Dado", QuantidadeDerivadaDeEfeito = "Dano",
                MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
                CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
                PreRequisitos = new[] { new[] { "Dano" } },
            },
            PorUnidadeEfeito("Dano", 1, 2),
        });
        Services.AddScoped(_ => http);
        var lote = new List<EfeitoAdicionadoResult>();

        var cut = await RenderOpenAsync(3, Array.Empty<(string, int?)>(), lote);
        Selecionar(cut, "Dreno de Vitalidade");

        cut.Markup.Should().Contain("Custo em PI: 0");

        cut.Find("button:contains('Adicionar Efeito')").Click();

        lote.Should().HaveCount(2);
        lote[0].Should().Be(new EfeitoAdicionadoResult("Dano", 0, 0));
        lote[1].Should().Be(new EfeitoAdicionadoResult("Dreno de Vitalidade", null, 0));
    }
}
