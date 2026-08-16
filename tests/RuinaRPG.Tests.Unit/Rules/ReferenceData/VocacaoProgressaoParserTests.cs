using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class VocacaoProgressaoParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Vocação.md (level 1 row, all 5 vocações).
    private const string Markdown = """
        | Nível | Campeão | Campeão | Caçador | Caçador | Feiticeiro | Feiticeiro | Adepto | Adepto | Bruxo | Bruxo  |
        | :---: | :-----: | :-----: | :-----: | :-----: | :--------: | :--------: | :----: | :----: | :---: | :----: |
        |       |  Vida   | Arcana  |  Vida   | Arcana  |    Vida    |   Arcana   |  Vida  | Arcana | Vida  | Arcana |
        |   1   |    8    |    4    |    6    |    6    |     4      |     8      |   4    |   8    |   4   |   8    |
        """;

    [Fact]
    public void Parse_extracts_one_entry_per_vocacao_per_level()
    {
        var result = VocacaoProgressaoParser.Parse(Markdown);

        result.Should().HaveCount(5);
        result.Should().ContainSingle(r => r.Vocacao == "Campeão" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 8, Arcana = 4 });
        result.Should().ContainSingle(r => r.Vocacao == "Feiticeiro" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 4, Arcana = 8 });
    }
}
