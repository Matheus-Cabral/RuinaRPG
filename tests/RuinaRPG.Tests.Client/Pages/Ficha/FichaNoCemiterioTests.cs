using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Services;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using System.Net;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages.Ficha;

/// <summary>
/// Ficha de Personagem R0010 (cemitério) no cliente: o aviso com os motivos, a ficha inteira só leitura (a cascata de
/// desabilitação do MudBlazor nas abas e o editor da História), a caixa "Morto" do GM, o aviso em tempo real e o
/// breadcrumb. Usa a página inteira com os dados ricos da rede de segurança (uma linha em cada lista), para que os
/// botões das linhas também sejam verificados.
/// </summary>
[Collection(TempoRealCollection.Nome)]
public sealed class FichaNoCemiterioTests : RedeDeSegurancaBase
{
    private const string Recusa = "Esta ficha está no cemitério e não pode ser editada.";

    private static readonly string[] Abas =
        { "Informações Básicas", "Atributos & Perícias", "Combate", "Magias & Habilidades", "Posses", "Antecedentes", "Diário" };

    // A ficha da rede (Personagem "Aria"), com o estado do cemitério.
    private static object Aria(bool noCemiterio, params string[] motivos) => new
    {
        id = SheetId, campaignId = "c1", campaignName = "Crônicas de Alker", ownerId = "u2", imageUrl = "/img/um.png", imageId = "img1",
        nome = "Aria", linhagem = "Humano", variante = "Sinir", vocacao = "Feiticeiro", subVocacao = "Piromante", afinidade = "Fogo", propriedade = "Uma casa",
        nivel = 3, circulo = 1, grau = 2, possuiCoracaoDeMana = true, experienciaAtual = 120, eapAtual = 7,
        nucleosRankF = 1, nucleosRankE = 2, nucleosRankD = 0, nucleosRankC = 0, nucleosRankB = 0, nucleosRankA = 0, nucleosRankS = 0,
        pontosDeIgnicaoAtual = 12, pontosDeIgnicaoTotal = 40, vitalidadeAtual = 20, focoAtual = 10, adrenalinaAtual = 3, estresseAtual = 1,
        cobertura = "Nenhuma", ciclos = 4, graduacao = 1, graduacaoLabel = "Círculo",
        vitalidadeMaximo = 25, focoMaximo = 12, adrenalinaMaximo = 5, estresseMaximo = 10,
        xpParaProximoNivel = 30, pontosDeIgnicaoBonusManual = 2, pontosDePericiaBonusCritico = 1,
        arcaRolada = (int?)null, estrela = (string?)null, sinaAtual = 1, historicoId = (string?)null, equipmentKitId = (string?)null, historia = "Nasceu no porto.",
        afinidadeAdicional = 0, vocacaoArcanaGasta = 2, vocacaoArcanaMaxima = 6,
        noCemiterio, motivosDoCemiterio = motivos,
    };

    private void FichaNoCemiterio(params string[] motivos) => Responder("^/character-sheets/s1$", Aria(true, motivos));

    private void FichaForaDoCemiterio() => Responder("^/character-sheets/s1$", Aria(false));

    private static IElement Raiz(IRenderedComponent<IComponent> cut) => cut.Find(".ficha-condicoes-raiz");

    private static IElement? CaixaMorto(IRenderedComponent<IComponent> cut) =>
        Raiz(cut).QuerySelectorAll(".mud-checkbox").SingleOrDefault(c => TextoNormalizado(c) == "Morto")?.QuerySelector("input");

    // Os botões que só mostram (a descrição de uma Perícia, a ajuda ⓘ): não editam nada e continuam utilizáveis.
    private const string SoMostram = ".rr-pericia-nome, .rr-info-popup";

    // Os controles da aba aberta que ainda aceitam edição: input, textarea, button e MudSelect sem o estado desabilitado.
    private static List<string> ControlesHabilitados(IRenderedComponent<IComponent> cut)
    {
        var aba = AbaAberta(cut);
        var controles = aba.QuerySelectorAll("input, textarea, button")
            .Where(e => e.GetAttribute("type") != "hidden" && !e.HasAttribute("disabled") && !e.Matches(SoMostram))
            .Select(e => $"{e.LocalName} «{TextoNormalizado(e.Closest(".mud-input-control, .mud-checkbox, button") ?? e)}»");
        var selects = aba.QuerySelectorAll(".mud-select")
            .Where(s => s.QuerySelector(".mud-disabled") is null)
            .Select(s => $"select «{TextoNormalizado(s)}»");
        return controles.Concat(selects).ToList();
    }

    private async Task<IRenderedComponent<IComponent>> AbrirAsync(string papel)
    {
        var cut = await RenderFichaAsync("Personagem", papel);
        // O MainLayout abre a conexão no app; aqui o teste faz esse papel (sem as Notas Secretas: nada de HTTP a mais).
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync(notasSecretas: false);
        return cut;
    }

    [Fact]
    public async Task Ficha_no_cemiterio_mostra_o_aviso_com_os_motivos()
    {
        FichaNoCemiterio("Morte", "RemovidoDaCampanha", "ContaExcluida");

        var cut = await AbrirAsync("Jogador");

        var aviso = Raiz(cut).QuerySelectorAll(".mud-alert").Single(a => TextoNormalizado(a).StartsWith("Esta ficha está no cemitério"));
        TextoNormalizado(aviso).Should().Be("Esta ficha está no cemitério: Personagem morto, Jogador removido da campanha, Conta do jogador excluída.");
        aviso.QuerySelector("svg").Should().NotBeNull("o aviso tem ícone MudBlazor");
    }

    [Fact]
    public async Task Ficha_fora_do_cemiterio_nao_mostra_o_aviso()
    {
        var cut = await AbrirAsync("Jogador");

        TextoNormalizado(Raiz(cut)).Should().NotContain("cemitério");
    }

    [Theory]
    [InlineData("GM")]
    [InlineData("Jogador")]
    public async Task Ficha_no_cemiterio_nao_tem_controle_habilitado(string papel)
    {
        FichaNoCemiterio("Morte");
        var cut = await AbrirAsync(papel);

        foreach (var aba in Abas)
        {
            await IrParaAbaAsync(cut, aba);
            cut.WaitForAssertion(() => ControlesHabilitados(cut).Should().BeEmpty($"a aba {aba} está só leitura"));
        }

        // As abas continuam navegáveis (é como se chegou até aqui) e, para o GM, a caixa Morto continua utilizável.
        if (papel == "GM")
            CaixaMorto(cut)!.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Ficha_fora_do_cemiterio_tem_controles_habilitados()
    {
        var cut = await AbrirAsync("Jogador");

        foreach (var aba in Abas)
        {
            await IrParaAbaAsync(cut, aba);
            cut.WaitForAssertion(() => ControlesHabilitados(cut).Should().NotBeEmpty($"a aba {aba} é editável"));
        }
    }

    [Fact]
    public async Task As_ajudas_continuam_abrindo_no_cemiterio()
    {
        FichaNoCemiterio("Morte");
        var cut = await AbrirAsync("Jogador");

        await IrParaAbaAsync(cut, "Atributos & Perícias");

        cut.WaitForAssertion(() => AbaAberta(cut).QuerySelectorAll(SoMostram).Should().NotBeEmpty());
        AbaAberta(cut).QuerySelectorAll(SoMostram).Should().OnlyContain(b => !b.HasAttribute("disabled"));
    }

    [Fact]
    public async Task O_editor_da_historia_abre_so_leitura_no_cemiterio()
    {
        FichaNoCemiterio("Morte");
        var cut = await AbrirAsync("Jogador");

        await IrParaAbaAsync(cut, "Antecedentes");

        cut.WaitForAssertion(() => JSInterop.Invocations["ruinaRichText.create"].Should().ContainSingle());
        JSInterop.Invocations["ruinaRichText.create"].Single().Arguments[3].Should().BeEquivalentTo(new { somenteLeitura = true });
    }

    [Fact]
    public async Task GM_ve_a_caixa_Morto_e_jogador_nao()
    {
        var gm = await AbrirAsync("GM");
        CaixaMorto(gm).Should().NotBeNull();
        CaixaMorto(gm)!.HasAttribute("checked").Should().BeFalse();
    }

    [Fact]
    public async Task Jogador_nao_ve_a_caixa_Morto()
    {
        FichaNoCemiterio("Morte");

        var jogador = await AbrirAsync("Jogador");

        CaixaMorto(jogador).Should().BeNull();
        TextoNormalizado(Raiz(jogador)).Should().NotContain("Morto ");
    }

    [Fact]
    public async Task A_caixa_Morto_do_GM_vem_marcada_quando_o_motivo_e_Morte()
    {
        FichaNoCemiterio("Morte");

        var cut = await AbrirAsync("GM");

        CaixaMorto(cut)!.HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public async Task Marcar_Morto_chama_o_endpoint_e_recarrega()
    {
        var cut = await AbrirAsync("GM");
        LimparRequisicoes();
        FichaNoCemiterio("Morte");

        await cut.InvokeAsync(() => CaixaMorto(cut)!.Change(true));

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal("PUT /character-sheets/s1/morte {\"morto\":true}", "GET /character-sheets/s1",
            // Entrou no cemitério: o formulário é relido do servidor (nenhuma edição recusada fica na tela).
            "GET /character-sheets/s1"));
        cut.WaitForAssertion(() => TextoNormalizado(Raiz(cut)).Should().Contain("Esta ficha está no cemitério: Personagem morto."));
        CaixaMorto(cut)!.HasAttribute("checked").Should().BeTrue();
        ControlesHabilitados(cut).Should().BeEmpty();
    }

    [Fact]
    public async Task Desmarcar_Morto_devolve_a_ficha_a_edicao()
    {
        FichaNoCemiterio("Morte");
        var cut = await AbrirAsync("GM");
        LimparRequisicoes();
        FichaForaDoCemiterio();

        await cut.InvokeAsync(() => CaixaMorto(cut)!.Change(false));

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal("PUT /character-sheets/s1/morte {\"morto\":false}", "GET /character-sheets/s1"));
        cut.WaitForAssertion(() => TextoNormalizado(Raiz(cut)).Should().NotContain("cemitério"));
        ControlesHabilitados(cut).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Aviso_em_tempo_real_poe_e_tira_do_modo_leitura()
    {
        var cut = await AbrirAsync("Jogador");
        var navegacao = Services.GetRequiredService<NavigationManager>();
        var endereco = navegacao.Uri;
        ControlesHabilitados(cut).Should().NotBeEmpty();

        // GM marca Morto: o servidor avisa o dono com o id da ficha.
        FichaNoCemiterio("Morte");
        await cut.InvokeAsync(() => NotificationConnection.RaiseFichaNoCemiterioAlteradaAsync(SheetId));

        cut.WaitForAssertion(() => TextoNormalizado(Raiz(cut)).Should().Contain("Esta ficha está no cemitério: Personagem morto."));
        ControlesHabilitados(cut).Should().BeEmpty();

        // O aviso de outra ficha não mexe nesta.
        FichaForaDoCemiterio();
        LimparRequisicoes();
        await cut.InvokeAsync(() => NotificationConnection.RaiseFichaNoCemiterioAlteradaAsync("outra"));
        await EsperarCalmariaAsync();
        Requisicoes.Should().BeEmpty();

        // GM desmarca: a ficha aberta volta a editável sem recarregar a página.
        await cut.InvokeAsync(() => NotificationConnection.RaiseFichaNoCemiterioAlteradaAsync(SheetId));

        cut.WaitForAssertion(() => TextoNormalizado(Raiz(cut)).Should().NotContain("cemitério"));
        ControlesHabilitados(cut).Should().NotBeEmpty();
        navegacao.Uri.Should().Be(endereco);
    }

    [Fact]
    public async Task Recusa_409_do_auto_save_aparece_na_pagina()
    {
        var cut = await AbrirAsync("Jogador");
        ResponderErro("^/character-sheets/s1$", HttpStatusCode.Conflict, Recusa);

        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Nome").Change("Novo"));

        cut.WaitForAssertion(() => Raiz(cut).QuerySelectorAll(".mud-alert").Select(TextoNormalizado).Should().Contain(a => a.Contains(Recusa)),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Recusa_409_do_auto_save_dos_pontos_de_ignicao_aparece_na_pagina()
    {
        var cut = await AbrirAsync("Jogador");
        ResponderErro("/character-sheets/s1/pontos-de-ignicao", HttpStatusCode.Conflict, Recusa);

        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Pontos de Ignição Atual").Change("20"));

        cut.WaitForAssertion(() => Raiz(cut).QuerySelectorAll(".mud-alert").Select(TextoNormalizado).Should().Contain(a => a.Contains(Recusa)),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Recusa_409_depois_de_um_salvamento_com_recarga_adiada_continua_no_alerta()
    {
        // Spec 2.1.0 A.4: o Nome salva e a releitura dele fica adiada (o PI está pendente); o PI recebe 409. Quando tudo
        // assenta, a releitura adiada não pode ter apagado a recusa do alerta.
        var cut = await AbrirAsync("Jogador");
        ResponderErro("/character-sheets/s1/pontos-de-ignicao", HttpStatusCode.Conflict, Recusa);

        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Nome").Change("Novo"));
        await Task.Delay(100);
        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Pontos de Ignição Atual").Change("20"));
        await Task.Delay(1500);

        cut.WaitForAssertion(() =>
        {
            Raiz(cut).QuerySelectorAll(".mud-alert").Select(TextoNormalizado).Should().Contain(a => a.Contains(Recusa));
            Campo(AbaAberta(cut), "Pontos de Ignição Atual").GetAttribute("value").Should().Be("12");
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task No_cemiterio_o_avatar_so_mostra_a_imagem()
    {
        FichaNoCemiterio("Morte");
        var cut = await AbrirAsync("Jogador");

        await cut.InvokeAsync(() => AbaAberta(cut).QuerySelector(".mud-avatar")!.Click());

        var dialogo = Dialogo(cut);
        dialogo.QuerySelectorAll("button").Select(TextoNormalizado).Should().Equal("Fechar");
        dialogo.QuerySelectorAll("input").Should().BeEmpty();
        dialogo.QuerySelector("img").Should().NotBeNull();
    }

    [Fact]
    public async Task Edicao_pendente_recusada_volta_ao_valor_do_servidor_e_sair_do_cemiterio_limpa_a_recusa()
    {
        // Review Focus 1: o jogador digita, o GM marca Morto antes do auto-save; o save recebe 409.
        var cut = await AbrirAsync("Jogador");
        ResponderErro("/character-sheets/s1/pontos-de-ignicao", HttpStatusCode.Conflict, Recusa);
        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Pontos de Ignição Atual").Change("20"));
        FichaNoCemiterio("Morte");
        await cut.InvokeAsync(() => NotificationConnection.RaiseFichaNoCemiterioAlteradaAsync(SheetId));

        // A recusa fica no alerta e o campo volta ao que o servidor tem (nada recusado fica na tela).
        cut.WaitForAssertion(() =>
        {
            Raiz(cut).QuerySelectorAll(".mud-alert").Select(TextoNormalizado).Should().Contain(a => a.Contains(Recusa));
            Campo(AbaAberta(cut), "Pontos de Ignição Atual").GetAttribute("value").Should().Be("12");
        }, TimeSpan.FromSeconds(3));
        ControlesHabilitados(cut).Should().BeEmpty();

        // O GM desmarca: a ficha volta a editável e a recusa, que não vale mais, sai do alerta.
        FichaForaDoCemiterio();
        await cut.InvokeAsync(() => NotificationConnection.RaiseFichaNoCemiterioAlteradaAsync(SheetId));

        cut.WaitForAssertion(() => TextoNormalizado(Raiz(cut)).Should().NotContain("cemitério"));
        ControlesHabilitados(cut).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Recusa_409_recarrega_o_formulario_com_o_valor_do_servidor()
    {
        var cut = await AbrirAsync("Jogador");
        ResponderErro("/character-sheets/s1/pontos-de-ignicao", HttpStatusCode.Conflict, Recusa);

        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Pontos de Ignição Atual").Change("20"));

        cut.WaitForAssertion(() =>
        {
            Raiz(cut).QuerySelectorAll(".mud-alert").Select(TextoNormalizado).Should().Contain(a => a.Contains(Recusa));
            Campo(AbaAberta(cut), "Pontos de Ignição Atual").GetAttribute("value").Should().Be("12");
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Falha_ao_reler_o_estado_do_cemiterio_aparece_na_pagina()
    {
        var cut = await AbrirAsync("Jogador");
        Responder("^/character-sheets/s1$", null);

        await cut.InvokeAsync(() => NotificationConnection.RaiseFichaNoCemiterioAlteradaAsync(SheetId));

        cut.WaitForAssertion(() => Raiz(cut).QuerySelectorAll(".mud-alert").Select(TextoNormalizado)
            .Should().Contain(a => a.Contains("Não foi possível atualizar a ficha.")));
    }

    private static List<string> LinksDaTrilha(IRenderedComponent<IComponent> cut) =>
        cut.FindAll(".mud-breadcrumb-item")
            .Select(li => li.QuerySelector("a") is { } a ? $"{TextoNormalizado(li)} → {a.GetAttribute("href")}" : TextoNormalizado(li))
            .ToList();

    [Theory]
    [InlineData("GM", new[] { "Painel → painel", "Campanhas → campanhas", "Crônicas de Alker → campanhas/c1?aba=cemiterio", "Aria → #" })]
    [InlineData("Jogador", new[] { "Painel → painel", "Aria → #" })]
    public async Task Breadcrumb_da_ficha_no_cemiterio(string papel, string[] esperado)
    {
        FichaNoCemiterio("Morte");

        var cut = await AbrirAsync(papel);

        LinksDaTrilha(cut).Should().Equal(esperado);
    }
}
