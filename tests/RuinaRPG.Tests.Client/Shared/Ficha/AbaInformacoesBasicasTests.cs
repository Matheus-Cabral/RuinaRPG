using System.Net;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A aba Informações Básicas compartilhada (E, Task 10) dentro das três páginas. O que se vê e as requisições ficam com a
/// rede de segurança (RedeCabecalhoEInformacoesBasicasTests); aqui fica o que ela não vê: o formulário auto-salvo da
/// página passado à aba, o autosave e a recarga adiada por um campo dentro dela (Review Focus 1), dados novos depois de
/// salvar (a rede responde sempre a mesma ficha), erros de dentro da aba no alerta da página e as mensagens de
/// validação de cada tipo.
/// </summary>
public class AbaInformacoesBasicasTests : RedeDeSegurancaBase
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComLinhagem => new() { "Personagem", "NPC" };

    private static TipoDeFichaCliente TipoDoCliente(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private static string NomeDaFicha(string tipo) => tipo switch { "Personagem" => "Aria", "NPC" => "Borin", _ => "Lobo Cinzento" };

    private static IRenderedComponent<AbaInformacoesBasicas> Aba(IRenderedComponent<IComponent> cut) => cut.FindComponent<AbaInformacoesBasicas>();

    /// <summary>O &lt;input&gt; do campo de rótulo <paramref name="rotulo"/> renderizado pela aba compartilhada (não pela página).</summary>
    private static IElement CampoNaAba(IRenderedComponent<IComponent> cut, string rotulo)
    {
        var label = Aba(cut).FindAll("label.mud-input-label").Single(l => TextoNormalizado(l) == rotulo);
        return label.Closest(".mud-input-control")!.QuerySelector("input")!;
    }

    /// <summary>A ficha de hoje (a da rede) com as mudanças de <paramref name="mudar"/>, para responder depois de salvar.</summary>
    private async Task<string> FichaMudadaAsync(string tipo, Action<JsonObject> mudar)
    {
        var http = Services.GetRequiredService<HttpClient>();
        var ficha = JsonNode.Parse(await http.GetStringAsync($"{Prefixo(tipo)}/{SheetId}"))!.AsObject();
        mudar(ficha);
        LimparRequisicoes();
        return ficha.ToJsonString();
    }

    private static readonly object CaracteristicasDepois = new
    {
        positivas = new object[]
        {
            new { id = "t1", traitId = "tr1", nome = "Corajoso", descricao = "Não foge.", custo = 2, polaridade = "Positiva", especificacao = (string?)null, requerEspecificacao = false, isRacial = false },
            new { id = "t3", traitId = "tr9", nome = "Ágil", descricao = "Rápido.", custo = 3, polaridade = "Positiva", especificacao = (string?)null, requerEspecificacao = false, isRacial = false },
        },
        totalPositivas = 5,
        negativas = Array.Empty<object>(),
        totalNegativas = 0,
        pontosDisponiveis = 5,
        pontosDisponiveisNegativas = 3,
    };

    // ---------------- a aba e o formulário da página ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_tab_is_the_shared_component_with_the_page_form_and_save_paths(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);

        cut.FindComponents<AbaInformacoesBasicas>().Should().ContainSingle();
        var aba = Aba(cut).Instance;
        aba.Estado.Should().BeSameAs(cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value);
        aba.Formulario.Tipo.Should().BeSameAs(TipoDoCliente(tipo));
        aba.Formulario.Nome.Should().Be(NomeDaFicha(tipo));
        aba.AutoSave.Should().NotBeNull();
        aba.AoMudar.HasDelegate.Should().BeTrue();
        aba.AoMudarExperiencia.HasDelegate.Should().BeTrue();
        aba.AoMudarPontosDeIgnicao.HasDelegate.Should().BeTrue();
        // Nível à mão tem endpoint próprio; o Personagem aplica XP e PI na hora.
        aba.AoMudarNivel.HasDelegate.Should().Be(TipoDoCliente(tipo).EditaNivelAManual);
        aba.SalvarAgora.HasDelegate.Should().Be(!TipoDoCliente(tipo).EditaNivelAManual);
    }

    [Theory]
    [InlineData("Personagem", 0, 1, 1, 0)]
    [InlineData("NPC", 1, 0, 1, 0)]
    [InlineData("Criatura", 1, 0, 0, 1)]
    public async Task The_kind_only_parts_render_where_the_kind_has_them(string tipo, int nivelManual, int nivelPorExperiencia, int comLinhagem, int semLinhagem)
    {
        var cut = await RenderFichaAsync(tipo);

        cut.FindComponents<NivelManualSection>().Should().HaveCount(nivelManual);
        cut.FindComponents<NivelPorExperienciaSection>().Should().HaveCount(nivelPorExperiencia);
        cut.FindComponents<GraduacaoECoracaoDeMana>().Should().HaveCount(comLinhagem);
        cut.FindComponents<AmbaresAbsorvidosSection>().Should().HaveCount(comLinhagem);
        cut.FindComponents<LinhagemVarianteFields>().Should().HaveCount(comLinhagem);
        cut.FindComponents<RacaEArquetipoFields>().Should().HaveCount(semLinhagem);
        cut.FindComponents<SeletorDeCaracteristica>().Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(TiposComLinhagem))]
    public async Task The_variant_picker_gets_the_released_variants_from_the_state(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);

        cut.FindComponent<LinhagemVarianteFields>().Instance.VariantesLiberadas.Should().BeSameAs(Aba(cut).Instance.Estado.VariantesLiberadas);
    }

    // A2: só o NPC tem a ajuda do XP por abate; a Criatura mostra os números sem ela.
    [Theory]
    [InlineData("NPC", 1)]
    [InlineData("Criatura", 0)]
    public async Task Only_the_npc_has_the_xp_por_abate_help(string tipo, int ajudas)
    {
        var cut = await RenderFichaAsync(tipo);

        cut.FindComponent<NivelManualSection>().FindComponents<InfoPopup>().Should().HaveCount(ajudas);
    }

    // ---------------- Review Focus (1): autosave e recarga adiada a partir de um campo da aba ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Edit_inside_tab_shows_auto_save_and_defers_reload(string tipo)
    {
        JSInterop.Setup<bool>("ruinaFoco.campoEmEdicao").SetResult(true);
        var cut = await RenderFichaAsync(tipo, "Jogador");
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync(notasSecretas: false);
        LimparRequisicoes();
        var nome = CampoNaAba(cut, "Nome");

        // Um campo da aba compartilhada passa pelo AutoSaveCoordinator da página: um PUT e o indicador da aba.
        await cut.InvokeAsync(() => nome.Change("Novo"));

        var p = $"/{Prefixo(tipo)}/{SheetId}";
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.FirstOrDefault().Should().Be($"PUT {p} {RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo)}");
            todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo));
        });
        cut.WaitForAssertion(() => TextoNormalizado(Aba(cut).Find(".rr-autosave-indicator")).Should().MatchRegex(@"^Salvo às \d{2}:\d{2}$"), TimeSpan.FromSeconds(5));
        LimparRequisicoes();

        // Condições mudam com um campo (o Nome, dentro da aba) em edição: o aviso relê já, a ficha espera.
        await cut.InvokeAsync(() => NotificationConnection.RaiseCondicoesAlteradasAsync(new CondicoesAlteradasNotification(TipoDoCliente(tipo).TipoDaNotificacao, SheetId)));
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"GET {p}/condicoes-ativas"));
        // Passa da conferência periódica da RecargaAdiada (1 s): o campo continua em edição, nada é relido.
        await Task.Delay(1300);
        Requisicoes.Should().Equal($"GET {p}/condicoes-ativas");

        // O foco sai do campo da aba: o focusout sobe até a raiz da página (ficha-condicoes-raiz) e a ficha relê. O campo
        // é o mesmo, achado no DOM da página (bUnit sobe pelos ancestrais do DOM de quem o achou; o da aba não tem a raiz).
        JSInterop.Setup<bool>("ruinaFoco.campoEmEdicao").SetResult(false);
        LimparRequisicoes();
        var nomeNaPagina = Campo(cut.Find(".ficha-condicoes-raiz"), "Nome");
        nomeNaPagina.Id.Should().Be(CampoNaAba(cut, "Nome").Id).And.NotBeNullOrEmpty();
        await cut.InvokeAsync(() => nomeNaPagina.TriggerEventAsync("onfocusout", new FocusEventArgs()));

        var recarga = new List<string> { $"GET {p}" };
        recarga.AddRange(RecargaDasAbas(tipo));
        recarga.AddRange(new[] { $"GET {p}/spell-abilities", "GET /campaigns/c1/available-spell-abilities" });
        if (tipo != "Criatura")
            recarga.AddRange(new[] { $"GET {p}/runes", "GET /campaigns/c1/available-runes" });
        recarga.Add($"GET {p}/artifacts");
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(recarga), TimeSpan.FromSeconds(8));
    }

    // ---------------- autosave: nada perdido, nada em dobro ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Two_quick_edits_inside_the_tab_save_once_with_both(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        LimparRequisicoes();

        await cut.InvokeAsync(() => CampoNaAba(cut, "Nome").Change("Novo"));
        await cut.InvokeAsync(() => CampoNaAba(cut, tipo == "Criatura" ? "Raça" : "Propriedade").Change("Outra"));

        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo)
            .Replace(tipo == "Criatura" ? "\"raca\":\"Lobo\"" : $"\"propriedade\":\"{(tipo == "Personagem" ? "Uma casa" : "Uma forja")}\"",
                tipo == "Criatura" ? "\"raca\":\"Outra\"" : "\"propriedade\":\"Outra\"");
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.Where(r => r.StartsWith("PUT ")).Should().Equal($"PUT /{Prefixo(tipo)}/{SheetId} {corpo}");
            todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo));
        });
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task An_edit_after_a_save_saves_again_with_the_reloaded_form(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        LimparRequisicoes();
        await cut.InvokeAsync(() => CampoNaAba(cut, "Nome").Change("Novo"));
        await EsperarRequisicoesAsync(cut, todas => todas.Count(r => r.StartsWith("PUT ")).Should().Be(1));
        LimparRequisicoes();

        // A recarga trouxe o nome do servidor (a rede responde a ficha de sempre); o segundo PUT leva o formulário relido.
        await cut.InvokeAsync(() => CampoNaAba(cut, tipo == "Criatura" ? "Raça" : "Propriedade").Change("Outra"));

        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: NomeDaFicha(tipo))
            .Replace(tipo == "Criatura" ? "\"raca\":\"Lobo\"" : $"\"propriedade\":\"{(tipo == "Personagem" ? "Uma casa" : "Uma forja")}\"",
                tipo == "Criatura" ? "\"raca\":\"Outra\"" : "\"propriedade\":\"Outra\"");
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.FirstOrDefault().Should().Be($"PUT /{Prefixo(tipo)}/{SheetId} {corpo}");
            todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo));
        });
    }

    // ---------------- dados novos depois de salvar (a rede não vê: responde sempre a mesma ficha) ----------------

    [Theory]
    [InlineData("Personagem", new[] { "Nível: 4", "Círculo: 3", "Faltam 5 XP para o próximo nível", "VIS Atual: 9" })]
    [InlineData("NPC", new[] { "«5»", "Grau: 3", "Abate: 60", "Assistência: 30", "VIS Atual: 9" })]
    [InlineData("Criatura", new[] { "«3»", "Abate: 45", "Assistência: 22" })]
    public async Task After_a_save_the_tab_shows_the_reloaded_sheet(string tipo, string[] esperado)
    {
        var cut = await RenderFichaAsync(tipo);
        var ficha = await FichaMudadaAsync(tipo, f =>
        {
            f["nivel"] = f["nivel"]!.GetValue<int>() + 1;
            f["xpParaProximoNivel"] = 5;
            f["eapAtual"] = 9;
            f["graduacao"] = 3;
            f["abate"] = tipo == "NPC" ? 60 : 45;
            f["assistencia"] = tipo == "NPC" ? 30 : 22;
        });
        Responder($"^/{Prefixo(tipo)}/{SheetId}$", ficha);
        Responder($"^/{Prefixo(tipo)}/{SheetId}/traits$", CaracteristicasDepois);

        await cut.InvokeAsync(() => CampoNaAba(cut, "Nome").Change("Novo"));

        cut.WaitForAssertion(() =>
        {
            var leitura = Leitura(AbaAberta(cut));
            leitura.Should().ContainInOrder(esperado);
            leitura.Should().Contain("Ágil (3) — Rápido.").And.Contain("Negativas (Total: 0)").And.NotContain("Teimoso (-1) — Não cede. (com nobres)");
        }, TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
    }

    [Theory]
    [InlineData("GM")]
    [InlineData("Jogador")]
    public async Task Applying_xp_clears_the_field_and_shows_the_reloaded_experience(string papel)
    {
        var cut = await RenderFichaAsync("Personagem", papel);
        Responder($"^/character-sheets/{SheetId}$", await FichaMudadaAsync("Personagem", f => f["experienciaAtual"] = 135));

        await cut.InvokeAsync(() => CampoNaAba(cut, "Adicionar/subtrair XP").Change("15"));
        await cut.InvokeAsync(() => Aba(cut).FindAll("button").First(b => TextoNormalizado(b) == "Aplicar").Click());

        cut.WaitForAssertion(() =>
        {
            CampoNaAba(cut, "Experiência Atual").GetAttribute("value").Should().Be("135");
            CampoNaAba(cut, "Adicionar/subtrair XP").GetAttribute("value").Should().BeNullOrEmpty();
        }, TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
    }

    // Os números digitados e ainda não aplicados sobrevivem à troca de aba (antes do E eram campos da página; as abas do
    // MudTabs não ficam vivas, então moram no estado).
    [Fact]
    public async Task Typed_xp_and_pi_deltas_survive_a_tab_switch()
    {
        var cut = await RenderFichaAsync("Personagem");
        await cut.InvokeAsync(() => CampoNaAba(cut, "Adicionar/subtrair XP").Change("15"));
        await cut.InvokeAsync(() => CampoNaAba(cut, "Adicionar/subtrair PI (bônus manual)").Change("4"));
        LimparRequisicoes();

        await IrParaAbaAsync(cut, "Combate");
        await IrParaAbaAsync(cut, "Informações Básicas");

        cut.WaitForAssertion(() =>
        {
            CampoNaAba(cut, "Adicionar/subtrair XP").GetAttribute("value").Should().Be("15");
            CampoNaAba(cut, "Adicionar/subtrair PI (bônus manual)").GetAttribute("value").Should().Be("4");
        }, LimiteDeEspera);
        await EsperarCalmariaAsync();
        Requisicoes.Should().NotContain(r => !r.StartsWith("GET ", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adding_a_caracteristica_shows_the_reloaded_list(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        Responder($"^/{Prefixo(tipo)}/{SheetId}/traits$", CaracteristicasDepois);
        await EscolherNoSeletorAsync(cut, 0, "tr9", "Ágil (3, Positiva)");
        LimparRequisicoes();

        await cut.InvokeAsync(() => Aba(cut).FindAll("button").Single(b => TextoNormalizado(b) == "Adicionar Característica").Click());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
            $"POST /{Prefixo(tipo)}/{SheetId}/traits {{\"especificacao\":null,\"traitId\":\"tr9\"}}", $"GET /{Prefixo(tipo)}/{SheetId}/traits"));
        cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Contain("Ágil (3) — Rápido.").And.Contain("Positivas (Total: 5)"), TimeSpan.FromSeconds(5));
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Removing_a_caracteristica_shows_the_reloaded_list(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        Responder($"^/{Prefixo(tipo)}/{SheetId}/traits$", CaracteristicasDepois);
        LimparRequisicoes();

        var teimoso = Aba(cut).FindAll("tr").Single(tr => TextoNormalizado(tr).StartsWith("Teimoso", StringComparison.Ordinal));
        await cut.InvokeAsync(() => teimoso.QuerySelector("button")!.Click());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"DELETE /{Prefixo(tipo)}/{SheetId}/traits/t2", $"GET /{Prefixo(tipo)}/{SheetId}/traits"));
        cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Contain("Negativas (Total: 0)").And.NotContain("Teimoso (-1) — Não cede. (com nobres)"), TimeSpan.FromSeconds(5));
    }

    // R-E3: a Habilidade Racial mora no estado; rolar a Arca salva pelo formulário da página e mostra a habilidade relida.
    [Theory]
    [MemberData(nameof(TiposComLinhagem))]
    public async Task Rolling_the_arca_shows_the_reloaded_racial_ability(string tipo)
    {
        // Só o Humano rola a Arca: a ficha passa a ser Humano (o NPC da rede não é) e a página a relê depois de salvar.
        var cut = await RenderFichaAsync(tipo);
        Responder($"^/{Prefixo(tipo)}/{SheetId}$", await FichaMudadaAsync(tipo, f => f["linhagem"] = "Humano"));
        await cut.InvokeAsync(() => CampoNaAba(cut, "Nome").Change("Novo"));
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Contain(r => r.EndsWith("/racial-ability")));
        await IrParaAbaAsync(cut, "Magias & Habilidades");
        await EsperarCalmariaAsync();
        Responder("/racial-ability", new { nome = "Adaptável", descricao = "Aprende rápido.", arcaRolada = 7, arcaNome = "Arca do Vento", arcaDescricao = "Sopra.", arcaEvolucoes = Array.Empty<object>(), arcaDado = 20 });
        LimparRequisicoes();

        var arca = cut.FindComponent<HabilidadeRacialSection>().Find("input");
        await cut.InvokeAsync(() => arca.Change("7"));

        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.FirstOrDefault().Should().StartWith($"PUT /{Prefixo(tipo)}/{SheetId} ").And.Contain("\"arcaRolada\":7");
            todas.Last().Should().Be($"GET /{Prefixo(tipo)}/{SheetId}/racial-ability");
            todas.Count(r => r.EndsWith("/racial-ability")).Should().Be(2);
        });
        cut.WaitForAssertion(() => cut.FindComponent<HabilidadeRacialSection>().Markup.Should().Contain("Arca do Vento").And.Contain("Sopra."), TimeSpan.FromSeconds(5));
        cut.FindComponent<HabilidadeRacialSection>().Instance.Estado.HabilidadeRacial!.ArcaNome.Should().Be("Arca do Vento");
    }

    // ---------------- erros de dentro da aba no alerta da página ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_refused_caracteristica_shows_in_the_page_alert(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        ResponderErro($"^/{Prefixo(tipo)}/{SheetId}/traits$", HttpStatusCode.BadRequest, "Essa característica exige especificação.");
        await EscolherNoSeletorAsync(cut, 0, "tr9", "Ágil (3, Positiva)");

        await cut.InvokeAsync(() => Aba(cut).FindAll("button").Single(b => TextoNormalizado(b) == "Adicionar Característica").Click());

        cut.WaitForAssertion(() => cut.Find(".mud-alert").TextContent.Should().Contain("Essa característica exige especificação."), TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_refused_upload_shows_in_the_page_alert(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        ResponderErro("^/images$", HttpStatusCode.BadRequest, "grande demais");
        await cut.InvokeAsync(() => Aba(cut).Find(".mud-avatar").Click());
        var editar = cut.WaitForElement("button[title='Editar imagem']");
        await cut.InvokeAsync(() => editar.Click());
        cut.WaitForState(() => cut.FindComponents<InputFile>().Count > 0);
        LimparRequisicoes();

        await cut.InvokeAsync(() => cut.FindComponents<InputFile>().Single().UploadFiles(InputFileContent.CreateFromText("conteudo", "foto.png")));

        cut.WaitForAssertion(() => cut.Find(".mud-alert").TextContent.Should().Contain("Não foi possível enviar a imagem."), TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
        Requisicoes.Should().ContainSingle().Which.Should().StartWith("POST /images");
    }

    // ---------------- validação de cada tipo ----------------

    // O formulário de cada tipo tem as mensagens dele; a do PI segue o rótulo do campo.
    [Theory]
    [InlineData("Personagem", "Pontos de Ignição Atual", "Informe os Pontos de Ignição Atual.")]
    [InlineData("NPC", "Pontos de Ignição Atual", "Informe os Pontos de Ignição Atual.")]
    [InlineData("Criatura", "Pontos de Ignição", "Informe os Pontos de Ignição.")]
    [InlineData("Personagem", "Experiência Atual", "Informe a Experiência Atual.")]
    [InlineData("NPC", "Nível", "Informe o Nível.")]
    [InlineData("Criatura", "Nível", "Informe o Nível.")]
    [InlineData("Personagem", "Rank F", "Informe os Núcleos de Rank F.")]
    public async Task Clearing_a_required_field_shows_the_message_of_its_kind(string tipo, string rotulo, string mensagem)
    {
        var cut = await RenderFichaAsync(tipo);
        LimparRequisicoes();

        await cut.InvokeAsync(() => Campo(AbaAberta(cut), rotulo).Change(""));

        cut.WaitForAssertion(() => TextoNormalizado(AbaAberta(cut)).Should().Contain(mensagem), TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
    }
}
