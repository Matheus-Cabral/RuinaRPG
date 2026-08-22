using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class CirculoGrauPorEapParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Circulo e Grau por EAP.md (rows 0, 1, 9).
    private const string Markdown = """
        |              | EAP máximo por círculo/grau | EAP para Próximo  |     Afinidade     |    Afinidade    |
        | :----------: | :-------------------------: | :---------------: | :---------------: | :-------------: |
        | Círculo/Grau |      Valores absolutos      | Valores relativos | Valores Absolutos | Ganho por Nível |
        |      0       |              0              |         0         |         3         |        0        |
        |      1       |             100             |        100        |         5         |        2        |
        |      9       |            Max.             |       Max.        |        21         |        2        |
        """;

    [Fact]
    public void Parse_handles_numeric_and_non_numeric_EAP_cells()
    {
        var result = CirculoGrauPorEapParser.Parse(Markdown);

        result.Should().HaveCount(3);
        result[0].Should().BeEquivalentTo(new { CirculoOuGrau = 0, EapAbsoluto = "0", AfinidadeAbsoluta = 3, AfinidadeGanhoPorNivel = 0 });
        result[1].Should().BeEquivalentTo(new { CirculoOuGrau = 1, EapAbsoluto = "100", AfinidadeAbsoluta = 5 });
        result[2].Should().BeEquivalentTo(new { CirculoOuGrau = 9, EapAbsoluto = "Max.", EapRelativo = "Max." });
    }
}
