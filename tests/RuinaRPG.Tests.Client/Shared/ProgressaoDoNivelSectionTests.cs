using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Rules.Niveis;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ProgressaoDoNivelSectionTests : MudBunitContext
{
    private static readonly Guid Pontos = Guid.NewGuid();
    private static readonly Guid MaxPericia = Guid.NewGuid();
    private static readonly Guid MaxAtributo = Guid.NewGuid();
    private static readonly Guid Xp = Guid.NewGuid();
    private static readonly Guid Eap = Guid.NewGuid();
    private static readonly Guid Custom = Guid.NewGuid();

    private static ColunaDeNivelResponse Col(Guid id, string nome, string tipo, string? chave, int ordem) =>
        new(id, nome, tipo, chave, chave is not null, ordem);

    private static LinhaDeNivelResponse Linha(int nivel, params (Guid, int?)[] valores) =>
        new(nivel, null, valores.ToDictionary(v => v.Item1, v => v.Item2));

    private void Serve(TabelaDeNiveisResponse tabela) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(tabela) }));

    private async Task<IRenderedComponent<ProgressaoDoNivelSection>> RenderAsync(TabelaDeNiveisResponse tabela, int nivel)
    {
        Serve(tabela);
        var cut = Render<ProgressaoDoNivelSection>(p => p.Add(x => x.Nivel, nivel));
        await Task.Delay(50);
        return cut;
    }

    [Fact]
    public async Task Shows_accumulated_totals_up_to_the_level()
    {
        var tabela = new TabelaDeNiveisResponse(
            [Col(Pontos, "Pontos de Atributo", "Acumulativa", ChavesDeNivel.PontosDeAtributo, 1)],
            [Linha(1, (Pontos, 9)), Linha(2, (Pontos, 1))]);

        var cut = await RenderAsync(tabela, 2);

        cut.Markup.Should().Contain("Pontos de Atributo").And.Contain("10");
    }

    [Fact]
    public async Task Shows_caps_with_inheritance_and_sem_limite_when_empty()
    {
        var tabela = new TabelaDeNiveisResponse(
            [Col(MaxPericia, "Máx. de Perícia", "PorNivel", ChavesDeNivel.MaxPericia, 1),
             Col(MaxAtributo, "Máx. de Atributo", "PorNivel", ChavesDeNivel.MaxAtributo, 2)],
            [Linha(1, (MaxPericia, 4), (MaxAtributo, null)), Linha(2), Linha(3)]);

        var cut = await RenderAsync(tabela, 3);

        cut.Markup.Should().Contain("máx. 4").And.Contain("sem limite");
    }

    [Fact]
    public async Task Hides_xp_and_eap_rows()
    {
        var tabela = new TabelaDeNiveisResponse(
            [Col(Xp, "XP para o próximo nível", "PorNivel", ChavesDeNivel.XpParaProximoNivel, 1),
             Col(Eap, "EAP base", "PorNivel", ChavesDeNivel.EapBase, 2),
             Col(Pontos, "Pontos de Atributo", "Acumulativa", ChavesDeNivel.PontosDeAtributo, 3)],
            [Linha(1, (Xp, 100), (Eap, 5), (Pontos, 9))]);

        var cut = await RenderAsync(tabela, 1);

        cut.Markup.Should().Contain("Pontos de Atributo");
        cut.Markup.Should().NotContain("XP para o próximo nível").And.NotContain("EAP base");
    }

    [Fact]
    public async Task Shows_custom_columns()
    {
        var tabela = new TabelaDeNiveisResponse(
            [Col(Custom, "Slots de Runa", "Acumulativa", null, 1)],
            [Linha(1, (Custom, 2)), Linha(2, (Custom, 3))]);

        var cut = await RenderAsync(tabela, 2);

        cut.Markup.Should().Contain("Slots de Runa").And.Contain("5");
    }

    [Fact]
    public async Task Renders_nothing_and_does_not_throw_when_the_table_cannot_be_loaded()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var cut = Render<ProgressaoDoNivelSection>(p => p.Add(x => x.Nivel, 1));
        await Task.Delay(50);

        cut.Markup.Should().NotContain("Progressão do nível");
    }
}
