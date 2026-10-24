using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>O estado da página de ficha que as abas compartilhadas recebem em cascata (núcleo do E, Task 6).</summary>
public class FichaEstadoTests
{
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("https://localhost/api/") };

    [Fact]
    public void Keeps_the_kind_the_sheet_and_the_client_it_was_built_with()
    {
        var estado = new FichaEstado(Http, TipoDeFichaCliente.Npc, "s1");

        estado.Tipo.Should().BeSameAs(TipoDeFichaCliente.Npc);
        estado.SheetId.Should().Be("s1");
        estado.Http.Should().BeSameAs(Http);
    }

    [Fact]
    public void Starts_empty()
    {
        var estado = new FichaEstado(Http, TipoDeFichaCliente.Personagem, "s1");

        estado.Erro.Should().BeNull();
        estado.PenalidadesVersao.Should().Be(0);
        estado.CondicoesVersao.Should().Be(0);
        estado.IsGmCaller.Should().BeFalse();
        estado.CampaignId.Should().BeNull();
        estado.MinhasImagens.Should().BeEmpty();
    }

    [Fact]
    public void Notificar_alteracao_raises_Alterado()
    {
        var estado = new FichaEstado(Http, TipoDeFichaCliente.Criatura, "s1");
        var vezes = 0;
        estado.Alterado += () => vezes++;

        estado.NotificarAlteracao();

        vezes.Should().Be(1);
    }

    [Fact]
    public void Notificar_alteracao_without_listeners_does_nothing()
    {
        var estado = new FichaEstado(Http, TipoDeFichaCliente.Criatura, "s1");

        var agir = () => estado.NotificarAlteracao();

        agir.Should().NotThrow();
    }

    [Fact]
    public void A_listener_that_left_is_not_called()
    {
        var estado = new FichaEstado(Http, TipoDeFichaCliente.Personagem, "s1");
        var vezes = 0;
        void Ouvir() => vezes++;
        estado.Alterado += Ouvir;
        estado.Alterado -= Ouvir;

        estado.NotificarAlteracao();

        vezes.Should().Be(0);
    }
}
