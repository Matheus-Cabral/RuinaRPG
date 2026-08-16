using FluentAssertions;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class GraduacaoEfeitoParserTests
{
    // Real excerpt from Docs/Sistema RPG/GRAUS & CÍRCULOS.md — top intro table skipped
    // (that's CirculoGrauPorEap's scaling table, not a named effect), then 2 effects from
    // 1º Grau (one with a prerequisite) and 1 from 2º Grau, to prove multi-grade extraction.
    private const string Markdown = """
        Custo: 1,25 de arcana por PI (arredondado para cima)

        # 1º GRAU / CÍRCULO I

        ## Efeitos Básicos

        Dano: 2 Pontos por Dado.

        ## Aumentar Armadura

        Gasto: 2 PI por Ponto de Redução.

        Max. 5 de Redução por Grau/Círculo.

        Concede ao personagem Redução Física.

        Obrigatória a compra de Duração.

        ## Contrato Mágico

        Gasto: 3 PI

        Essencial para os bruxos que expressam sua magia como Invocação.

        ## Aumentar Alcance

        Gasto: 1 PI por metro de alcance.

        É obrigatória a compra de Alcance.

        # 2º GRAU / CÍRCULO II

        ## Aceleração

        Gasto: 3 PI

        O Alvo acometido por Aceleração recebe metade de sua movimentação como movimentação adicional.
        """;

    [Fact]
    public void Parse_extracts_named_effects_with_their_introducing_grade()
    {
        var result = GraduacaoEfeitoParser.Parse(Markdown);

        result.Should().Contain(e => e.Nome == "Aumentar Armadura" && e.Grau == 1);
        result.Should().Contain(e => e.Nome == "Contrato Mágico" && e.Grau == 1);
        result.Should().Contain(e => e.Nome == "Aumentar Alcance" && e.Grau == 1);
        result.Should().Contain(e => e.Nome == "Aceleração" && e.Grau == 2);
    }

    [Fact]
    public void Parse_excludes_the_Efeitos_Basicos_heading_itself()
    {
        // "Efeitos Básicos" is a sub-section wrapper (Dano/Alcance/Duração scaling), not a named,
        // purchasable Special Effect — it must not appear as its own GraduacaoEfeito entry.
        var result = GraduacaoEfeitoParser.Parse(Markdown);

        result.Should().NotContain(e => e.Nome == "Efeitos Básicos");
    }

    [Fact]
    public void Parse_extracts_Gasto_text_and_detects_a_prerequisite()
    {
        var result = GraduacaoEfeitoParser.Parse(Markdown);

        // Test "Obrigatória" form (without leading "É")
        var aumentarArmadura = result.Single(e => e.Nome == "Aumentar Armadura");
        aumentarArmadura.Gasto.Should().Be("2 PI por Ponto de Redução.");
        aumentarArmadura.TemPreRequisito.Should().BeTrue();
        aumentarArmadura.PreRequisitoDescricao.Should().Contain("Duração");

        // Test "É obrigatória" form (with leading "É")
        var aumentarAlcance = result.Single(e => e.Nome == "Aumentar Alcance");
        aumentarAlcance.Gasto.Should().Be("1 PI por metro de alcance.");
        aumentarAlcance.TemPreRequisito.Should().BeTrue();
        aumentarAlcance.PreRequisitoDescricao.Should().Contain("Alcance");

        var contratoMagico = result.Single(e => e.Nome == "Contrato Mágico");
        contratoMagico.Gasto.Should().Be("3 PI");
        contratoMagico.TemPreRequisito.Should().BeFalse();
    }
}
