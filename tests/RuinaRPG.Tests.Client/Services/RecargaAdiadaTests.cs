using FluentAssertions;
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class RecargaAdiadaTests
{
    private int _recargas;
    private bool _emEdicao;
    private bool _salvando;

    private RecargaAdiada Criar() => new(() => { _recargas++; return Task.CompletedTask; }, () => Task.FromResult(_emEdicao), () => _salvando, TimeSpan.FromMilliseconds(5));

    [Fact]
    public async Task Reloads_right_away_when_no_field_is_being_edited()
    {
        var recarga = Criar();

        await recarga.SolicitarAsync();

        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task Holds_the_reload_while_a_field_has_focus_and_runs_it_once_when_focus_leaves()
    {
        var recarga = Criar();
        _emEdicao = true;

        await recarga.SolicitarAsync();
        await recarga.SolicitarAsync();
        _recargas.Should().Be(0);

        _emEdicao = false;
        await recarga.FocoSaiuAsync();

        _recargas.Should().Be(1);
        await recarga.FocoSaiuAsync();
        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task Waits_for_a_pending_autosave_before_reloading_after_focus_leaves()
    {
        var recarga = Criar();
        _emEdicao = true;
        await recarga.SolicitarAsync();
        _emEdicao = false;
        _salvando = true;

        var espera = recarga.FocoSaiuAsync();
        await Task.Delay(50);
        _recargas.Should().Be(0);

        _salvando = false;
        await espera;
        _recargas.Should().Be(1);
    }
}
