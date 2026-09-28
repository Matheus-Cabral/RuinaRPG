using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class RequisitosFormModelTests
{
    [Fact]
    public void Round_trips_through_the_dto()
    {
        var dto = new RequisitosDePassivaDto(Nivel: 3, Vocacao: "Bruxo", Classe: "Ocultista", CoracaoDeMana: true,
            Atributos: [new("Forca", 4)], SubAtributos: [new("Iniciativa", 2)], Pericias: [new("Atletismo", 1)]);

        RequisitosFormModel.FromDto(dto).ToDto().Should().BeEquivalentTo(dto with { Linhagem = null });
    }

    [Fact]
    public void Blank_strings_and_unchecked_coracao_become_null()
    {
        var model = new RequisitosFormModel { Vocacao = "", Classe = "  ", CoracaoDeMana = false };
        var dto = model.ToDto();
        dto.Vocacao.Should().BeNull();
        dto.Classe.Should().BeNull();
        dto.CoracaoDeMana.Should().BeNull();
    }
}
