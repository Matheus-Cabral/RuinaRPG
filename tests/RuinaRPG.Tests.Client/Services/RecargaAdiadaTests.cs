using FluentAssertions;
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class RecargaAdiadaTests
{
    private int _recargas;
    private bool _emEdicao;
    private bool _salvando;
    private bool _jsFalha;
    private Exception? _falhaDaRecarga;
    private readonly List<Exception> _falhas = new();

    private RecargaAdiada Criar(TimeSpan? limite = null) => new(
        () => { _recargas++; return _falhaDaRecarga is null ? Task.CompletedTask : Task.FromException(_falhaDaRecarga); },
        () => _jsFalha ? Task.FromException<bool>(new InvalidOperationException("sem ruinaFoco")) : Task.FromResult(_emEdicao),
        () => _salvando, TimeSpan.FromMilliseconds(10), limite ?? TimeSpan.FromSeconds(30), _falhas.Add);

    private static async Task AteAsync(Func<bool> condicao)
    {
        for (var i = 0; i < 200 && !condicao(); i++)
            await Task.Delay(10);
    }

    [Fact]
    public async Task Reloads_right_away_when_nothing_is_being_edited_or_saved()
    {
        await Criar().SolicitarAsync();

        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task Holds_the_reload_while_a_field_has_focus_and_runs_it_once_when_editing_ends()
    {
        using var recarga = Criar();
        _emEdicao = true;

        await recarga.SolicitarAsync();
        await recarga.SolicitarAsync();
        await Task.Delay(50);
        _recargas.Should().Be(0);

        _emEdicao = false;
        recarga.FocoSaiu();
        await AteAsync(() => _recargas > 0);
        await Task.Delay(50);

        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task A_pending_autosave_defers_the_reload_even_with_no_field_focused_and_it_runs_after_the_save()
    {
        using var recarga = Criar();
        _emEdicao = false;
        _salvando = true;

        await recarga.SolicitarAsync();
        await Task.Delay(50);
        _recargas.Should().Be(0);

        _salvando = false;
        await AteAsync(() => _recargas > 0);
        await Task.Delay(50);

        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task A_failure_asking_the_browser_counts_as_not_editing_and_the_reload_runs()
    {
        _jsFalha = true;

        await Criar().SolicitarAsync();

        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task A_focused_text_field_defers_with_no_time_limit_until_it_is_released_then_one_reload()
    {
        using var recarga = Criar(TimeSpan.FromMilliseconds(100));
        _emEdicao = true;

        await recarga.SolicitarAsync();
        await Task.Delay(500); // five times the bound
        _recargas.Should().Be(0);

        _emEdicao = false;
        await AteAsync(() => _recargas > 0);
        await Task.Delay(50);

        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task A_save_stuck_beyond_the_bound_with_no_field_focused_reloads_anyway()
    {
        using var recarga = Criar(TimeSpan.FromMilliseconds(150));
        _salvando = true;

        await recarga.SolicitarAsync();
        _recargas.Should().Be(0);
        await AteAsync(() => _recargas > 0);

        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task Disposing_cancels_a_deferred_reload()
    {
        var recarga = Criar();
        _emEdicao = true;
        await recarga.SolicitarAsync();

        recarga.Dispose();
        _emEdicao = false;
        await Task.Delay(100);

        _recargas.Should().Be(0);
    }

    [Fact]
    public async Task A_failing_deferred_reload_is_reported_not_left_unobserved()
    {
        using var recarga = Criar();
        _emEdicao = true;
        _falhaDaRecarga = new HttpRequestException("fora do ar");
        await recarga.SolicitarAsync();

        _emEdicao = false;
        await AteAsync(() => _falhas.Count > 0);

        _falhas.Should().ContainSingle().Which.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task Disposing_while_the_browser_is_being_asked_drops_the_immediate_reload()
    {
        var resposta = new TaskCompletionSource<bool>();
        var recarga = new RecargaAdiada(() => { _recargas++; return Task.CompletedTask; }, () => resposta.Task, () => false,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(30), _falhas.Add);

        var solicitada = recarga.SolicitarAsync();
        recarga.Dispose();
        resposta.SetResult(false);
        await solicitada;

        _recargas.Should().Be(0, "a página já foi descartada: recarregar escreveria num componente que não existe mais");
    }

    [Fact]
    public async Task A_deferred_reload_that_fails_after_dispose_is_not_reported()
    {
        var recarregando = new TaskCompletionSource();
        _emEdicao = true;
        var recarga = new RecargaAdiada(async () => { _recargas++; await recarregando.Task; }, () => Task.FromResult(_emEdicao), () => false,
            TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(30), _falhas.Add);
        await recarga.SolicitarAsync();
        _emEdicao = false;
        await AteAsync(() => _recargas > 0);

        recarga.Dispose();
        recarregando.SetException(new InvalidOperationException("componente descartado"));
        await Task.Delay(50);

        _falhas.Should().BeEmpty("depois do descarte não há página para mostrar o erro");
    }
}
