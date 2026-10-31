using System.Net;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A aba Atributos &amp; Perícias compartilhada (E, Task 11) dentro das três páginas. O que se vê e as requisições de cada
/// edição ficam com a rede de segurança (RedeAtributosEPericiasTests); aqui fica o que ela não vê: dados novos depois de
/// salvar (a rede responde sempre as mesmas linhas), a recarga que a página faz (Condições) chegando à aba, o Acerto
/// Crítico e a Afinidade Adicional (do formulário auto-salvo da página) e os erros de dentro da aba no alerta da página.
/// </summary>
public class AbaAtributosEPericiasTests : RedeDeSegurancaBase
{
    private const string NomeDaAba = "Atributos & Perícias";

    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComAfinidades => new() { "Personagem", "NPC" };

    private async Task<IRenderedComponent<IComponent>> AbrirAsync(string tipo, string papel = "GM")
    {
        var cut = await RenderFichaAsync(tipo, papel);
        await IrParaAbaAsync(cut, NomeDaAba);
        await EsperarCalmariaAsync();
        LimparRequisicoes();
        return cut;
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

    private static IElement LinhaDoVigor(IRenderedComponent<IComponent> cut) => Linha(Tabela(AbaAberta(cut), "Atributo"), "Vigor");

    private static IElement LinhaDoAtletismo(IRenderedComponent<IComponent> cut) => Linha(Tabela(AbaAberta(cut), "Perícia"), "Atletismo");

    private static IElement? BotaoDoElemento(IRenderedComponent<IComponent> cut, string rotulo) =>
        AbaAberta(cut).QuerySelector($"button[aria-label='{rotulo}']");

    private static IElement LinhaDoElemento(IRenderedComponent<IComponent> cut, string nome) =>
        BotaoDoElemento(cut, $"Pôr 1 ponto em {nome}")!.Closest("tr")!;

    /// <summary>A barra de segmentos de um elemento: o rótulo ("2 de 6 pontos") e quantos segmentos estão cheios.</summary>
    private static (string? Rotulo, int Segmentos, int Cheios) Barra(IRenderedComponent<IComponent> cut, string nome)
    {
        var barra = LinhaDoElemento(cut, nome).QuerySelector("div[role=img]")!;
        return (barra.GetAttribute("aria-label"), barra.Children.Length, barra.Children.Count(c => c.ClassList.Contains("mud-theme-primary")));
    }

    private static string UltimaCelula(IElement linha) => TextoNormalizado(linha.QuerySelectorAll("td").Last());

    /// <summary>A recarga das abas e, quando a gravação sobe a versão das penalidades, o alerta delas.</summary>
    private async Task EsperarGravacaoAsync(IRenderedComponent<IComponent> cut, string tipo, string gravacao, bool recarregaPenalidades)
    {
        var esperado = new List<string> { gravacao };
        esperado.AddRange(RecargaDasAbas(tipo));
        if (recarregaPenalidades)
            esperado.Add($"GET /{Prefixo(tipo)}/{SheetId}/equipment-penalties");
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(esperado));
    }

    private static object Elemento(int id, string nome, int pontos, int? experiencia = null, bool bloqueado = false, string? motivo = null,
        int? teto = null, int? comCondicoes = null) => new
    {
        id, nome, tipo = "Base", teto, pontos, experiencia, bloqueado, motivo, pontosComCondicoes = comCondicoes ?? pontos,
    };

    private static object Afinidades(int gasto, int maximo, params object[] elementos) => Afinidades(gasto, maximo, true, elementos);

    private static object Afinidades(int gasto, int maximo, bool secaoVisivel, params object[] elementos) =>
        new { gasto, maximo, secaoVisivel, elementos };

    private static object Pericia(int gasto, string? atributo, int total) => new
    {
        pericia = "Atletismo", gasto, modificador = 1, atributoEscolhido = atributo, total, contaHistorico = true,
        nome = "Atletismo", descricao = "Correr e saltar.",
    };

    // ---------------- a aba, o estado e o formulário da página ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_tab_is_the_shared_component_with_the_page_state_and_form(string tipo)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponents<AbaAtributosEPericias>().Should().ContainSingle();
        var aba = cut.FindComponent<AbaAtributosEPericias>().Instance;
        aba.Estado.Should().BeSameAs(cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value);
        // O formulário é o auto-salvo da página (o _form dela), já carregado com a ficha.
        var pagina = cut.FindComponents<IComponent>().Select(c => c.Instance)
            .Single(c => c.GetType().Name == tipo switch { "Personagem" => "FichaDePersonagem", "NPC" => "FichaDeNpc", _ => "FichaDeCriatura" });
        var formDaPagina = pagina.GetType().GetField("_form", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(pagina);
        aba.Formulario.Should().NotBeNull().And.BeSameAs(formDaPagina);
        aba.Formulario.Nome.Should().Be(tipo switch { "Personagem" => "Aria", "NPC" => "Borin", _ => "Lobo Cinzento" });
        aba.AoMudar.HasDelegate.Should().Be(tipo != "Criatura");
        aba.SalvarERecarregar.HasDelegate.Should().Be(tipo == "Personagem");
    }

    [Theory]
    [InlineData("Personagem", 1, 1, 1)]
    [InlineData("NPC", 1, 0, 1)]
    [InlineData("Criatura", 0, 0, 0)]
    public async Task The_parts_follow_the_kind(string tipo, int progressao, int orcamento, int afinidades)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponents<ProgressaoDoNivelSection>().Should().HaveCount(progressao);
        cut.FindComponents<OrcamentoDePericiasSection>().Should().HaveCount(orcamento);
        cut.FindComponents<AfinidadesPorElementoSection>().Should().HaveCount(afinidades);
    }

    // A margem de cima da tabela de Perícias vem com o bloco do Acerto Crítico logo acima dela (só onde há orçamento).
    [Theory]
    [InlineData("Personagem", true)]
    [InlineData("NPC", false)]
    [InlineData("Criatura", false)]
    public async Task The_skill_table_has_a_top_margin_only_below_the_acerto_critico(string tipo, bool margem)
    {
        var cut = await AbrirAsync(tipo);

        Tabela(AbaAberta(cut), "Perícia").Closest(".mud-table")!.ClassList.Contains("mt-3").Should().Be(margem);
    }

    // ---------------- dados novos depois de salvar (a rede não vê: responde sempre as mesmas linhas) ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Editing_an_attribute_shows_the_reloaded_attributes_and_skills(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/attributes", new[] { new { atributo = "Vigor", gasto = 4, bonus = 1, temMaestria = false, total = 7 } });
        Responder("/attributes/budget", new { gastoTotal = 4, pontosDisponiveis = 10 });
        Responder("/skills", new[] { Pericia(2, "Vigor", 9) });

        await cut.InvokeAsync(() => LinhaDoVigor(cut).QuerySelectorAll("input")[0].Change("4"));

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/attributes/Vigor {{\"bonus\":1,\"gasto\":4,\"temMaestria\":false}}", recarregaPenalidades: true);
        cut.WaitForAssertion(() =>
        {
            Leitura(AbaAberta(cut)).Should().Contain("Pontos de Atributo: 4 / 10");
            UltimaCelula(LinhaDoVigor(cut)).Should().Be("7");
            LinhaDoVigor(cut).QuerySelectorAll("input")[0].GetAttribute("value").Should().Be("4");
            UltimaCelula(LinhaDoAtletismo(cut)).Should().Be("9");
        }, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("Personagem", "Pontos de Perícia: 5 / 8")]
    [InlineData("NPC", "Pontos de Perícia Gastos: 5")]
    [InlineData("Criatura", "Pontos de Perícia Gastos: 5")]
    public async Task Editing_a_skill_shows_the_reloaded_skill_and_points(string tipo, string pontos)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/skills", new[] { Pericia(5, "Vigor", 6) });
        Responder("/skills/budget", new { gastoTotal = 5, pontosDisponiveis = 8 });

        await cut.InvokeAsync(() => LinhaDoAtletismo(cut).QuerySelectorAll("input")[0].Change("5"));

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/skills/Atletismo {{\"atributoEscolhido\":\"Vigor\",\"gasto\":5}}", recarregaPenalidades: true);
        cut.WaitForAssertion(() =>
        {
            Leitura(AbaAberta(cut)).Should().Contain(pontos);
            UltimaCelula(LinhaDoAtletismo(cut)).Should().Be("6");
        }, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Clearing_the_skill_attribute_shows_the_reloaded_skill(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/skills", new[] { Pericia(2, null, 2) });

        await cut.InvokeAsync(() => LinhaDoAtletismo(cut).QuerySelectorAll("td")[3].QuerySelector(".mud-input-control")!.MouseDown());
        var vazio = cut.WaitForElements(".mud-popover-open .mud-list-item").Single(li => TextoNormalizado(li) == "—");
        await cut.InvokeAsync(() => vazio.Click());

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/skills/Atletismo {{\"atributoEscolhido\":null,\"gasto\":2}}", recarregaPenalidades: true);
        cut.WaitForAssertion(() =>
        {
            UltimaCelula(LinhaDoAtletismo(cut)).Should().Be("2");
            LinhaDoAtletismo(cut).QuerySelectorAll("td")[3].QuerySelector("input")!.GetAttribute("value").Should().BeNullOrEmpty();
        }, TimeSpan.FromSeconds(5));
    }

    // ---------------- Afinidades: pontos por elemento (2.c) ----------------

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task The_elements_show_name_points_bar_and_experience(string tipo)
    {
        Responder("/afinidades", Afinidades(5, 8, Elemento(2, "Fogo", 2, experiencia: 1), Elemento(4, "Gelo", 3, teto: 4)));
        var cut = await AbrirAsync(tipo);

        var leitura = Leitura(AbaAberta(cut));
        leitura.Should().ContainInOrder("Distribuídos 5/8", "Fogo", "2", "«1»", "Gelo", "3", "«»");
        // Sem teto, os segmentos vão até o máximo da ficha; com teto, até ele.
        Barra(cut, "Fogo").Should().Be(("2 de 8 pontos", 8, 2));
        Barra(cut, "Gelo").Should().Be(("3 de 4 pontos", 4, 3));
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task A_plus_puts_one_more_point_and_shows_the_reloaded_list(string tipo)
    {
        Responder("/afinidades", Afinidades(2, 6, Elemento(2, "Fogo", 2, experiencia: 1)));
        var cut = await AbrirAsync(tipo);
        Responder("/afinidades", Afinidades(3, 6, Elemento(2, "Fogo", 3, experiencia: 1)));

        await cut.InvokeAsync(() => BotaoDoElemento(cut, "Pôr 1 ponto em Fogo")!.Click());

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/afinidades/2 {{\"experiencia\":1,\"pontos\":3}}", recarregaPenalidades: false);
        cut.WaitForAssertion(() =>
        {
            Leitura(AbaAberta(cut)).Should().Contain("Distribuídos 3/6");
            Barra(cut, "Fogo").Should().Be(("3 de 6 pontos", 6, 3));
        }, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task A_minus_takes_one_point_and_shows_the_reloaded_list(string tipo)
    {
        Responder("/afinidades", Afinidades(2, 6, Elemento(2, "Fogo", 2)));
        var cut = await AbrirAsync(tipo);
        Responder("/afinidades", Afinidades(1, 6, Elemento(2, "Fogo", 1)));

        await cut.InvokeAsync(() => BotaoDoElemento(cut, "Tirar 1 ponto de Fogo")!.Click());

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/afinidades/2 {{\"experiencia\":null,\"pontos\":1}}", recarregaPenalidades: false);
        cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Contain("Distribuídos 1/6"), TimeSpan.FromSeconds(5));
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task A_refused_point_shows_the_server_message_in_the_page_alert(string tipo)
    {
        Responder("/afinidades", Afinidades(3, 6, Elemento(3, "Ar", 1), Elemento(4, "Gelo", 2)));
        ResponderErro("/afinidades/3", HttpStatusCode.BadRequest, "Gelo depende de Ar: zere Gelo antes.");
        var cut = await AbrirAsync(tipo);

        await cut.InvokeAsync(() => BotaoDoElemento(cut, "Tirar 1 ponto de Ar")!.Click());

        cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Select(TextoNormalizado).Should().Equal("Gelo depende de Ar: zere Gelo antes."), TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
    }

    // Bloqueado: a dica do servidor aparece e o + some para quem as regras recusam; o GM do NPC vê a dica mas pode subir.
    [Theory]
    [InlineData("Personagem", "GM", false)]
    [InlineData("Personagem", "Jogador", false)]
    [InlineData("NPC", "Jogador", false)]
    [InlineData("NPC", "GM", true)]
    public async Task A_blocked_element_shows_the_hint_and_no_plus(string tipo, string papel, bool podeSubir)
    {
        Responder("/afinidades", Afinidades(0, 6, Elemento(4, "Gelo", 0, bloqueado: true, motivo: "Precisa de 1 em Água e 1 em Ar")));
        var cut = await AbrirAsync(tipo, papel);

        TextoNormalizado(LinhaDoElemento(cut, "Gelo")).Should().Contain("Precisa de 1 em Água e 1 em Ar");
        BotaoDoElemento(cut, "Pôr 1 ponto em Gelo")!.HasAttribute("disabled").Should().Be(!podeSubir);
    }

    // No máximo (ou no teto), o + não sobe mais; acima do máximo o contador fica vermelho e a mensagem diz que só dá para tirar.
    [Theory]
    [InlineData("Personagem", "GM", 6, 6, null, false, "Todos os pontos distribuídos.")]
    [InlineData("NPC", "Jogador", 7, 6, null, false, "Acima do máximo em 1: só é possível tirar pontos.")]
    [InlineData("Personagem", "Jogador", 2, 6, 2, false, "Restam 4 ponto(s) para distribuir.")]
    [InlineData("NPC", "GM", 7, 6, null, true, "Acima do máximo em 1: só é possível tirar pontos.")]
    public async Task The_plus_stops_at_the_maximum_and_at_the_ceiling(string tipo, string papel, int gasto, int maximo, int? teto, bool podeSubir, string mensagem)
    {
        Responder("/afinidades", Afinidades(gasto, maximo, Elemento(2, "Fogo", 2, teto: teto)));
        var cut = await AbrirAsync(tipo, papel);

        BotaoDoElemento(cut, "Pôr 1 ponto em Fogo")!.HasAttribute("disabled").Should().Be(!podeSubir);
        Leitura(AbaAberta(cut)).Should().Contain(mensagem);
        var contador = AbaAberta(cut).QuerySelectorAll(".mud-typography").Single(t => TextoNormalizado(t) == $"Distribuídos {gasto}/{maximo}");
        contador.ClassList.Contains("mud-error-text").Should().Be(gasto > maximo);
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Editing_the_experience_puts_it_with_the_points(string tipo)
    {
        Responder("/afinidades", Afinidades(2, 6, Elemento(2, "Fogo", 2, experiencia: 1)));
        var cut = await AbrirAsync(tipo);

        await cut.InvokeAsync(() => LinhaDoElemento(cut, "Fogo").QuerySelector("input")!.Change(""));

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/afinidades/2 {{\"experiencia\":null,\"pontos\":2}}", recarregaPenalidades: false);
    }

    // Review Focus 5: vocação marcial sem Afinidade Adicional nem coração de mana — a lista e o contador não aparecem;
    // só fica a Afinidade Adicional, que é como a seção volta.
    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task A_hidden_section_shows_only_the_afinidade_adicional(string tipo)
    {
        Responder("/afinidades", Afinidades(0, 0, false, Elemento(2, "Fogo", 0)));
        var cut = await AbrirAsync(tipo);

        var leitura = Leitura(AbaAberta(cut));
        leitura.Should().Contain("Afinidade Adicional");
        leitura.Should().NotContain(t => t.StartsWith("Distribuídos", StringComparison.Ordinal)).And.NotContain("Fogo");
        BotaoDoElemento(cut, "Pôr 1 ponto em Fogo").Should().BeNull();
    }

    // ---------------- a recarga que a página faz chega à aba ----------------

    // A página relê as abas 2 e 3 por conta própria (aqui, o aviso de Condições alteradas); a aba aberta mostra o novo.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_page_reload_shows_fresh_attributes_in_the_tab(string tipo)
    {
        var cut = await AbrirAsync(tipo, "Jogador");
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync(notasSecretas: false);
        Responder("/attributes", new[] { new { atributo = "Vigor", gasto = 2, bonus = 1, temMaestria = false, total = 1 } });
        Responder("/attributes/budget", new { gastoTotal = 2, pontosDisponiveis = 12 });
        var k = tipo switch { "Personagem" => "character", "NPC" => "npc", _ => "creature" };

        await cut.InvokeAsync(() => NotificationConnection.RaiseCondicoesAlteradasAsync(new CondicoesAlteradasNotification(k, SheetId)));

        cut.WaitForAssertion(() =>
        {
            Leitura(AbaAberta(cut)).Should().Contain("Pontos de Atributo: 2 / 12");
            UltimaCelula(LinhaDoVigor(cut)).Should().Be("1");
        }, TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
    }

    // ---------------- o formulário auto-salvo da página dentro da aba ----------------

    // Personagem: o "Aplicar" do Acerto Crítico soma ao formulário, salva na hora (fora do autosave) e relê as abas;
    // o campo esvazia e a aba mostra os pontos novos.
    [Theory]
    [InlineData("GM")]
    [InlineData("Jogador")]
    public async Task Applying_acerto_critico_saves_reloads_and_shows_the_new_points(string papel)
    {
        var cut = await AbrirAsync("Personagem", papel);
        var ficha = await FichaMudadaAsync("Personagem", f => f["pontosDePericiaBonusCritico"] = 4);
        Responder("^/character-sheets/s1$", ficha);
        Responder("/skills/budget", new { gastoTotal = 2, pontosDisponiveis = 12 });
        var delta = Campo(AbaAberta(cut), "Adicionar/subtrair pontos de Acerto Crítico");
        await cut.InvokeAsync(() => delta.Change("3"));
        LimparRequisicoes();

        await cut.InvokeAsync(() => Botao(AbaAberta(cut), "Aplicar").Click());

        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha("Personagem", nome: "Aria").Replace("\"pontosDePericiaBonusCritico\":1", "\"pontosDePericiaBonusCritico\":4");
        // O PUT, a recarga do salvamento (em paralelo), a recarga das abas em sequência e, só no fim (a página
        // re-renderiza quando tudo acabou), o alerta das penalidades desta aba.
        var depoisDeSalvar = RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar("Personagem");
        var recarga = RecargaDasAbas("Personagem").Append("GET /character-sheets/s1/equipment-penalties").ToArray();
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.Should().HaveCount(1 + depoisDeSalvar.Length + recarga.Length);
            todas.First().Should().Be($"PUT /character-sheets/s1 {corpo}");
            todas.Skip(1).Take(depoisDeSalvar.Length).Should().BeEquivalentTo(depoisDeSalvar);
            todas.TakeLast(recarga.Length).Should().Equal(recarga);
        });
        cut.WaitForAssertion(() =>
        {
            var leitura = Leitura(AbaAberta(cut));
            leitura.Should().Contain("Pontos ganhos por Acerto Crítico: 4").And.Contain("Pontos de Perícia: 2 / 12");
            Campo(AbaAberta(cut), "Adicionar/subtrair pontos de Acerto Crítico").GetAttribute("value").Should().BeNullOrEmpty();
        }, TimeSpan.FromSeconds(5));
    }

    // O número digitado e ainda não aplicado sobrevive à troca de aba, como quando era um campo da página: a aba
    // inativa sai da árvore, então o número mora no estado.
    [Fact]
    public async Task A_typed_acerto_critico_survives_a_tab_switch()
    {
        var cut = await AbrirAsync("Personagem");
        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Adicionar/subtrair pontos de Acerto Crítico").Change("3"));

        await IrParaAbaAsync(cut, "Combate");
        await IrParaAbaAsync(cut, NomeDaAba);

        cut.WaitForAssertion(() => Campo(AbaAberta(cut), "Adicionar/subtrair pontos de Acerto Crítico").GetAttribute("value").Should().Be("3"), TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
        Requisicoes.Should().NotContain(r => !r.StartsWith("GET ", StringComparison.Ordinal));
    }

    // Personagem e NPC: a Afinidade Adicional é do formulário auto-salvo; o salvamento relê as abas (o máximo das Afinidades).
    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Changing_afinidade_adicional_autosaves_and_shows_the_reloaded_maximum(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        var ficha = await FichaMudadaAsync(tipo, f => f["afinidadeAdicional"] = 2);
        Responder($"^/{Prefixo(tipo)}/s1$", ficha);
        Responder("/afinidades", Afinidades(2, 9, Elemento(2, "Fogo", 2)));

        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Afinidade Adicional").Change("2"));

        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: tipo == "NPC" ? "Borin" : "Aria").Replace("\"afinidadeAdicional\":0", "\"afinidadeAdicional\":2");
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.First().Should().Be($"PUT /{Prefixo(tipo)}/s1 {corpo}");
            // O alerta das penalidades desta aba relê depois do salvamento, como hoje.
            todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo)
                .Append($"GET /{Prefixo(tipo)}/s1/equipment-penalties"));
        });
        cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Contain("Distribuídos 2/9"), TimeSpan.FromSeconds(5));
    }
}
