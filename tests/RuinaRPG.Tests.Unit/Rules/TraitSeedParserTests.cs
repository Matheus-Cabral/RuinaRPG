using FluentAssertions;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class TraitSeedParserTests
{
    // Real excerpt from Docs/Sistema RPG/Características.md: one single-tier Positiva, one
    // multi-tier Positiva, one single-tier Negativa (already-negative cost in the source), and
    // one Negativa whose cost line has a trailing qualifier word OTHER than "por X" ("cada"
    // instead of "por sentido") — a real phrasing this parser must not silently drop.
    private const string Markdown = """
        # Positivas

        ### Alfabetizado

        1 ponto: Saber ler e escrever é um conhecimento destinado a um número um tanto limitado de pessoas.

        ### Aparência Inofensiva

        2 ponto: você não aparenta ser perigoso.

        3 pontos: Considere que o personagem automaticamente você recebe vantagem em testes de Iniciativa.

        # Negativas

        ### Alergia

        -1 ponto: o Personagem é alérgico a alguma coisa.

        ### Código de Honra

        -1 ponto cada: o personagem segue algum rígido código de conduta e jamais poderá desobedecê-lo.
        """;

    [Fact]
    public void Parse_extracts_one_row_per_single_tier_trait()
    {
        var result = TraitSeedParser.Parse(Markdown);

        result.Should().ContainSingle(t => t.Nome == "Alfabetizado")
            .Which.Should().BeEquivalentTo(new { Custo = 1, Polaridade = "Positiva" });
    }

    [Fact]
    public void Parse_extracts_one_row_per_cost_tier_for_multi_tier_traits_with_a_disambiguating_name()
    {
        var result = TraitSeedParser.Parse(Markdown);

        var tiers = result.Where(t => t.Nome.StartsWith("Aparência Inofensiva")).ToList();
        tiers.Should().HaveCount(2);
        tiers.Should().Contain(t => t.Nome == "Aparência Inofensiva (2 pontos)" && t.Custo == 2);
        tiers.Should().Contain(t => t.Nome == "Aparência Inofensiva (3 pontos)" && t.Custo == 3);
    }

    [Fact]
    public void Parse_reads_already_negative_costs_for_Negativas_as_is()
    {
        var result = TraitSeedParser.Parse(Markdown);

        result.Should().ContainSingle(t => t.Nome == "Alergia")
            .Which.Should().BeEquivalentTo(new { Custo = -1, Polaridade = "Negativa" });
    }

    [Fact]
    public void Parse_does_not_drop_a_trait_whose_cost_line_has_a_non_por_qualifier_word()
    {
        // Regression guard: "ponto cada:" has a qualifier word ("cada") that is not the "por X"
        // shape seen elsewhere (e.g. "2 pontos por sentido:") — an earlier version of this parser's
        // regex only tolerated "por X" and silently dropped entries like this one.
        var result = TraitSeedParser.Parse(Markdown);

        result.Should().ContainSingle(t => t.Nome == "Código de Honra")
            .Which.Should().BeEquivalentTo(new { Custo = -1, Polaridade = "Negativa" });
    }

    [Fact]
    public void Parse_extracts_exactly_69_traits_from_the_real_source_document()
    {
        // 30 Positivas (24 traits, 5 of them multi-tier: Aparência Inofensiva x2, Arma ou Artefato
        // Mágico x2, Dívida de Gratidão x3, Imunidade de Venenos x2, Sono Leve x2) +
        // 39 Negativas (34 traits, 4 of them multi-tier: Deficiente Físico x3, Fetiche Material x2,
        // Fobia x2, Mania de Perseguição x2) = 69. Counted by hand against the doc when this task was
        // planned — a real change to Características.md (a trait added/removed/re-tiered) is expected
        // to move this number, and this test's failure is exactly the signal that should happen.
        var result = TraitSeedParser.Parse(RulesDataProvider.ReadResource("Caracteristicas.md"));

        result.Should().HaveCount(69);
        result.Should().Contain(t => t.Nome == "Imunidade de Venenos (2 pontos)"); // proves Step 1's doc fix took effect
        result.Should().Contain(t => t.Nome == "Código de Honra"); // proves the "ponto cada:" qualifier is handled
    }
}
