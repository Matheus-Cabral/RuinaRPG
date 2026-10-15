using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.SpellsAndAbilities;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class EditarMagiaHabilidadeDialogTests : MudBunitContext
{
    private readonly List<string> _requests = new();
    private UpdateSheetSpellAbilityRequest? _corpo;

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private void RegisterHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            if (request.Method == HttpMethod.Put)
                _corpo = request.Content!.ReadFromJsonAsync<UpdateSheetSpellAbilityRequest>().GetAwaiter().GetResult();
            return respond(request);
        }));

    private async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> AbrirAsync(
        string saveUrl, string nome, string tipo, int grau, string descricao, List<SpellAbilityEffectResponse> efeitos)
    {
        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        IDialogReference? reference = null;
        await cut.InvokeAsync(async () => reference = await dialogs.ShowAsync<EditarMagiaHabilidadeDialog>("Editar Magia",
            new DialogParameters<EditarMagiaHabilidadeDialog>
            {
                { x => x.SaveUrl, saveUrl }, { x => x.Nome, nome }, { x => x.Tipo, tipo }, { x => x.Grau, grau },
                { x => x.Descricao, descricao }, { x => x.Efeitos, efeitos },
            }));
        return (cut, reference!);
    }

    [Fact]
    public async Task Opens_filled_with_the_entry_and_Salvar_puts_the_edited_fields_to_SaveUrl()
    {
        RegisterHttp(r => r.Method == HttpMethod.Put ? Json(new { }) : Json(new List<object>()));
        var (cut, dialog) = await AbrirAsync("character-sheets/s1/spell-abilities/e1", "Bola de Fogo", "Magia", 3, "Uma explosão.",
            [new SpellAbilityEffectResponse("Dano", 4, 8)]);

        cut.Markup.Should().Contain("Bola de Fogo");
        cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Nome").Find("input").Change("Bola de Fogo Maior");
        cut.Find("button:contains('Salvar')").Click();
        var resultado = await dialog.Result;

        _requests.Should().Contain(r => r.StartsWith("PUT") && r.EndsWith("character-sheets/s1/spell-abilities/e1"));
        _corpo!.Nome.Should().Be("Bola de Fogo Maior");
        _corpo.Tipo.Should().Be("Magia");
        _corpo.Grau.Should().Be(3);
        _corpo.Efeitos.Should().ContainSingle(e => e.EfeitoNome == "Dano" && e.Quantidade == 4 && e.CustoPI == 8);
        resultado!.Canceled.Should().BeFalse();
    }

    [Fact]
    public async Task A_refused_save_shows_the_api_message_and_stays_open()
    {
        RegisterHttp(r => r.Method == HttpMethod.Put
            ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Pontos de Ignição insuficientes: custa 6 e a ficha tem 5.") }
            : Json(new List<object>()));
        var (cut, _) = await AbrirAsync("npc-sheets/s1/spell-abilities/e1", "Bola de Fogo", "Magia", 3, "x", [new SpellAbilityEffectResponse("Dano", 4, 8)]);

        cut.Find("button:contains('Salvar')").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Pontos de Ignição insuficientes: custa 6 e a ficha tem 5."));
    }

    [Fact]
    public async Task The_Tipo_select_offers_only_Magia_and_Habilidade()
    {
        RegisterHttp(_ => Json(new List<object>()));
        var (cut, _) = await AbrirAsync("character-sheets/s1/spell-abilities/e1", "Bola de Fogo", "Magia", 3, "x", []);

        var tipo = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Tipo");

        tipo.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().Equal("Magia", "Habilidade");
    }
}
