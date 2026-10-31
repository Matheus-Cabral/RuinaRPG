using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>
/// O salvamento do formulário inteiro leva, ao lado de cada Atual, o valor com que a ficha foi carregada: o servidor só
/// grava o Atual que o jogador editou (ele também escreve esses valores). O carregado acompanha cada releitura da ficha.
/// </summary>
[Collection("Tempo real")]
public class FichaAtuaisCarregadosTests : MudBunitContext
{
    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "GM") }, "test"))));
    }

    private static readonly string[] SingleObjectSuffixes =
    {
        "/attributes/budget", "/skills/budget", "/afinidades", "/sub-attributes", "/modificador-de-dano",
        "/racial-ability", "/racial-traits/pending", "/traits",
    };

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    // Um handler assíncrono: o PUT da ficha pode esperar uma "porta" para ficar em voo enquanto o teste edita de novo.
    private sealed class AsyncHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }

    private readonly List<string> _corpos = new();
    private object _ficha = new { };
    // Enquanto não nula, o PUT da ficha só responde quando ela abre.
    private TaskCompletionSource? _portaDoPut;

    private void Prepare()
    {
        Services.AddScoped(_ => new HttpClient(new AsyncHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Put && Regex.IsMatch(path, "-sheets/[^/]+$"))
            {
                _corpos.Add(await request.Content!.ReadAsStringAsync());
                if (_portaDoPut is { } porta)
                    await porta.Task;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            if (request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (path.EndsWith("/auth/me") || path.EndsWith("/level-up-notice"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (Regex.IsMatch(path, "-sheets/[^/]+$"))
                return Json(_ficha);
            return SingleObjectSuffixes.Any(path.EndsWith) ? Json(new { }) : Json(new List<object>());
        })) { BaseAddress = new Uri("http://localhost/api/") });
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider());
    }

    private static object Ficha(int vitalidade, int foco, int adrenalina, int estresse) => new
    {
        OwnerId = "u1", VitalidadeAtual = vitalidade, FocoAtual = foco, AdrenalinaAtual = adrenalina, EstresseAtual = estresse,
        VitalidadeMaximo = 30, FocoMaximo = 30, AdrenalinaMaximo = 30, EstresseMaximo = 30, Nivel = 1, Cobertura = "Nenhuma",
    };

    private IRenderedComponent<ComponentBase> RenderPagina(Type pagina) => pagina switch
    {
        _ when pagina == typeof(FichaDePersonagem) => Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1")),
        _ when pagina == typeof(FichaDeNpc) => Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1")),
        _ => Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1")),
    };

    private async Task EditarVitalidadeAsync(IRenderedComponent<ComponentBase> cut, int valor, int salvamentos)
    {
        var campo = cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Vitalidade");
        await cut.InvokeAsync(() => campo.Instance.ValueChanged.InvokeAsync(valor));
        cut.WaitForAssertion(() => _corpos.Should().HaveCount(salvamentos), TimeSpan.FromSeconds(3));
    }

    private static int Campo(string corpo, string nome) => JsonDocument.Parse(corpo).RootElement.GetProperty(nome).GetInt32();

    /// <summary>O campo, ou null quando o corpo não o tem (a Criatura não tem Estresse).</summary>
    private static int? CampoOpcional(string corpo, string nome) =>
        JsonDocument.Parse(corpo).RootElement.TryGetProperty(nome, out var valor) && valor.ValueKind != JsonValueKind.Null ? valor.GetInt32() : null;

    [Theory]
    [InlineData(typeof(FichaDePersonagem), "character", 3, 2)]
    [InlineData(typeof(FichaDeNpc), "npc", 3, 2)]
    [InlineData(typeof(FichaDeCriatura), "creature", null, null)]
    public async Task The_save_sends_the_loaded_values_and_a_reload_refreshes_them(Type pagina, string kind, int? estresseCarregado, int? estresseRecarregado)
    {
        _ficha = Ficha(12, 9, 6, 3);
        Prepare();
        await Services.GetRequiredService<AvisosDoHub>().StartAsync();
        var cut = RenderPagina(pagina);
        await Task.Delay(100);
        var combate = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == "Combate");
        await cut.InvokeAsync(() => combate.Click());

        await EditarVitalidadeAsync(cut, 20, salvamentos: 1);

        var primeiro = _corpos[0];
        (Campo(primeiro, "vitalidadeAtual"), Campo(primeiro, "vitalidadeAtualCarregada")).Should().Be((20, 12));
        (Campo(primeiro, "focoAtualCarregado"), Campo(primeiro, "adrenalinaAtualCarregada")).Should().Be((9, 6));
        CampoOpcional(primeiro, "estresseAtualCarregado").Should().Be(estresseCarregado);

        // O servidor mudou os Atuais (ex.: um ajuste "a cada vez") e avisou: a ficha se relê.
        _ficha = Ficha(7, 8, 5, 2);
        await cut.InvokeAsync(() => NotificationConnection.RaiseCondicoesAlteradasAsync(new CondicoesAlteradasNotification(kind, "sheet-1")));
        cut.WaitForAssertion(() => cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Vitalidade").Find("input").GetAttribute("value").Should().Be("7"));

        await EditarVitalidadeAsync(cut, 4, salvamentos: 2);

        var segundo = _corpos[1];
        (Campo(segundo, "vitalidadeAtual"), Campo(segundo, "vitalidadeAtualCarregada")).Should().Be((4, 7));
        (Campo(segundo, "focoAtual"), Campo(segundo, "focoAtualCarregado")).Should().Be((8, 8));
        Campo(segundo, "adrenalinaAtualCarregada").Should().Be(5);
        CampoOpcional(segundo, "estresseAtualCarregado").Should().Be(estresseRecarregado);
    }

    [Theory]
    [InlineData(typeof(FichaDePersonagem))]
    [InlineData(typeof(FichaDeNpc))]
    [InlineData(typeof(FichaDeCriatura))]
    public async Task A_second_save_in_the_same_burst_sends_the_first_saves_values_as_loaded(Type pagina)
    {
        _ficha = Ficha(12, 9, 6, 3);
        Prepare();
        await Services.GetRequiredService<AvisosDoHub>().StartAsync();
        var cut = RenderPagina(pagina);
        await Task.Delay(100);
        var combate = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == "Combate");
        await cut.InvokeAsync(() => combate.Click());

        // O primeiro PUT fica em voo enquanto a Vitalidade muda de novo: a releitura depois dele é adiada (há outra
        // edição pendente), então o segundo PUT não pode reenviar o carregado antigo (12).
        _portaDoPut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var campo = cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Vitalidade");
        await cut.InvokeAsync(() => campo.Instance.ValueChanged.InvokeAsync(20));
        // O PUT em voo não renderiza nada: espera pelo corpo sem depender de uma renderização.
        for (var i = 0; i < 60 && _corpos.Count == 0; i++)
            await Task.Delay(50);
        _corpos.Should().HaveCount(1);
        await cut.InvokeAsync(() => campo.Instance.ValueChanged.InvokeAsync(25));
        _portaDoPut.SetResult();
        _portaDoPut = null;
        cut.WaitForAssertion(() => _corpos.Should().HaveCount(2), TimeSpan.FromSeconds(3));

        var segundo = _corpos[1];
        (Campo(segundo, "vitalidadeAtual"), Campo(segundo, "vitalidadeAtualCarregada")).Should().Be((25, 20));
        (Campo(segundo, "focoAtual"), Campo(segundo, "focoAtualCarregado")).Should().Be((9, 9));
    }
}
