using System.Net;
using System.Text.Json.Nodes;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// O alto das três páginas (E, Task 13): <see cref="CabecalhoDaFicha"/> (trilha, título, Condições ativas, alerta de erro,
/// aviso de Novo Nível) e <see cref="EscolhaRacialDialog"/> (onde o tipo tem Traços Raciais). O que se lê e as requisições
/// ficam com a rede de segurança (RedeCabecalhoEInformacoesBasicasTests.Cabecalho, RedePartesPorTipoTests); aqui fica o
/// que ela não vê: os componentes com o estado da página, dados novos depois de salvar (a rede responde sempre o mesmo) e
/// o erro de uma escolha recusada no alerta da página.
/// </summary>
public class CabecalhoDaFichaTests : RedeDeSegurancaBase
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComTracosRaciais => new() { "Personagem", "NPC" };

    private static FichaEstado Estado(IRenderedComponent<IComponent> cut) => cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value!;

    private static readonly object Pendente = new
    {
        pending = true,
        gratuita = new[] { new { traitNome = "Ágil", especificacao = (string?)null, requerEspecificacao = false } },
        obrigatoria = Array.Empty<object>(),
    };

    /// <summary>Muda o nome da ficha pela aba Informações Básicas (autosave) e espera a recarga.</summary>
    private async Task SalvarNomeAsync(IRenderedComponent<IComponent> cut, string tipo)
    {
        var http = Services.GetRequiredService<HttpClient>();
        var ficha = JsonNode.Parse(await http.GetStringAsync($"{Prefixo(tipo)}/{SheetId}"))!.AsObject();
        ficha["nome"] = "Novo Nome";
        Responder($"^/{Prefixo(tipo)}/{SheetId}$", ficha.ToJsonString());
        LimparRequisicoes();
        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Nome").Change("Novo Nome"));
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Contain($"GET /{Prefixo(tipo)}/{SheetId}/level-up-notice"));
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_header_is_the_shared_component_with_the_page_state(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);

        cut.FindComponents<CabecalhoDaFicha>().Should().ContainSingle();
        cut.FindComponent<CabecalhoDaFicha>().Instance.Estado.Should().BeSameAs(Estado(cut));
    }

    [Theory]
    [InlineData("Personagem", 1)]
    [InlineData("NPC", 1)]
    [InlineData("Criatura", 0)]
    public async Task The_racial_choice_dialog_is_only_where_the_kind_has_racial_traits(string tipo, int dialogos)
    {
        var cut = await RenderFichaAsync(tipo);

        cut.FindComponents<EscolhaRacialDialog>().Should().HaveCount(dialogos);
    }

    // Um salvamento que sobe o Nível traz o aviso novo (a página relê o aviso depois de salvar).
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_save_that_levels_up_shows_the_new_notice(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        Responder("/level-up-notice", new { bonusTexts = new[] { "+1 Vigor" } });

        await SalvarNomeAsync(cut, tipo);

        cut.WaitForAssertion(() => TextoNormalizado(cut.Find(".mud-alert")).Should().Contain("Novo Nível!").And.Contain("+1 Vigor"), LimiteDeEspera);
        cut.FindAll(".mud-breadcrumb-item").Select(TextoNormalizado).Last().Should().Be("Novo Nome");
    }

    // A troca de Variante pode deixar uma escolha pendente: o diálogo abre depois do salvamento.
    [Theory]
    [MemberData(nameof(TiposComTracosRaciais))]
    public async Task A_save_that_leaves_a_pending_choice_opens_the_dialog(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        cut.FindAll(".mud-dialog").Should().BeEmpty();
        Responder("/racial-traits/pending", Pendente);

        await SalvarNomeAsync(cut, tipo);

        cut.WaitForAssertion(() => TextoNormalizado(cut.Find(".mud-dialog")).Should().Contain("Ágil"), LimiteDeEspera);
    }

    [Theory]
    [MemberData(nameof(TiposComTracosRaciais))]
    public async Task A_refused_choice_shows_in_the_page_alert_and_keeps_the_dialog(string tipo)
    {
        Responder("/racial-traits/pending", Pendente);
        var cut = await RenderFichaAsync(tipo);
        ResponderErro("/racial-traits/resolve", HttpStatusCode.BadRequest, "Variante sem escolha.");

        var radio = Dialogo(cut).QuerySelectorAll("label").First(l => TextoNormalizado(l) == "Ágil").QuerySelector("input")!;
        await cut.InvokeAsync(() => radio.Click());
        await cut.InvokeAsync(() => Botao(Dialogo(cut), "Confirmar").Click());

        cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Select(TextoNormalizado).Should().Contain(t => t.Contains("Variante sem escolha.")), LimiteDeEspera);
        cut.FindAll(".mud-dialog").Should().ContainSingle();
    }

    // As Condições ativas de uma ficha concedida: o alerta aparece quando a recarga diz que a ficha foi concedida.
    [Theory]
    [InlineData("NPC")]
    [InlineData("Criatura")]
    public async Task The_conditions_alert_follows_the_granted_flag_after_a_reload(string tipo)
    {
        var cut = await RenderFichaAsync(tipo, "GM");
        cut.FindComponents<RuinaRPG.Client.Shared.CondicoesAtivasAlert>().Should().BeEmpty();

        var http = Services.GetRequiredService<HttpClient>();
        var ficha = JsonNode.Parse(await http.GetStringAsync($"{Prefixo(tipo)}/{SheetId}"))!.AsObject();
        ficha["ownerId"] = "jogador-1";
        ficha["nome"] = "Novo Nome";
        Responder($"^/{Prefixo(tipo)}/{SheetId}$", ficha.ToJsonString());
        LimparRequisicoes();
        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Nome").Change("Novo Nome"));

        cut.WaitForAssertion(() => cut.FindComponents<RuinaRPG.Client.Shared.CondicoesAtivasAlert>().Should().ContainSingle(), LimiteDeEspera);
        await EsperarCalmariaAsync();
    }
}
