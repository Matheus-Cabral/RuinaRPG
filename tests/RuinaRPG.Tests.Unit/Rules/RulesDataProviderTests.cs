using FluentAssertions;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class RulesDataProviderTests
{
    [Fact]
    public void All_seven_collections_are_populated_from_the_real_embedded_docs()
    {
        IRulesDataProvider provider = new RulesDataProvider();

        provider.Niveis.Should().HaveCount(50);
        provider.Vocacoes.Should().HaveCountGreaterThan(0).And.Contain(v => v.Vocacao == "Campeão" && v.Nivel == 1);
        provider.Arquetipos.Should().Contain(a => a.Arquetipo == "Fisico" && a.Nivel == 1);
        provider.CirculoGrauPorEap.Should().HaveCount(10); // Círculo/Grau 0..9
        provider.XpPorNivel.Should().HaveCount(50);
        provider.EapPorNivel.Should().HaveCount(50);
        provider.EapPorNivel.Should().Contain(e => e.Nivel == 1 && e.ValorAbsoluto == 0);
        provider.Efeitos.Should().Contain(e => e.Nome == "Aumentar Armadura");
        provider.Regras.Should().Contain(r => r.Titulo.Contains("Adrenalina"));
    }

    [Fact]
    public void Repeated_access_reuses_the_cached_parse_result()
    {
        IRulesDataProvider provider = new RulesDataProvider();

        var firstCall = provider.Niveis;
        var secondCall = provider.Niveis;

        secondCall.Should().BeSameAs(firstCall);
    }
}
