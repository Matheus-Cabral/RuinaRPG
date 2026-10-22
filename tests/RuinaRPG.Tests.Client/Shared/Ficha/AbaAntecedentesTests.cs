using System.Net;
using System.Text.Json.Nodes;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A aba Antecedentes compartilhada (E, Task 9) dentro das páginas que a têm (Personagem e NPC): uma por página, com o
/// estado da ficha; Estrela, Histórico e História vêm do formulário auto-salvo da página; o editor grava no endereço do
/// tipo; a Criatura não tem a aba. O que se vê e as requisições ficam com a rede de segurança (RedeAntecedentesEDiarioTests).
/// </summary>
public class AbaAntecedentesTests : RedeDeSegurancaBase
{
    public static TheoryData<string> TiposComAntecedentes => new() { "Personagem", "NPC" };

    private async Task<IRenderedComponent<IComponent>> AbrirAsync(string tipo, string papel = "GM")
    {
        var cut = await RenderFichaAsync(tipo, papel);
        await IrParaAbaAsync(cut, "Antecedentes");
        await EsperarCalmariaAsync();
        return cut;
    }

    [Theory]
    [MemberData(nameof(TiposComAntecedentes))]
    public async Task The_tab_is_the_shared_component_with_the_page_state(string tipo)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponents<AbaAntecedentes>().Should().ContainSingle();
        cut.FindComponent<AbaAntecedentes>().Instance.Estado.Should().BeSameAs(cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value);
    }

    // Estrela, Histórico e História moram no formulário auto-salvo da página, que o passa (com o retorno que salva) à aba.
    [Theory]
    [InlineData("Personagem", "Nasceu no porto.")]
    [InlineData("NPC", "Ferreiro do vale.")]
    public async Task The_tab_gets_the_fields_from_the_page_form(string tipo, string historia)
    {
        var cut = await AbrirAsync(tipo);

        var aba = cut.FindComponent<AbaAntecedentes>().Instance;
        aba.Formulario.Tipo.Nome.Should().Be(tipo);
        aba.Formulario.Estrela.Should().BeNull();
        aba.Formulario.HistoricoId.Should().BeNull();
        aba.Formulario.Historia.Should().Be(historia);
        aba.AoMudar.HasDelegate.Should().BeTrue();
        cut.FindComponent<HistoriaEditor>().Instance.Html.Should().Be(historia);
        cut.FindComponent<HistoriaEditor>().Instance.HtmlChanged.HasDelegate.Should().BeTrue();
        aba.Estado.HistoriaCarregada.Should().BeTrue();
    }

    [Theory]
    [InlineData("Personagem", "character-sheets/s1/historia")]
    [InlineData("NPC", "npc-sheets/s1/historia")]
    public async Task The_historia_editor_saves_at_the_kind_address(string tipo, string url)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponent<HistoriaEditor>().Instance.SaveUrl.Should().Be(url);
    }

    [Fact]
    public async Task The_creature_page_has_no_antecedentes()
    {
        var cut = await RenderFichaAsync("Criatura");

        cut.FindComponents<AbaAntecedentes>().Should().BeEmpty();
    }

    // Mesmo se alguém a puser numa página sem Antecedentes, a aba não mostra nada.
    [Fact]
    public void The_tab_renders_nothing_where_the_kind_has_no_antecedentes()
    {
        var estado = new FichaEstado(Services.GetRequiredService<HttpClient>(), TipoDeFichaCliente.Criatura, SheetId);

        var cut = Render<CascadingValue<FichaEstado>>(p => p
            .Add(c => c.Value, estado)
            .AddChildContent<AbaAntecedentes>());

        cut.Markup.Trim().Should().BeEmpty();
    }

    // Depois do autosave a página relê a ficha: o que voltou DIFERENTE do servidor tem de aparecer na aba (a rede de
    // segurança devolve a mesma ficha e não vê uma aba que deixou de re-renderizar).
    [Theory]
    [MemberData(nameof(TiposComAntecedentes))]
    public async Task After_a_save_the_tab_shows_the_reloaded_sheet(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        var ficha = JsonNode.Parse(await Services.GetRequiredService<HttpClient>().GetStringAsync($"{Prefixo(tipo)}/{SheetId}"))!;
        ficha["estrela"] = "Aeurer";
        ficha["historicoId"] = "h1";
        Responder($"/{Prefixo(tipo)}/{SheetId}", ficha.ToJsonString());
        LimparRequisicoes();

        await EscolherOpcaoAsync(cut, () => AbaAberta(cut), "Estrela", "I — AEURER — Estrela da Curiosidade");

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Contain($"GET /{Prefixo(tipo)}/{SheetId}"));
        cut.WaitForAssertion(() =>
        {
            cut.FindComponent<HistoricoSelect>().Instance.Value.Should().Be("h1");
            cut.FindComponents<InfoPopup>().Select(p => p.Instance.Title).Should().Contain("Ferreiro");
        });
    }

    // Um erro do editor (aqui, a galeria recusada) aparece no alerta da página, que fica fora da aba.
    [Theory]
    [MemberData(nameof(TiposComAntecedentes))]
    public async Task An_editor_error_shows_in_the_page_alert(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        ResponderErro("/historia/imagens", HttpStatusCode.BadRequest, "Galeria recusada.");

        var anexo = cut.FindComponents<ImageAttachmentField>().Single();
        await cut.InvokeAsync(() => anexo.Instance.UploadForTests(new FakeBrowserFile("mapa.png")));

        cut.WaitForAssertion(() => cut.FindComponent<DismissibleAlert>().Instance.Message.Should().Be("Galeria recusada."));
        cut.WaitForAssertion(() => cut.FindComponent<DismissibleAlert>().Markup.Should().Contain("Galeria recusada."));
    }
}
