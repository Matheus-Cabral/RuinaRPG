using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class EapPorNivelParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabelas de XP, Atributos, Características e EAP.md — all
    // 4 tables in that file, in order (XP, Atributos, Características, EAP), so ElementAt(3) lands
    // on the right one. Unlike the XP table, EAP has no "Lvl. Max" placeholder at level 50.
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

        |       |     Características     |    Características    |
        | :---: | :---------------------: | :--------------------: |
        | Nível |    Valores Absolutos    |    Ganho por Nível     |
        |   1   |            0             |            0            |

        |       |        EAP        |       EAP       |
        | :---: | :---------------: | :-------------: |
        | Nível | Valores absolutos | Ganho por nível |
        |   1   |         0          |       30        |
        |   2   |         30         |       30        |
        """;

    [Fact]
    public void Parse_reads_only_the_4th_EAP_table()
    {
        var result = EapPorNivelParser.Parse(Markdown);

        result.Should().HaveCount(2);
        result[0].Should().BeEquivalentTo(new { Nivel = 1, ValorAbsoluto = 0 });
        result[1].Should().BeEquivalentTo(new { Nivel = 2, ValorAbsoluto = 30 });
    }
}
