using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class PericiaCatalogoTests
{
    private static PericiaCatalogo Create(out Func<int> chamadas, HttpStatusCode status = HttpStatusCode.OK)
    {
        var count = 0;
        chamadas = () => count;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            count++;
            request.RequestUri!.AbsolutePath.Should().Be("/api/pericias");
            if (status != HttpStatusCode.OK)
                return new HttpResponseMessage(status);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<PericiaResponse>
            {
                new(4, "ArmasBrancas", "Armas Brancas", "Lâminas.", "Forca", false, false),
            }) };
        });
        return new PericiaCatalogo(http);
    }

    [Fact]
    public async Task Label_and_Descricao_come_from_the_api_and_load_only_once()
    {
        var catalogo = Create(out var chamadas);

        await catalogo.CarregarAsync();
        await catalogo.CarregarAsync();

        catalogo.Label("ArmasBrancas").Should().Be("Armas Brancas");
        catalogo.Descricao("ArmasBrancas").Should().Be("Lâminas.");
        catalogo.Ativas.Select(p => p.Chave).Should().Equal("ArmasBrancas");
        chamadas().Should().Be(1);
    }

    [Fact]
    public async Task Unknown_key_falls_back_to_the_key_itself()
    {
        var catalogo = Create(out _);
        await catalogo.CarregarAsync();

        catalogo.Label("Inexistente").Should().Be("Inexistente");
        catalogo.Descricao("Inexistente").Should().BeNull();
    }

    [Fact]
    public async Task A_failed_load_leaves_the_catalog_empty_and_is_retried_next_time()
    {
        var catalogo = Create(out var chamadas, HttpStatusCode.InternalServerError);

        await catalogo.CarregarAsync();
        await catalogo.CarregarAsync();

        catalogo.Ativas.Should().BeEmpty();
        catalogo.Label("ArmasBrancas").Should().Be("ArmasBrancas");
        chamadas().Should().Be(2);
    }

    [Fact]
    public async Task Recarregar_fetches_again()
    {
        var catalogo = Create(out var chamadas);

        await catalogo.CarregarAsync();
        await catalogo.RecarregarAsync();

        chamadas().Should().Be(2);
    }

    private sealed class GatedHandler : HttpMessageHandler
    {
        public int Chamadas;
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Chamadas);
            await Gate.Task;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<PericiaResponse>
            {
                new(4, "ArmasBrancas", "Armas Brancas", "Lâminas.", "Forca", false, false),
            }) };
        }
    }

    [Fact]
    public async Task Concurrent_loads_share_a_single_request()
    {
        var handler = new GatedHandler();
        var catalogo = new PericiaCatalogo(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") });

        var a = catalogo.CarregarAsync();
        var b = catalogo.CarregarAsync();
        handler.Gate.SetResult();
        await Task.WhenAll(a, b);

        handler.Chamadas.Should().Be(1);
        catalogo.Label("ArmasBrancas").Should().Be("Armas Brancas");
    }
}
