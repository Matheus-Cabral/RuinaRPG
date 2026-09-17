using Bunit;
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

public class EfeitosTableTests : MudBunitContext
{
    private static HttpClient FakeCatalogClient(object efeitos) => FakeHttpMessageHandler.CreateClient(request =>
        request.RequestUri!.AbsolutePath.EndsWith("efeitos")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(efeitos) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));

    private static object Efeito(string nome, string tipoDeCusto, int? custoFixo = null, int? custoPorUnidade = null,
        string? quantidadeDerivadaDeEfeito = null) => new
    {
        Id = Guid.NewGuid().ToString(), Nome = nome, Grau = 1, Descricao = "Descrição.", TipoDeCusto = tipoDeCusto,
        CustoFixo = custoFixo, CustoPorUnidade = custoPorUnidade, UnidadeLabel = (string?)null,
        QuantidadeDerivadaDeEfeito = quantidadeDerivadaDeEfeito, MaxUnidades = (int?)null, MaxEscalaPorGrau = false,
        MaxContandoAPartirDoGrau = (int?)null, CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
        PreRequisitos = Array.Empty<string[]>(),
    };

    [Fact]
    public async Task Editing_Quantidade_of_a_PorUnidade_row_recomputes_its_own_Custo()
    {
        var http = FakeCatalogClient(new[] { Efeito("Duração", "PorUnidade", custoPorUnidade: 4) });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Duração", 0, 0) };

        var cut = Render<EfeitosTable>(p => p.Add(x => x.Efeitos, efeitos).Add(x => x.Grau, 3));
        await Task.Delay(50);

        var quantidade = cut.FindComponents<MudNumericField<int?>>().Single();
        await cut.InvokeAsync(() => quantidade.Instance.ValueChanged.InvokeAsync(2));

        efeitos[0].Quantidade.Should().Be(2);
        efeitos[0].CustoPI.Should().Be(8); // 4 por unidade * 2
    }

    [Fact]
    public async Task Editing_Quantidade_of_a_row_another_row_derives_from_recomputes_the_dependent_too()
    {
        var http = FakeCatalogClient(new[]
        {
            Efeito("Dano", "PorUnidade", custoPorUnidade: 2),
            Efeito("Dreno de Vitalidade", "DerivadoDeOutroEfeito", custoPorUnidade: 2, quantidadeDerivadaDeEfeito: "Dano"),
        });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Dano", 3, 6), new("Dreno de Vitalidade", null, 6) };

        var cut = Render<EfeitosTable>(p => p.Add(x => x.Efeitos, efeitos).Add(x => x.Grau, 3));
        await Task.Delay(50);

        var quantidade = cut.FindComponents<MudNumericField<int?>>().Single(); // só "Dano" tem Quantidade editável
        await cut.InvokeAsync(() => quantidade.Instance.ValueChanged.InvokeAsync(5));

        efeitos[0].CustoPI.Should().Be(10); // 2 * 5
        efeitos[1].CustoPI.Should().Be(10); // 2 * quantidadeDerivada(5)
    }

    [Fact]
    public async Task A_Fixo_row_has_no_editable_fields()
    {
        var http = FakeCatalogClient(new[] { Efeito("Contrato Mágico", "Fixo", custoFixo: 3) });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Contrato Mágico", null, 3) };

        var cut = Render<EfeitosTable>(p => p.Add(x => x.Efeitos, efeitos).Add(x => x.Grau, 1));
        await Task.Delay(50);

        cut.FindComponents<MudNumericField<int?>>().Should().BeEmpty();
        cut.FindComponents<MudNumericField<int>>().Should().BeEmpty();
        cut.Markup.Should().Contain("—");
    }

    [Fact]
    public async Task A_Manual_row_has_an_editable_Custo_with_no_automatic_recompute()
    {
        var http = FakeCatalogClient(new[] { Efeito("Imunidade", "Manual") });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Imunidade", null, 5) };

        var cut = Render<EfeitosTable>(p => p.Add(x => x.Efeitos, efeitos).Add(x => x.Grau, 9));
        await Task.Delay(50);

        var custo = cut.FindComponents<MudNumericField<int>>().Single();
        await cut.InvokeAsync(() => custo.Instance.ValueChanged.InvokeAsync(12));

        efeitos[0].CustoPI.Should().Be(12);
    }

    [Fact]
    public async Task Removing_a_row_removes_it_from_the_bound_list_and_notifies_OnChanged()
    {
        var http = FakeCatalogClient(new[] { Efeito("Contrato Mágico", "Fixo", custoFixo: 3) });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Contrato Mágico", null, 3) };
        var changed = false;

        var cut = Render<EfeitosTable>(p => p
            .Add(x => x.Efeitos, efeitos)
            .Add(x => x.Grau, 1)
            .Add(x => x.OnChanged, EventCallback.Factory.Create(this, () => changed = true)));
        await Task.Delay(50);

        cut.Find("button").Click();

        efeitos.Should().BeEmpty();
        changed.Should().BeTrue();
    }
}
