using System.Net;
using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A aba Diário (E, Task 13), parte só do Personagem, dentro da página. O que se lê e as requisições ficam com a rede de
/// segurança (RedeAntecedentesEDiarioTests.Diario); aqui fica o que ela não vê: o componente com o estado da página, os
/// dados novos depois de cada ação (a rede responde sempre o mesmo Diário), os erros no alerta da página, o rascunho e a
/// edição sobrevivendo à troca de aba e a galeria do jogador (a da campanha).
/// </summary>
public class AbaDiarioTests : RedeDeSegurancaBase
{
    public static TheoryData<string> Papeis => new() { "GM", "Jogador" };

    private static readonly object[] EntradasDepois =
    {
        new { id = "d1", texto = "Chegamos à vila.", createdAt = "2026-10-01T12:00:00Z", imageUrls = Array.Empty<string>() },
        new { id = "d2", texto = "Encontramos o mapa.", createdAt = "2026-10-02T12:00:00Z", imageUrls = Array.Empty<string>() },
    };

    private async Task<IRenderedComponent<IComponent>> AbrirAsync(string papel = "GM")
    {
        var cut = await RenderFichaAsync("Personagem", papel);
        await IrParaAbaAsync(cut, "Diário");
        await EsperarCalmariaAsync();
        LimparRequisicoes();
        return cut;
    }

    private static IElement FormularioDaEntrada(IRenderedComponent<IComponent> cut) =>
        AbaAberta(cut).QuerySelectorAll("button").Single(b => TextoNormalizado(b) == "Adicionar Entrada" && b.GetAttribute("type") == "submit").Closest("form")!;

    private static IReadOnlyList<IElement> Cartoes(IRenderedComponent<IComponent> cut) => AbaAberta(cut).QuerySelectorAll(".mud-card").ToList();

    private static string Valor(IElement textarea) => textarea.GetAttribute("value") ?? textarea.TextContent;

    private static FichaEstado Estado(IRenderedComponent<IComponent> cut) => cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value!;

    // ---------------- o componente ----------------

    [Fact]
    public async Task The_diary_tab_is_the_kind_only_component_with_the_page_state()
    {
        var cut = await AbrirAsync();

        cut.FindComponents<DiarioDaFicha>().Should().ContainSingle();
        cut.FindComponent<DiarioDaFicha>().Instance.Estado.Should().BeSameAs(Estado(cut));
    }

    [Theory]
    [InlineData("NPC")]
    [InlineData("Criatura")]
    public async Task Kinds_without_a_diary_have_no_diary_component(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);

        cut.FindComponents<DiarioDaFicha>().Should().BeEmpty();
        cut.FindAll(".mud-tab").Select(TextoNormalizado).Should().NotContain("Diário");
    }

    // Review Focus (3): o jogador anexa e envia imagens na campanha da ficha; o GM, na galeria dele.
    [Theory]
    [MemberData(nameof(Papeis))]
    public async Task The_image_fields_use_the_campaign_only_for_a_player(string papel)
    {
        var cut = await AbrirAsync(papel);
        await cut.InvokeAsync(() => Botao(Cartoes(cut)[0], "Editar").Click());

        var anexos = cut.FindComponents<ImageAttachmentField>();
        anexos.Should().HaveCount(2);
        anexos.Select(a => a.Instance.CampaignId).Should().AllBe(papel == "GM" ? null : "c1");
        anexos.Select(a => a.Instance.AvailableImages).Should().AllSatisfy(l => l.Should().BeSameAs(Estado(cut).MinhasImagens));
    }

    // ---------------- dados novos depois de cada ação ----------------

    [Theory]
    [MemberData(nameof(Papeis))]
    public async Task Adding_an_entry_shows_the_reloaded_diary_and_clears_the_draft(string papel)
    {
        var cut = await AbrirAsync(papel);
        Responder("/character-sheets/s1/diary", EntradasDepois);

        await cut.InvokeAsync(() => FormularioDaEntrada(cut).QuerySelector("textarea")!.Change("Encontramos o mapa."));
        await cut.InvokeAsync(() => FormularioDaEntrada(cut).Submit());

        cut.WaitForAssertion(() =>
        {
            Cartoes(cut).Select(TextoNormalizado).Should().HaveCount(2).And.Contain(t => t.Contains("Encontramos o mapa."));
            Valor(FormularioDaEntrada(cut).QuerySelector("textarea")!).Should().BeEmpty();
        }, LimiteDeEspera);
    }

    [Theory]
    [MemberData(nameof(Papeis))]
    public async Task Saving_an_edit_shows_the_reloaded_entry(string papel)
    {
        var cut = await AbrirAsync(papel);
        await cut.InvokeAsync(() => Botao(Cartoes(cut)[0], "Editar").Click());
        await cut.InvokeAsync(() => Cartoes(cut)[0].QuerySelector("textarea")!.Change("Chegamos ao anoitecer."));
        Responder("/character-sheets/s1/diary", new[] { new { id = "d1", texto = "Chegamos ao anoitecer (revisto).", createdAt = "2026-10-01T12:00:00Z", imageUrls = Array.Empty<string>() } });

        await cut.InvokeAsync(() => Botao(Cartoes(cut)[0], "Salvar").Click());

        cut.WaitForAssertion(() =>
        {
            TextoNormalizado(Cartoes(cut).Single()).Should().Contain("Chegamos ao anoitecer (revisto).");
            Botao(Cartoes(cut)[0], "Editar").Should().NotBeNull();
        }, LimiteDeEspera);
    }

    [Theory]
    [MemberData(nameof(Papeis))]
    public async Task Deleting_an_entry_shows_the_reloaded_diary(string papel)
    {
        var cut = await AbrirAsync(papel);
        Responder("/character-sheets/s1/diary", Array.Empty<object>());

        await cut.InvokeAsync(() => Botao(Cartoes(cut)[0], "Excluir").Click());

        cut.WaitForAssertion(() => Cartoes(cut).Should().BeEmpty(), LimiteDeEspera);
    }

    // ---------------- erros no alerta da página ----------------

    [Fact]
    public async Task A_refused_entry_shows_in_the_page_alert_and_keeps_the_draft()
    {
        var cut = await AbrirAsync();
        ResponderErro("^/character-sheets/s1/diary$", HttpStatusCode.BadRequest, "Recusado.");

        await cut.InvokeAsync(() => FormularioDaEntrada(cut).QuerySelector("textarea")!.Change("Encontramos o mapa."));
        await cut.InvokeAsync(() => FormularioDaEntrada(cut).Submit());

        cut.WaitForAssertion(() => cut.Find(".mud-alert").TextContent.Should().Contain("Não foi possível adicionar a entrada."), LimiteDeEspera);
        Valor(FormularioDaEntrada(cut).QuerySelector("textarea")!).Should().Be("Encontramos o mapa.");
        await EsperarCalmariaAsync();
        Requisicoes.Should().ContainSingle().Which.Should().StartWith("POST ");
    }

    [Fact]
    public async Task A_refused_edit_shows_in_the_page_alert_and_stays_open()
    {
        var cut = await AbrirAsync();
        await cut.InvokeAsync(() => Botao(Cartoes(cut)[0], "Editar").Click());
        ResponderErro("/character-sheets/s1/diary/d1", HttpStatusCode.BadRequest, "Recusado.");

        await cut.InvokeAsync(() => Botao(Cartoes(cut)[0], "Salvar").Click());

        cut.WaitForAssertion(() => cut.Find(".mud-alert").TextContent.Should().Contain("Não foi possível salvar a entrada."), LimiteDeEspera);
        Botao(Cartoes(cut)[0], "Salvar").Should().NotBeNull();
    }

    [Fact]
    public async Task A_refused_upload_shows_in_the_page_alert()
    {
        var cut = await AbrirAsync();
        ResponderErro("/images", HttpStatusCode.BadRequest, "Recusado.");

        var anexo = cut.FindComponents<ImageAttachmentField>().Single();
        await cut.InvokeAsync(() => anexo.Instance.UploadForTests(new FakeBrowserFile("mapa.png")));

        cut.WaitForAssertion(() => cut.Find(".mud-alert").TextContent.Should().Contain("Não foi possível enviar a imagem."), LimiteDeEspera);
    }

    // ---------------- troca de aba ----------------

    // O rascunho e a edição em curso sobrevivem à troca de aba (antes do E eram campos da página).
    [Fact]
    public async Task The_draft_and_the_open_edit_survive_a_tab_switch()
    {
        var cut = await AbrirAsync();
        await cut.InvokeAsync(() => FormularioDaEntrada(cut).QuerySelector("textarea")!.Change("Rascunho."));
        await cut.InvokeAsync(() => Botao(Cartoes(cut)[0], "Editar").Click());
        await cut.InvokeAsync(() => Cartoes(cut)[0].QuerySelector("textarea")!.Change("Editando."));

        await IrParaAbaAsync(cut, "Combate");
        await IrParaAbaAsync(cut, "Diário");

        cut.WaitForAssertion(() =>
        {
            Valor(FormularioDaEntrada(cut).QuerySelector("textarea")!).Should().Be("Rascunho.");
            Valor(Cartoes(cut)[0].QuerySelector("textarea")!).Should().Be("Editando.");
        }, LimiteDeEspera);
        await EsperarCalmariaAsync();
        Requisicoes.Should().NotContain(r => !r.StartsWith("GET ", StringComparison.Ordinal));
    }
}
