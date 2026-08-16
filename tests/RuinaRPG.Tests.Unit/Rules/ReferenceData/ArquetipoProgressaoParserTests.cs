using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class ArquetipoProgressaoParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Arquetipos.md (level 1 row).
    private const string Markdown = """
        |       | Fisico | Fisico | Arcano | Arcano |
        | :---: | :----: | :----: | :----: | :----: |
        | Nível |  Vida  | Arcana |  Vida  | Arcana |
        |   1   |   8    |   4    |   4    |   8    |
        """;

    [Fact]
    public void Parse_extracts_one_entry_per_arquetipo_per_level()
    {
        var result = ArquetipoProgressaoParser.Parse(Markdown);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Arquetipo == "Fisico" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 8, Arcana = 4 });
        result.Should().ContainSingle(r => r.Arquetipo == "Arcano" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 4, Arcana = 8 });
    }
}
