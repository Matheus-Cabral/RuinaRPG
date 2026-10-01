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

    [Fact]
    public void Summing_extracted_values_reproduces_todays_calculators_for_every_level()
    {
        var markdown = File.ReadAllText(Path.Combine(RepoRoot(), "Docs", "Sistema RPG", "Tabela de Níveis.md"));
        var niveis = NivelBonusParser.Parse(markdown);

        for (var nivel = 1; nivel <= 50; nivel++)
        {
            var ate = niveis.Where(n => n.Nivel <= nivel).Select(n => NivelBonusExtractor.Extrair(n.BonusText)).ToList();
            int Soma(string chave) => ate.Sum(e => e.Valores.GetValueOrDefault(chave));

            Soma(ChavesDeNivel.PontosDeAtributo).Should().Be(AttributePointBudgetCalculator.Compute(nivel, niveis), $"nível {nivel}");
            Soma(ChavesDeNivel.PontosDePericia).Should().Be(SkillPointBudgetCalculator.Compute(nivel, niveis), $"nível {nivel}");
            (5 + Soma(ChavesDeNivel.EspacosDeCaracteristica)).Should().Be(TraitPointBudgetCalculator.Compute(nivel, niveis), $"nível {nivel}");
            Soma(ChavesDeNivel.PontosDeIgnicao).Should().Be(PontosDeIgnicaoCalculator.ComputeTotal(nivel, 0, niveis), $"nível {nivel}");
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
