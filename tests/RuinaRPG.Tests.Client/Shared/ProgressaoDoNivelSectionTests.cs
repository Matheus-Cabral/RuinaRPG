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
    public async Task Passiva_columns_show_the_additive_limit_or_sem_limite_when_the_column_is_empty()
    {
        var livres = Guid.NewGuid();
        var vocacionais = Guid.NewGuid();
        var tabela = new TabelaDeNiveisResponse(
            [Col(livres, "Passivas Livres", "Acumulativa", ChavesDeNivel.MaxPassivasLivres, 1),
             Col(vocacionais, "Passivas Vocacionais", "Acumulativa", ChavesDeNivel.MaxPassivasVocacionais, 2)],
            [Linha(1, (livres, 1), (vocacionais, null)), Linha(2), Linha(3, (livres, null))]);

        var cut = await RenderAsync(tabela, 3);

        var linhas = cut.FindAll("tr").Select(r => r.TextContent.Trim()).ToList();
        linhas.Should().Contain(l => l.StartsWith("Passiva Livre") && l.EndsWith("1"));
        linhas.Should().Contain(l => l.StartsWith("Passiva Vocacional") && l.EndsWith("sem limite"));
        cut.Markup.Should().NotContain("Passivas Livres").And.NotContain("Passivas Vocacionais");
        cut.Markup.Should().NotContain("máx.");
    }

    [Fact]
    public async Task Wildcard_column_shows_as_Habilidade_Passiva_with_the_accumulated_count_and_zero_when_empty()
    {
        var coringa = Guid.NewGuid();
        var comValor = new TabelaDeNiveisResponse(
            [Col(coringa, "Passivas Coringa", "Acumulativa", ChavesDeNivel.MaxPassivasCoringa, 1)],
            [Linha(1, (coringa, 1)), Linha(2, (coringa, 1))]);

        var cut = await RenderAsync(comValor, 2);

        var linhas = cut.FindAll("tr").Select(r => r.TextContent.Trim()).ToList();
        linhas.Should().ContainSingle(l => l.StartsWith("Habilidade Passiva") && l.EndsWith("2"));
        cut.Markup.Should().NotContain("Passivas Coringa").And.NotContain("sem limite");
    }

    [Fact]
    public async Task Empty_wildcard_column_shows_zero_not_sem_limite()
    {
        var coringa = Guid.NewGuid();
        var vazia = new TabelaDeNiveisResponse(
            [Col(coringa, "Passivas Coringa", "Acumulativa", ChavesDeNivel.MaxPassivasCoringa, 1)],
            [Linha(1), Linha(2)]);

        var cut = await RenderAsync(vazia, 2);

        cut.FindAll("tr").Select(r => r.TextContent.Trim()).Should().ContainSingle(l => l.StartsWith("Habilidade Passiva") && l.EndsWith("0"));
        cut.Markup.Should().NotContain("sem limite");
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

    [Fact]
    public async Task Custom_por_nivel_column_shows_the_plain_value_or_a_dash_never_a_cap()
    {
        var comValor = Guid.NewGuid();
        var vazia = Guid.NewGuid();
        var tabela = new TabelaDeNiveisResponse(
            [Col(comValor, "Bênçãos", "PorNivel", null, 1), Col(vazia, "Títulos", "PorNivel", null, 2)],
            [Linha(1, (comValor, 7), (vazia, null))]);

        var cut = await RenderAsync(tabela, 1);

        cut.Markup.Should().Contain("Bênçãos").And.Contain("Títulos").And.Contain("7").And.Contain("—");
        cut.Markup.Should().NotContain("máx.").And.NotContain("sem limite");
    }

    [Fact]
    public async Task A_column_with_an_unknown_tipo_is_skipped_without_dropping_the_panel()
    {
        var tabela = new TabelaDeNiveisResponse(
            [Col(Pontos, "Pontos de Atributo", "Acumulativa", ChavesDeNivel.PontosDeAtributo, 1),
             Col(Custom, "Coluna Estranha", "Futuro", null, 2)],
            [Linha(1, (Pontos, 9), (Custom, 3))]);

        var cut = await RenderAsync(tabela, 1);

        cut.Markup.Should().Contain("Pontos de Atributo").And.NotContain("Coluna Estranha");
    }

    [Fact]
    public async Task The_table_lives_in_an_expansion_panel_that_starts_collapsed()
    {
        var tabela = new TabelaDeNiveisResponse(
            [Col(Pontos, "Pontos de Atributo", "Acumulativa", ChavesDeNivel.PontosDeAtributo, 1)],
            [Linha(1, (Pontos, 9))]);

        var cut = await RenderAsync(tabela, 1);

        var painel = cut.Find(".mud-expand-panel");
        painel.ClassList.Should().NotContain("mud-panel-expanded");
        painel.TextContent.Should().Contain("Ver saldos e limites do nível");
        cut.Markup.Should().Contain("Pontos de Atributo");
    }

    [Fact]
    public async Task The_section_title_and_its_info_popup_stay_outside_the_panel()
    {
        var tabela = new TabelaDeNiveisResponse([Col(Pontos, "Pontos de Atributo", "Acumulativa", ChavesDeNivel.PontosDeAtributo, 1)], [Linha(1, (Pontos, 9))]);

        var cut = await RenderAsync(tabela, 1);

        cut.Find(".rr-section-title").TextContent.Should().Contain("Progressão do nível");
        cut.Find(".mud-expand-panel").QuerySelectorAll(".rr-section-title").Should().BeEmpty();
    }

    [Fact]
    public async Task Clicking_the_header_expands_it()
    {
        var tabela = new TabelaDeNiveisResponse([Col(Pontos, "Pontos de Atributo", "Acumulativa", ChavesDeNivel.PontosDeAtributo, 1)], [Linha(1, (Pontos, 9))]);
        var cut = await RenderAsync(tabela, 1);

        cut.Find(".mud-expand-panel-header").Click();

        cut.Find(".mud-expand-panel").ClassList.Should().Contain("mud-panel-expanded");
    }
}
