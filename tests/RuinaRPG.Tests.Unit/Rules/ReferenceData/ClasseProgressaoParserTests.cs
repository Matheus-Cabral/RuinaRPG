using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class ClasseProgressaoParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Classes.md (level 1 and level 20 rows, 2 classes).
    private const string Markdown = """
        |       | Duelista | Duelista | Guardião | Guardião |
        | :---: | :------: | :------: | :------: | :------: |
        | Nível |   Vida   |  Arcana  |   Vida   |  Arcana  |
        |   1   |    0     |    0     |    0     |    0     |
        |  20   |    8     |    8     |    12    |    4     |
        """;

    [Fact]
    public void Parse_extracts_one_entry_per_classe_per_level()
    {
        var result = ClasseProgressaoParser.Parse(Markdown);

        result.Should().HaveCount(4);
        result.Should().ContainSingle(r => r.Classe == "Duelista" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 0, Arcana = 0 });
        result.Should().ContainSingle(r => r.Classe == "Guardião" && r.Nivel == 20)
            .Which.Should().BeEquivalentTo(new { Vida = 12, Arcana = 4 });
    }
}
