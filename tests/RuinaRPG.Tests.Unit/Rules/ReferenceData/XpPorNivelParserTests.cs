using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class XpPorNivelParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabelas de XP, Atributos, Características e EAP.md — the FIRST
    // of the four tables in that file (XP máximo por nível), plus a second small table to prove multi-table handling.
    private const string Markdown = """
        |       | XP máximo por nível | XP máximo por nível |
        | :---: | :-----------------: | :-----------------: |
        | Nível |  Valores absolutos  |  Valores relativos  |
        |   1   |         50          |         50          |
        |  50   |      Lvl. Max       |      Lvl. Max       |

        |       |     Atributos     |    Atributos    |
        | :---: | :---------------: | :-------------: |
        | Nível | Valores Absolutos | Ganho por Nível |
        |   1   |         9         |        0        |
        """;

    [Fact]
    public void Parse_reads_only_the_first_XP_table_and_tolerates_non_numeric_cells()
    {
        var result = XpPorNivelParser.Parse(Markdown);

        result.Should().HaveCount(2);
        result[0].Should().BeEquivalentTo(new { Nivel = 1, XpAbsoluto = "50", XpRelativo = "50" });
        result[1].Should().BeEquivalentTo(new { Nivel = 50, XpAbsoluto = "Lvl. Max", XpRelativo = "Lvl. Max" });
    }
}
