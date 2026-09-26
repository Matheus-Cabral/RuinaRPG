using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class CaminhoSubElementoRulesTests
{
    [Theory]
    [InlineData(AfinidadeElemental.Alma, true)]
    [InlineData(AfinidadeElemental.Vida, true)]
    [InlineData(AfinidadeElemental.Fogo, false)]
    [InlineData(AfinidadeElemental.Curar, false)]
    public void EhCaminho_is_true_only_for_Alma_and_Vida_as_Afinidade(AfinidadeElemental afinidade, bool esperado)
    {
        CaminhoSubElementoRules.EhCaminho(afinidade).Should().Be(esperado);
    }
}
