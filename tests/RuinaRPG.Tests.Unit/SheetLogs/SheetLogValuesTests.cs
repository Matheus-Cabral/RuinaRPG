using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Runes;
using RuinaRPG.Domain.SpellsAndAbilities;
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

    [Theory]
    [InlineData(Atributo.Forca, "Força")]
    [InlineData(Atributo.Astucia, "Astúcia")]
    [InlineData(Atributo.Influencia, "Influência")]
    [InlineData(Atributo.Vigor, "Vigor")]
    public void An_attribute_renders_its_Portuguese_name(Atributo atributo, string esperado) =>
        SheetLogValues.Render(atributo).Should().Be(esperado);

    [Fact]
    public void Enums_shown_on_the_sheet_with_accents_or_spaces_render_as_the_sheet_shows_them()
    {
        SheetLogValues.Render(Vocacao.Campeao).Should().Be("Campeão");
        SheetLogValues.Render(Vocacao.Cacador).Should().Be("Caçador");
        SheetLogValues.Render(Variante.PhylacTai).Should().Be("Phylac'tai (Sol)");
        SheetLogValues.Render(Variante.Alora).Should().Be("Alóra (Lua)");
        SheetLogValues.Render(Elemento.Agua).Should().Be("Água");
        SheetLogValues.Render(EssenciaBasica.Agua).Should().Be("Água");
        SheetLogValues.Render(SubElemento.Invocacao).Should().Be("Invocação");
        SheetLogValues.Render(Arquetipo.Fisico).Should().Be("Físico");
        SheetLogValues.Render(TipoDeRuna.Negra).Should().Be("Runa Negra");
        SheetLogValues.Render(CategoriaDePassiva.DeClasse).Should().Be("Passiva de Classe");
    }

    [Fact]
    public void A_nullable_enum_with_a_value_renders_like_the_enum()
    {
        Atributo? escolhido = Atributo.Forca;
        SheetLogValues.Render(escolhido).Should().Be("Força");
    }

    [Fact]
    public void An_enum_member_without_an_entry_renders_its_member_name()
    {
        SheetLogValues.Render(Linhagem.Econos).Should().Be("Econos");
        SheetLogValues.Render(Rank.S).Should().Be("S");
    }
}
