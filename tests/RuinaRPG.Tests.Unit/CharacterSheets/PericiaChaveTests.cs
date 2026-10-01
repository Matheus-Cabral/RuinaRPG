using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class PericiaChaveTests
{
    [Theory]
    [InlineData("Navegação Aérea", "NavegacaoAerea")]
    [InlineData("  empatia c/ animais ", "EmpatiaCAnimais")]
    [InlineData("Ofício-2", "Oficio2")]
    public void Gerar_builds_an_accent_free_PascalCase_key(string nome, string esperado)
    {
        PericiaChave.Gerar(nome, Array.Empty<string>()).Should().Be(esperado);
    }

    [Fact]
    public void Gerar_appends_a_number_on_collision_case_insensitively()
    {
        PericiaChave.Gerar("Atletismo", new[] { "Atletismo", "atletismo2" }).Should().Be("Atletismo3");
    }

    [Fact]
    public void Gerar_of_a_name_without_letters_or_digits_falls_back_to_Pericia()
    {
        PericiaChave.Gerar("!!!", Array.Empty<string>()).Should().Be("Pericia");
    }
}
