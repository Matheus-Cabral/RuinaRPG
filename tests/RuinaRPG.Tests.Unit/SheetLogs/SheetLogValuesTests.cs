using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Runes;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Unit.SheetLogs;

public class SheetLogValuesTests
{
    [Fact]
    public void Booleans_render_as_Sim_and_Nao()
    {
        SheetLogValues.Render(true).Should().Be("Sim");
        SheetLogValues.Render(false).Should().Be("Não");
    }

    [Fact]
    public void Null_renders_as_a_dash() => SheetLogValues.Render(null).Should().Be("—");

    [Fact]
    public void Integers_render_plainly() => SheetLogValues.Render(12).Should().Be("12");

    [Fact]
    public void Decimals_render_in_ptBR_without_trailing_zeros()
    {
        SheetLogValues.Render(1.50m).Should().Be("1,5");
        SheetLogValues.Render(2.00m).Should().Be("2");
    }

    [Fact]
    public void Enums_with_a_screen_label_render_it() =>
        SheetLogValues.Render(DisciplinaDeRuna.Adicao).Should().Be("Adição");

    [Fact]
    public void Other_enums_render_their_name() =>
        SheetLogValues.Render(Cobertura.Parcial).Should().Be("Parcial");

    [Fact]
    public void Strings_render_as_they_are() => SheetLogValues.Render("Aldric").Should().Be("Aldric");
}
