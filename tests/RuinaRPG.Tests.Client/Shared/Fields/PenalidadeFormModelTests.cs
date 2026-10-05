using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.Items;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class PenalidadeFormModelTests
{
    [Fact]
    public void Round_trips_a_dto()
    {
        var dto = new PenalidadeDeEquipamentoDto([new("Forca", 2)], [new("Movimentacao", 1)], [new("reflexos", 4)], "Lenta");

        var volta = PenalidadeFormModel.FromDto(dto).ToDto();

        volta!.Atributos.Should().Equal(new PenalidadeLinhaDto("Forca", 2));
        volta.SubAtributos.Should().Equal(new PenalidadeLinhaDto("Movimentacao", 1));
        volta.Pericias.Should().Equal(new PenalidadeLinhaDto("reflexos", 4));
        volta.Texto.Should().Be("Lenta");
    }

    [Fact]
    public void An_empty_model_and_one_with_only_blank_lines_or_blank_text_give_null()
    {
        new PenalidadeFormModel().ToDto().Should().BeNull();
        new PenalidadeFormModel { Atributos = [new() { Alvo = "" }], Texto = "   " }.ToDto().Should().BeNull();
        PenalidadeFormModel.FromDto(null).ToDto().Should().BeNull();
    }

    [Fact]
    public void Lines_without_a_target_are_dropped_and_the_text_is_trimmed()
    {
        var model = new PenalidadeFormModel { Atributos = [new() { Alvo = "", Valor = 3 }, new() { Alvo = "Vigor", Valor = 1 }], Texto = "  x  " };

        var dto = model.ToDto()!;

        dto.Atributos.Should().Equal(new PenalidadeLinhaDto("Vigor", 1));
        dto.Texto.Should().Be("x");
    }
}
