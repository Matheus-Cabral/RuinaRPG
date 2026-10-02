using FluentAssertions;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Rules;
using Xunit;

namespace RuinaRPG.Tests.Unit.Rules;

public class TabelaDeNiveisHtmlTests
{
    private static readonly ColunaDeNivelDef Atributo = new(Guid.NewGuid(), "Pontos de Atributo", TipoDeColunaDeNivel.Acumulativa, ChavesDeNivel.PontosDeAtributo, 0);
    private static readonly ColunaDeNivelDef MaxAtributo = new(Guid.NewGuid(), "Máx. de Atributo", TipoDeColunaDeNivel.PorNivel, ChavesDeNivel.MaxAtributo, 1);
    private static readonly ColunaDeNivelDef Xp = new(Guid.NewGuid(), "XP para o próximo nível", TipoDeColunaDeNivel.PorNivel, ChavesDeNivel.XpParaProximoNivel, 2);
    private static readonly ColunaDeNivelDef Fama = new(Guid.NewGuid(), "Fama", TipoDeColunaDeNivel.Acumulativa, null, 3);

    private static ProgressaoDeNivel Tabela(params ColunaDeNivelDef[] colunas) => new(
        colunas,
        new[]
        {
            new LinhaDeNivel(1, "Status de Vida\nStatus de Foco", new Dictionary<Guid, int?> { [Atributo.Id] = 9, [Xp.Id] = 50, [Fama.Id] = 2 }),
            new LinhaDeNivel(2, null, new Dictionary<Guid, int?> { [Xp.Id] = 150 }),
            new LinhaDeNivel(3, null, new Dictionary<Guid, int?>()),
        });

    [Fact]
    public void Main_table_lists_one_bonus_per_line_and_a_dash_for_levels_that_grant_nothing()
    {
        var html = TabelaDeNiveisHtml.Montar(Tabela(Atributo, MaxAtributo, Xp, Fama), mostrarLimites: false);

        html.Should().Contain("<th>Nível</th><th>Bônus</th>");
        html.Should().Contain("<td>1</td><td>Pontos de Atributo: +9<br />Fama: +2<br />Status de Vida<br />Status de Foco</td>");
        html.Should().Contain("<td>2</td><td>—</td>");
        html.Should().Contain("<td>3</td><td>—</td>");
    }

    [Fact]
    public void Limits_table_is_omitted_when_the_flag_is_off_even_if_Por_nivel_columns_have_values()
    {
        var html = TabelaDeNiveisHtml.Montar(Tabela(Atributo, MaxAtributo, Xp, Fama), mostrarLimites: false);

        html.Should().NotContain("Limites e progressão").And.NotContain("XP para o próximo nível");
        html.Split("<table").Length.Should().Be(2);
    }

    [Fact]
    public void Limits_table_is_present_when_the_flag_is_on_and_a_Por_nivel_column_has_values()
    {
        var html = TabelaDeNiveisHtml.Montar(Tabela(Atributo, MaxAtributo, Xp, Fama), mostrarLimites: true);

        html.Should().Contain("<h3>Limites e progressão</h3>");
        html.Split("<table").Length.Should().Be(3);
    }

    [Fact]
    public void Limits_table_only_has_Por_nivel_columns_with_a_value_and_dashes_for_empty_cells()
    {
        var html = TabelaDeNiveisHtml.Montar(Tabela(Atributo, MaxAtributo, Xp, Fama), mostrarLimites: true);

        html.Should().Contain("<h3>Limites e progressão</h3>");
        html.Should().Contain("<th>Nível</th><th>XP para o próximo nível</th>");
        html.Should().NotContain("Máx. de Atributo");
        html.Should().Contain("<td>1</td><td>50</td>");
        html.Should().Contain("<td>3</td><td>—</td>");
    }

    [Fact]
    public void Limits_heading_and_table_are_omitted_when_no_Por_nivel_column_has_values()
    {
        var html = TabelaDeNiveisHtml.Montar(Tabela(Atributo, MaxAtributo), mostrarLimites: true);

        html.Should().NotContain("Limites e progressão");
        html.Split("<table").Length.Should().Be(2);
    }

    [Fact]
    public void Names_and_free_text_are_html_encoded()
    {
        var evil = new ColunaDeNivelDef(Guid.NewGuid(), "<b>x</b>", TipoDeColunaDeNivel.Acumulativa, null, 0);
        var porNivel = new ColunaDeNivelDef(Guid.NewGuid(), "<i>y</i>", TipoDeColunaDeNivel.PorNivel, null, 1);
        var t = new ProgressaoDeNivel(new[] { evil, porNivel }, new[]
        {
            new LinhaDeNivel(1, "<script>a</script>", new Dictionary<Guid, int?> { [evil.Id] = 1, [porNivel.Id] = 3 }),
        });

        var html = TabelaDeNiveisHtml.Montar(t, mostrarLimites: true);

        html.Should().Contain("&lt;b&gt;x&lt;/b&gt;: +1").And.Contain("&lt;script&gt;a&lt;/script&gt;").And.Contain("<th>&lt;i&gt;y&lt;/i&gt;</th>");
        html.Should().NotContain("<b>").And.NotContain("<script>").And.NotContain("<i>");
    }
}
