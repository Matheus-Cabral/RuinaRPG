using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A parte Antecedentes do <see cref="FichaEstado"/> (E, Task 9): o endereço da História de cada tipo, a marca de que a
/// História já veio da ficha (o editor só lê o HTML ao ser criado) e o erro do editor indo para o alerta da página.
/// Estrela, Histórico e o texto da História continuam no formulário auto-salvo da página.
/// </summary>
public class FichaEstadoAntecedentesTests
{
    private static FichaEstado Estado(TipoDeFichaCliente tipo) =>
        new(FakeHttpMessageHandler.CreateClient(_ => throw new InvalidOperationException("A parte Antecedentes não faz requisições.")), tipo, "s1");

    [Theory]
    [InlineData("Personagem", "character-sheets/s1/historia")]
    [InlineData("NPC", "npc-sheets/s1/historia")]
    public void The_historia_address_follows_the_kind(string tipo, string esperado)
    {
        var estado = Estado(tipo == "Personagem" ? TipoDeFichaCliente.Personagem : TipoDeFichaCliente.Npc);

        estado.UrlDaHistoria.Should().Be(esperado);
    }

    [Fact]
    public void The_historia_is_not_loaded_until_the_page_says_so()
    {
        var estado = Estado(TipoDeFichaCliente.Personagem);

        estado.HistoriaCarregada.Should().BeFalse();
        estado.HistoriaCarregada = true;
        estado.HistoriaCarregada.Should().BeTrue();
    }

    [Fact]
    public void An_editor_error_goes_to_the_page_alert_and_notifies_the_page()
    {
        var estado = Estado(TipoDeFichaCliente.Npc);
        var avisos = 0;
        estado.Alterado += () => avisos++;

        estado.MostrarErro("Não foi possível salvar a História.");

        estado.Erro.Should().Be("Não foi possível salvar a História.");
        avisos.Should().Be(1);
    }
}
