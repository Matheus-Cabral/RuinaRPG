using FluentAssertions;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class GrausECirculosMarkdownRealDocTests
{
    // Every Efeito.Nome seeded by EfeitoSeeder.cs (src/RuinaRPG.Infrastructure/Rules/EfeitoSeeder.cs),
    // hardcoded here so this test breaks the moment the two lists drift apart.
    private static readonly string[] NomesDoSeeder =
    [
        "Dano", "Alcance", "Duração",
        "Aumentar Armadura", "Aumentar Atributo", "Contrato Mágico", "Cura", "Deslocamento", "Miragem", "Regeneração",
        "Aceleração", "Amedrontar", "Diminuir Atributo", "Encantamento Elemental", "Enraizar", "Envenenar",
        "Libra (Vitalidade)", "Libra (Arcana)",
        "Área", "Armadura Arcana", "Ato Múltiplo", "Aumentar Max. Vitalidade", "Congelar", "Dreno de Vitalidade",
        "Reflexão", "Proteção", "Provocar", "Marionete Arcana",
        "Abençoar", "Aumentar Max. Arcana", "Confusão", "Dreno de Arcana", "Decair", "Detrito", "Fadiga", "Imagem Ilusória",
        "Feixe", "Ações por Turno", "Cegueira", "Defesa Verdadeira", "Deflexão", "Deflexão Mágica", "Encantamento Pessoal", "Encantar",
        "Amplificação Arcana", "Fúria", "Julgamento",
        "Absorção", "Crítico Aprimorado",
        "Atordoamento",
        "Imunidade",
    ];

    [Fact]
    public void Real_Graus_e_Circulos_document_contains_every_seeded_Efeito()
    {
        var md = RulesDataProvider.ReadResource("GRAUS e CIRCULOS.md");

        foreach (var nome in NomesDoSeeder)
            GrausECirculosMarkdown.Contem(md, nome).Should().BeTrue($"'{nome}' deveria ter um bloco (ou compartilhar um) no Livro de Regras");
    }
}
