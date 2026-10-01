using FluentAssertions;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.Rules;

public class LimitesDeNivelTests
{
    [Theory]
    [InlineData(2, 3, null, true)]
    [InlineData(2, 3, 3, true)]
    [InlineData(2, 4, 3, false)]
    [InlineData(5, 4, 3, true)]   // lowering an over-cap value is allowed
    [InlineData(5, 5, 3, true)]   // unchanged over-cap value is allowed
    [InlineData(5, 6, 3, false)]
    public void Gasto(int atual, int novo, int? limite, bool permitido) =>
        (LimitesDeNivel.Gasto("Força", atual, novo, limite, 7) is null).Should().Be(permitido);

    [Fact]
    public void Gasto_message_names_the_target_cap_and_level() =>
        LimitesDeNivel.Gasto("Atletismo", 0, 9, 8, 5).Should().Be("Atletismo não pode passar de 8 pontos no nível 5.");

    [Theory]
    [InlineData(0, null, true)]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(0, 0, false)]
    public void Passivas(int jaNaFicha, int? limite, bool permitido) =>
        (LimitesDeNivel.Passivas(CategoriaDePassiva.Livre, jaNaFicha, limite, 10) is null).Should().Be(permitido);

    [Theory]
    [InlineData(CategoriaDePassiva.Livre, "Livre(s)")]
    [InlineData(CategoriaDePassiva.Vocacional, "Vocacional(is)")]
    [InlineData(CategoriaDePassiva.DeClasse, "De Classe")]
    public void Passivas_message_uses_the_category_label(CategoriaDePassiva categoria, string rotulo) =>
        LimitesDeNivel.Passivas(categoria, 2, 2, 4).Should().Be($"O nível 4 permite no máximo 2 Passiva(s) {rotulo}.");
}
