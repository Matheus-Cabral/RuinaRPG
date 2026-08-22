using FluentAssertions;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class RegraEntryParserTests
{
    // Real excerpt from Docs/Sistema RPG/Ruína RPG - Sistema Básico.md.
    private const string Markdown = """
        ## 1. Atributos

        Os atributos definem as capacidades inatas do seu personagem. No início, o jogador distribui 9 pontos entre os oito atributos abaixo.

        - Instinto: Percepção, intuição e sentidos.

        ## 3. Pontos de Adrenalina (PA)

        Os Pontos de Adrenalina representam o esforço usado em cena.

        ### Escala de Dados

        Não há rolagem de dano separada.
        """;

    [Fact]
    public void Parse_flattens_H2_sections_into_searchable_entries()
    {
        var result = RegraEntryParser.Parse(Markdown);

        result.Should().Contain(r => r.Titulo == "1. Atributos" && r.Conteudo.Contains("Instinto"));
        result.Should().Contain(r => r.Titulo == "3. Pontos de Adrenalina (PA)" && r.Conteudo.Contains("esforço usado em cena"));
    }

    [Fact]
    public void Parse_includes_nested_H3_content_inside_its_parent_H2_entry()
    {
        var result = RegraEntryParser.Parse(Markdown);

        result.Single(r => r.Titulo == "3. Pontos de Adrenalina (PA)").Conteudo.Should().Contain("Escala de Dados");
    }
}
