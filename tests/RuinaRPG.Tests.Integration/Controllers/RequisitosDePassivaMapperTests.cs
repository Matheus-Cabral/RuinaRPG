using FluentAssertions;
using RuinaRPG.Api.Controllers;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RequisitosDePassivaMapperTests
{
    [Fact]
    public void ToDto_omits_requisites_on_removed_or_unknown_pericias_instead_of_throwing()
    {
        var porId = new Dictionary<int, PericiaDefinicao>
        {
            [1] = new() { Id = 1, Chave = "Viva", Nome = "Viva" },
            [2] = new() { Id = 2, Chave = "Morta", Nome = "Morta", IsDeleted = true },
        };
        var requisitos = new RequisitosDePassiva
        {
            Pericias = [new RequisitoDePericia(1, 3), new RequisitoDePericia(2, 4), new RequisitoDePericia(99, 5)],
        };

        var dto = RequisitosDePassivaMapper.ToDto(requisitos, porId);

        dto!.Pericias.Select(p => p.Alvo).Should().Equal("Viva");
    }
}
