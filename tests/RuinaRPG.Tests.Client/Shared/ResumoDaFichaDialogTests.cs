using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>Resumo da ficha de NPC/Criatura no encontro (Gerenciador de Encontros R0014): leitura, com o valor alterado destacado.</summary>
public class ResumoDaFichaDialogTests : MudBunitContext
{
    private readonly List<string> _requests = new();
    private HttpStatusCode _status = HttpStatusCode.OK;
    private string _mensagemDeErro = "";
    private TaskCompletionSource<bool>? _segurar;

    private sealed class EsperaHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }

    private static ResumoDeFichaResponse Resumo(string tipo = "Npc") => new("f1", tipo, "Goblin", "/images/goblin.png", 3, tipo == "Criatura" ? "D" : null,
        Atributos: new() { new("Forca", new(7, 10)), new("Vigor", new(8, 8)) },
        SubAtributos: new() { new("Iniciativa", new(4, 6)), new("Esquiva Natural", new(6, 6)) },
        Recursos: new() { new("Vitalidade", 12, new(8, 16)), new("Foco", 5, new(14, 14)) },
        Pericias: new() { new("Atletismo", "Atletismo", 2, "Forca", new(9, 12)) },
        Equipamentos: new() { new("Arma", "Espada longa", null), new("Artefato", "Anel de Força", "Antimagia") },
        MagiasEHabilidades: new() { new("Magia", "Bola de Fogo", "Antimagia"), new("Habilidade", "Golpe Forte", null) },
        Caracteristicas: new() { new("Corajoso", "Positiva", null) },
        Condicoes: new() { new("Fraqueza", "Perde força.", "Veneno", 3, new() { "Força −3", "Vitalidade −50% do total" }) });

    private async Task<IRenderedComponent<MudDialogProvider>> AbrirAsync(ResumoDeFichaResponse? resumo = null)
    {
        Services.AddScoped(_ => new HttpClient(new EsperaHandler(async request =>
        {
            _requests.Add(request.RequestUri!.AbsolutePath);
            if (_segurar is not null)
                await _segurar.Task;
            return _status == HttpStatusCode.OK
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(resumo ?? Resumo()) }
                : new HttpResponseMessage(_status) { Content = new StringContent(_mensagemDeErro) };
        })) { BaseAddress = new Uri("http://localhost/api/") });
        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        await cut.InvokeAsync(async () => await dialogs.ShowAsync<ResumoDaFichaDialog>(null,
            new DialogParameters<ResumoDaFichaDialog> { { x => x.EncounterId, "e1" }, { x => x.ParticipantId, "p1" }, { x => x.NomeDoParticipante, "Goblin 2" } }));
        return cut;
    }

    [Fact]
    public async Task Loads_the_resumo_of_the_participant_and_shows_identity_and_every_section()
    {
        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Bola de Fogo"));
        _requests.Should().ContainSingle().Which.Should().EndWith("/encounters/e1/participants/p1/resumo");
        cut.Markup.Should().Contain("Resumo da ficha").And.Contain("Goblin").And.Contain("Nível 3").And.Contain("/images/goblin.png");
        foreach (var secao in new[] { "Atributos", "Sub-Atributos", "Recursos", "Perícias", "Equipamento em uso", "Magias e Habilidades", "Características", "Condições ativas" })
            cut.Markup.Should().Contain(secao);
        cut.Markup.Should().Contain("Força").And.Contain("Esquiva Natural").And.Contain("Espada longa").And.Contain("Corajoso")
            .And.Contain("Força −3").And.Contain("Vitalidade −50% do total");
        cut.Markup.Should().Contain("Vitalidade: 12 / ").And.Contain("Foco: 5 / ");
    }

    [Fact]
    public async Task A_Criatura_shows_the_Rank_instead_of_the_Nivel_only()
    {
        var cut = await AbrirAsync(Resumo("Criatura"));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Rank D"));
        cut.Markup.Should().Contain("Nível 3");
    }

    [Fact]
    public async Task Highlights_only_the_values_a_condition_changed_with_the_original_struck_through()
    {
        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Bola de Fogo"));
        var riscados = cut.FindAll("s.resumo-original").Select(e => e.TextContent.Trim()).ToList();
        // Força 10 → 7, Iniciativa 6 → 4, Vitalidade total 16 → 8, Atletismo 12 → 9; Vigor, Esquiva e Foco não mudaram.
        riscados.Should().BeEquivalentTo("10", "6", "16", "12");
        cut.FindAll(".resumo-alterado").Should().HaveCount(4);
        cut.FindAll(".resumo-alterado").Select(e => e.TextContent).Should().Contain(t => t.Contains("7") && t.Contains("10"));
    }

    [Fact]
    public async Task Shows_the_Desabilitado_por_chip_only_on_the_disabled_rows()
    {
        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Bola de Fogo"));
        cut.FindAll(".mud-chip").Count(c => c.TextContent.Contains("Desabilitado por: Antimagia")).Should().Be(2);
        var linhas = cut.FindAll("li.resumo-linha");
        linhas.Single(l => l.TextContent.Contains("Espada longa")).TextContent.Should().NotContain("Desabilitado");
        linhas.Single(l => l.TextContent.Contains("Anel de Força")).TextContent.Should().Contain("Desabilitado por: Antimagia");
        linhas.Single(l => l.TextContent.Contains("Golpe Forte")).TextContent.Should().NotContain("Desabilitado");
    }

    [Theory]
    [InlineData("Npc", "npcs/f1")]
    [InlineData("Criatura", "criaturas/f1")]
    public async Task Offers_a_link_to_the_full_sheet_in_a_new_tab(string tipo, string href)
    {
        var cut = await AbrirAsync(Resumo(tipo));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Bola de Fogo"));
        var link = cut.FindAll("a").Single(a => a.TextContent.Contains("Abrir a ficha completa"));
        link.GetAttribute("href").Should().Be(href);
        link.GetAttribute("target").Should().Be("_blank");
    }

    [Fact]
    public async Task Shows_the_loading_state_until_the_resumo_arrives()
    {
        _segurar = new TaskCompletionSource<bool>();
        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.FindAll(".mud-progress-linear").Should().ContainSingle());
        cut.Markup.Should().NotContain("Bola de Fogo");
        _segurar.SetResult(true);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Bola de Fogo"));
        cut.FindAll(".mud-progress-linear").Should().BeEmpty();
    }

    [Fact]
    public async Task Shows_the_server_message_when_the_resumo_cannot_be_loaded()
    {
        _status = HttpStatusCode.BadRequest;
        _mensagemDeErro = "O resumo é só para NPCs e Criaturas.";

        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("O resumo é só para NPCs e Criaturas."));
        cut.FindAll(".mud-alert").Should().ContainSingle();
        cut.Markup.Should().NotContain("Atributos");
    }

    [Fact]
    public async Task A_missing_sheet_says_so()
    {
        _status = HttpStatusCode.NotFound;

        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("A ficha deste participante não existe mais."));
    }

    [Fact]
    public async Task Without_conditions_the_dialog_says_there_are_none_and_has_no_highlight()
    {
        var resumo = Resumo() with
        {
            Atributos = new() { new("Forca", new(10, 10)) }, SubAtributos = new(), Recursos = new() { new("Vitalidade", 16, new(16, 16)) },
            Pericias = new(), Condicoes = new()
        };
        var cut = await AbrirAsync(resumo);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Bola de Fogo"));
        cut.FindAll(".resumo-alterado").Should().BeEmpty();
        cut.Markup.Should().Contain("Nenhuma condição ativa.");
    }
}
