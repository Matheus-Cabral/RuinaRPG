using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Domain.Rules.ReferenceData;
using Xunit;

namespace RuinaRPG.Tests.Unit.Rules;

public class NivelBonusExtractorTests
{
    [Fact]
    public void Extracts_the_numbers_and_keeps_the_rest_of_level_1()
    {
        var e = NivelBonusExtractor.Extrair("+9 Pontos de Atributo  <br>+Status de Vida Aprimorado  <br>+Status de Foco Aprimorado  <br>+10 Pontos de Ignição  <br>+4 Pontos de Perícia  <br>+1 Espaço de Maestria  <br>+1 Ponto de Maestria");

        e.Valores.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            [ChavesDeNivel.PontosDeAtributo] = 9, [ChavesDeNivel.PontosDeIgnicao] = 10, [ChavesDeNivel.PontosDePericia] = 4,
            [ChavesDeNivel.EspacosDeMaestria] = 1, [ChavesDeNivel.PontosDeMaestria] = 1,
        });
        e.Restante.Should().Equal("+Status de Vida Aprimorado", "+Status de Foco Aprimorado");
    }

    // Regression snapshot: the per-level totals the regex calculators (deleted in the Auditoria da
    // Tabela de Níveis) produced over Docs/Sistema RPG/Tabela de Níveis.md, captured by running
    // them one last time before the deletion. Index 0 = Nível 1 ... index 49 = Nível 50.
    public static readonly int[] PontosDeAtributoPorNivel =
        [9, 10, 10, 10, 10, 11, 11, 11, 11, 13, 13, 13, 13, 14, 16, 16, 16, 17, 17, 19, 19, 19, 19, 20, 23, 23, 23, 24, 24, 28, 28, 28, 28, 29, 33, 33, 33, 34, 34, 40, 40, 40, 40, 42, 47, 47, 47, 48, 48, 56];
    public static readonly int[] PontosDePericiaPorNivel =
        [4, 4, 4, 4, 4, 4, 4, 4, 4, 6, 6, 7, 7, 7, 9, 9, 9, 9, 9, 11, 11, 13, 13, 13, 17, 17, 17, 17, 17, 19, 19, 21, 21, 21, 27, 27, 27, 27, 27, 27, 27, 31, 31, 31, 39, 39, 39, 39, 39, 43];
    /// <summary>Includes the flat creation base of 5.</summary>
    public static readonly int[] EspacosDeCaracteristicaPorNivel =
        [5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11];
    /// <summary>With bonusManual 0.</summary>
    public static readonly int[] PontosDeIgnicaoPorNivel =
        [10, 14, 18, 22, 26, 30, 34, 38, 42, 50, 58, 66, 74, 82, 94, 102, 110, 118, 126, 138, 150, 162, 174, 186, 202, 214, 226, 238, 250, 266, 282, 298, 314, 330, 350, 366, 382, 398, 414, 434, 454, 474, 494, 514, 538, 558, 578, 598, 618, 648];

    [Fact]
    public void Summing_extracted_values_reproduces_todays_calculators_for_every_level()
    {
        var markdown = File.ReadAllText(Path.Combine(RepoRoot(), "Docs", "Sistema RPG", "Tabela de Níveis.md"));
        var niveis = NivelBonusParser.Parse(markdown);

        for (var nivel = 1; nivel <= 50; nivel++)
        {
            var ate = niveis.Where(n => n.Nivel <= nivel).Select(n => NivelBonusExtractor.Extrair(n.BonusText)).ToList();
            int Soma(string chave) => ate.Sum(e => e.Valores.GetValueOrDefault(chave));

            Soma(ChavesDeNivel.PontosDeAtributo).Should().Be(PontosDeAtributoPorNivel[nivel - 1], $"nível {nivel}");
            Soma(ChavesDeNivel.PontosDePericia).Should().Be(PontosDePericiaPorNivel[nivel - 1], $"nível {nivel}");
            (5 + Soma(ChavesDeNivel.EspacosDeCaracteristica)).Should().Be(EspacosDeCaracteristicaPorNivel[nivel - 1], $"nível {nivel}");
            Soma(ChavesDeNivel.PontosDeIgnicao).Should().Be(PontosDeIgnicaoPorNivel[nivel - 1], $"nível {nivel}");
        }
    }

    [Fact]
    public void The_calculators_over_the_extracted_table_reproduce_the_snapshot_for_every_level()
    {
        var markdown = File.ReadAllText(Path.Combine(RepoRoot(), "Docs", "Sistema RPG", "Tabela de Níveis.md"));
        var tabela = TabelaDeNiveisDeTeste.Criar(NivelBonusParser.Parse(markdown)
            .Select(n => TabelaDeNiveisDeTeste.Nivel(n.Nivel, NivelBonusExtractor.Extrair(n.BonusText).Valores.Select(v => (v.Key, v.Value)).ToArray()))
            .ToArray());

        tabela.UltimoNivel.Should().Be(50);
        for (var nivel = 1; nivel <= 50; nivel++)
        {
            AttributePointBudgetCalculator.Compute(nivel, tabela).Should().Be(PontosDeAtributoPorNivel[nivel - 1], $"nível {nivel}");
            SkillPointBudgetCalculator.Compute(nivel, tabela).Should().Be(PontosDePericiaPorNivel[nivel - 1], $"nível {nivel}");
            TraitPointBudgetCalculator.Compute(nivel, tabela).Should().Be(EspacosDeCaracteristicaPorNivel[nivel - 1], $"nível {nivel}");
            PontosDeIgnicaoCalculator.ComputeTotal(nivel, 0, tabela).Should().Be(PontosDeIgnicaoPorNivel[nivel - 1], $"nível {nivel}");
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RuinaRPG.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
