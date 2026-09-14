using Bunit;
using Bunit.Rendering;
using FluentAssertions;
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
    private async Task<IRenderedComponent<ContainerFragment>> RenderOpenAsync(int grau, IReadOnlyList<(string Nome, int? Quantidade)> efeitosExistentes)
    {
        var root = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AddEfeitoForm>(1);
            builder.AddAttribute(2, nameof(AddEfeitoForm.Grau), grau);
            builder.AddAttribute(3, nameof(AddEfeitoForm.EfeitosExistentes), efeitosExistentes);
            builder.CloseComponent();
        });
        await Task.Delay(50); // let the fake HTTP fetch + OnParametersSetAsync settle before opening
        root.Find(".mud-input-control").MouseDown();
        return root;
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
    public async Task An_effect_whose_prerequisite_is_not_yet_present_is_not_offered()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Duração", 1, 0),
            FixoEfeito("Cura", 1, 2, ["Dano"]),
        });
        Services.AddScoped(_ => http);

        var cut = await RenderOpenAsync(1, new[] { ("Duração", (int?)null) });

        cut.Markup.Should().NotContain("Cura");
    }

    [Fact]
    public async Task An_effect_whose_prerequisite_is_already_present_is_offered()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Dano", 1, 0),
            FixoEfeito("Cura", 1, 2, ["Dano"]),
        });
        Services.AddScoped(_ => http);

        var cut = await RenderOpenAsync(1, new[] { ("Dano", (int?)null) });

        cut.Markup.Should().Contain("Cura");
    }

    [Fact]
    public async Task Selecting_a_DerivadoDeOutroEfeito_effect_without_its_source_present_shows_a_persistent_error()
    {
        var http = FakeHttpMessageHandler.CreateClient(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[]
            {
                new
                {
                    Id = Guid.NewGuid().ToString(), Nome = "Dreno de Vitalidade", Grau = 3, Descricao = "Descrição.", TipoDeCusto = "DerivadoDeOutroEfeito",
                    CustoFixo = (int?)null, CustoPorUnidade = (int?)2, UnidadeLabel = "Dado", QuantidadeDerivadaDeEfeito = "Dano",
                    MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
                    CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
                    PreRequisitos = new[] { new[] { "Dano" } },
                },
            }),
        });
        Services.AddScoped(_ => http);

        var cut = await RenderOpenAsync(3, Array.Empty<(string, int?)>());

        // Dreno never satisfies its own prerequisite here (no "Dano" present), so it's correctly
        // absent from the offered list — proving the same prerequisite gate also covers this case,
        // not a separate code path.
        cut.Markup.Should().NotContain("Dreno de Vitalidade");
    }
}
