using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class NivelBonusParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Níveis.md (levels 1, 2, 50).
    private const string Markdown = """
        | NÍVEL | BÔNUS                                                                                                                                                                                           |
        | :---: | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
        |   1   | +9 Pontos de Atributo  <br>+Status de Vida Aprimorado  <br>+Status de Foco Aprimorado  <br>+10 Pontos de Ignição  <br>+4 Pontos de Perícia  <br>+1 Espaço de Maestria  <br>+1 Ponto de Maestria |
        |   2   | +4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco  <br>+1 Ponto de Atributo                                                                                                             |
        |  50   | +30 Pontos de Ignição  <br>+Status Aprimorado de Vida/Foco (Total)  <br>+4 Pontos de Perícia  <br>+ Ultima Passiva  <br>+8 Pontos de atributo  <br>+1 Espaço de Maestria                       |
        """;

    [Fact]
    public void Parse_extracts_one_entry_per_level_row()
    {
        var result = NivelBonusParser.Parse(Markdown);

        result.Should().HaveCount(3);
        result[0].Nivel.Should().Be(1);
        result[0].BonusText.Should().Contain("+9 Pontos de Atributo");
        result[2].Nivel.Should().Be(50);
        result[2].BonusText.Should().Contain("Ultima Passiva");
    }
}
