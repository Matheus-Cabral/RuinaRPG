using FluentAssertions;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class PericiaDisplayTests
{
    [Theory]
    [InlineData("Acrobacia", "Acrobacia")]
    [InlineData("Alquimia", "Alquimia")]
    [InlineData("Arcano", "Arcano")]
    [InlineData("Armadilhas", "Armadilhas")]
    [InlineData("ArmasBrancas", "Armas Brancas")]
    [InlineData("ArtefatosMagicos", "Artefatos Mágicos")]
    [InlineData("Artistico", "Artístico")]
    [InlineData("Atletismo", "Atletismo")]
    [InlineData("Avaliacao", "Avaliação")]
    [InlineData("Biblioteca", "Biblioteca")]
    [InlineData("Brigar", "Brigar")]
    [InlineData("Conducao", "Condução")]
    [InlineData("Conhecimentos", "Conhecimentos")]
    [InlineData("Crime", "Crime")]
    [InlineData("EmpatiaComAnimais", "Empatia c/ Animais")]
    [InlineData("Enganacao", "Enganação")]
    [InlineData("ForcaDeVontade", "Força de Vontade")]
    [InlineData("Fortitude", "Fortitude")]
    [InlineData("Furtividade", "Furtividade")]
    [InlineData("Herborismo", "Herborismo")]
    [InlineData("Intimidacao", "Intimidação")]
    [InlineData("Intuicao", "Intuição")]
    [InlineData("Investigacao", "Investigação")]
    [InlineData("Labia", "Lábia")]
    [InlineData("Lideranca", "Liderança")]
    [InlineData("Linguistica", "Linguística")]
    [InlineData("Medicina", "Medicina")]
    [InlineData("Navegacao", "Navegação")]
    [InlineData("Ocultismo", "Ocultismo")]
    [InlineData("Oficio", "Ofício")]
    [InlineData("Percepcao", "Percepção")]
    [InlineData("Pontaria", "Pontaria")]
    [InlineData("Prontidao", "Prontidão")]
    [InlineData("Reflexos", "Reflexos")]
    [InlineData("Religiao", "Religião")]
    [InlineData("Saquear", "Saquear")]
    [InlineData("Seducao", "Sedução")]
    [InlineData("SensoComum", "Senso Comum")]
    [InlineData("Sobrevivencia", "Sobrevivência")]
    public void Label_returns_the_proper_Portuguese_display_name_for_every_Pericia(string raw, string expected)
    {
        PericiaDisplay.Label(raw).Should().Be(expected);
    }

    [Fact]
    public void Label_falls_back_to_the_raw_value_for_an_unmapped_string()
    {
        // Defensive: never blank out or throw on a value this map hasn't caught up with yet.
        PericiaDisplay.Label("AlgoNovo").Should().Be("AlgoNovo");
    }
}
