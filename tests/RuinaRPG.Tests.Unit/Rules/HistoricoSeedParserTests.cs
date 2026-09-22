using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class HistoricoSeedParserTests
{
    // Real shape from Docs/Sistema RPG/Historico.md: numbered "# N. Nome" headings, a description
    // paragraph, then a "+6 X"/"+3 Y" pair, separated by "---" lines (which the parser must ignore,
    // same as it ignores the intro paragraph before the first heading). One entry uses a
    // multi-word/accented Perícia label ("Empatia c/ Animais") to make sure the label lookup
    // isn't limited to single, unaccented words.
    private const string Markdown = """
        Texto introdutório que não pertence a nenhum Histórico.

        Cada historico concede +6 em uma perícia e +3 em outra.

        ---

        # 1. Estudo Acadêmico

        O personagem passou anos cercado por livros, mestres e estudos.

        +6 Arcano
        +3 Biblioteca

        ---

        # 19. Afinidade Animal

        O personagem sempre teve facilidade para se aproximar de animais.

        +6 Empatia c/ Animais
        +3 Sobrevivência
        """;

    [Fact]
    public void Parse_extracts_Nome_Descricao_and_the_two_bonified_Pericias()
    {
        var result = HistoricoSeedParser.Parse(Markdown);

        var estudoAcademico = result.Should().ContainSingle(h => h.Nome == "Estudo Acadêmico").Subject;
        estudoAcademico.Descricao.Should().Be("O personagem passou anos cercado por livros, mestres e estudos.");
        estudoAcademico.PericiaMaisSeis.Should().Be(Pericia.Arcano);
        estudoAcademico.PericiaMaisTres.Should().Be(Pericia.Biblioteca);
    }

    [Fact]
    public void Parse_strips_the_leading_number_from_the_heading_to_produce_Nome()
    {
        var result = HistoricoSeedParser.Parse(Markdown);

        result.Should().NotContain(h => h.Nome.StartsWith("1.") || h.Nome.StartsWith("19."));
    }

    [Fact]
    public void Parse_resolves_a_multi_word_accented_Pericia_label()
    {
        var result = HistoricoSeedParser.Parse(Markdown);

        var afinidadeAnimal = result.Should().ContainSingle(h => h.Nome == "Afinidade Animal").Subject;
        afinidadeAnimal.PericiaMaisSeis.Should().Be(Pericia.EmpatiaComAnimais);
        afinidadeAnimal.PericiaMaisTres.Should().Be(Pericia.Sobrevivencia);
    }

    [Fact]
    public void Parse_ignores_the_intro_prose_before_the_first_heading()
    {
        var result = HistoricoSeedParser.Parse(Markdown);

        result.Should().HaveCount(2);
    }
}
