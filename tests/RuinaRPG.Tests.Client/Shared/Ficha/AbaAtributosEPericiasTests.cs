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

    private static List<IElement> LinhasDasAfinidades(IRenderedComponent<IComponent> cut) =>
        Tabela(AbaAberta(cut), "Essência Básica 1").QuerySelectorAll("tbody tr").ToList();

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

    private static object Afinidade(string id, string elemento, int valor, int experiencia) => new
    {
        id, elemento, elementoValor = valor, segundaEssencia = (string?)null, segundaEssenciaValor = (int?)null,
        subElemento = (string?)null, subElementoValor = (int?)null, experiencia, elementoValorComCondicoes = valor,
    };

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
        cut.FindComponents<AfinidadesSection>().Should().HaveCount(afinidades);
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

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Adding_an_afinidade_shows_the_reloaded_list_and_clears_the_new_row(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        await cut.InvokeAsync(() => LinhasDasAfinidades(cut).Last().QuerySelectorAll("td")[3].QuerySelector("input")!.Change("2"));
        Responder("/affinities", new[] { Afinidade("afi1", "Fogo", 2, 1), Afinidade("afi2", "Terra", 3, 2) });
        LimparRequisicoes();

        await cut.InvokeAsync(() => Botao(LinhasDasAfinidades(cut).Last(), "Adicionar").Click());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Contain(r => r.StartsWith("GET ") && r.EndsWith("/affinities")));
        cut.WaitForAssertion(() =>
        {
            LinhasDasAfinidades(cut).Should().HaveCount(3);
            LinhasDasAfinidades(cut)[1].QuerySelector("input")!.GetAttribute("value").Should().Be("Terra");
            LinhasDasAfinidades(cut).Last().QuerySelectorAll("td")[3].QuerySelector("input")!.GetAttribute("value").Should().BeNullOrEmpty();
        }, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Removing_an_afinidade_shows_the_reloaded_list(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/affinities", Array.Empty<object>());

        await cut.InvokeAsync(() => Botao(LinhasDasAfinidades(cut)[0], "Remover").Click());

        await EsperarGravacaoAsync(cut, tipo, $"DELETE /{Prefixo(tipo)}/s1/affinities/afi1", recarregaPenalidades: false);
        cut.WaitForAssertion(() => LinhasDasAfinidades(cut).Should().ContainSingle(), TimeSpan.FromSeconds(5));
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Editing_an_afinidade_shows_the_reloaded_row(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/affinities", new[] { Afinidade("afi1", "Fogo", 5, 4) });

        await cut.InvokeAsync(() => LinhasDasAfinidades(cut)[0].QuerySelectorAll("td")[3].QuerySelector("input")!.Change("4"));

        await EsperarGravacaoAsync(cut, tipo,
            $"PUT /{Prefixo(tipo)}/s1/affinities/afi1 {{\"elemento\":\"Fogo\",\"elementoValor\":2,\"experiencia\":4,\"segundaEssencia\":null,\"segundaEssenciaValor\":null,\"subElementoValor\":null}}",
            recarregaPenalidades: false);
        cut.WaitForAssertion(() =>
        {
            var celulas = LinhasDasAfinidades(cut)[0].QuerySelectorAll("input").Select(i => i.GetAttribute("value")).ToList();
            celulas.Should().Contain("5").And.Contain("4");
        }, TimeSpan.FromSeconds(5));
    }

    // ---------------- erros de dentro da aba no alerta da página ----------------

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task A_refused_afinidade_shows_in_the_page_alert_and_reloads_nothing(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        ResponderErro("/affinities", HttpStatusCode.BadRequest, "Essência inválida.");

        await cut.InvokeAsync(() => Botao(LinhasDasAfinidades(cut).Last(), "Adicionar").Click());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().ContainSingle().Which.Should().StartWith($"POST /{Prefixo(tipo)}/s1/affinities "));
        cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Select(TextoNormalizado).Should().Equal("Essência inválida."), TimeSpan.FromSeconds(5));
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task A_refused_afinidade_edit_shows_in_the_page_alert(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        ResponderErro("/affinities/afi1", HttpStatusCode.BadRequest, "Valor acima do máximo.");

        await cut.InvokeAsync(() => LinhasDasAfinidades(cut)[0].QuerySelectorAll("td")[3].QuerySelector("input")!.Change("40"));

        cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Select(TextoNormalizado).Should().Equal("Valor acima do máximo."), TimeSpan.FromSeconds(5));
        await EsperarCalmariaAsync();
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

    // Personagem e NPC: a Afinidade Adicional é do formulário auto-salvo; o salvamento relê a ficha (a Vocação Arcana máxima).
    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Changing_afinidade_adicional_autosaves_and_shows_the_reloaded_vocacao_arcana(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        var ficha = await FichaMudadaAsync(tipo, f =>
        {
            f["afinidadeAdicional"] = 2;
            f["vocacaoArcanaMaxima"] = 9;
        });
        Responder($"^/{Prefixo(tipo)}/s1$", ficha);

        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Afinidade Adicional").Change("2"));

        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: tipo == "NPC" ? "Borin" : "Aria").Replace("\"afinidadeAdicional\":0", "\"afinidadeAdicional\":2");
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.First().Should().Be($"PUT /{Prefixo(tipo)}/s1 {corpo}");
            // O alerta das penalidades desta aba relê depois do salvamento, como hoje.
            todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo)
                .Append($"GET /{Prefixo(tipo)}/s1/equipment-penalties"));
        });
        cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Contain("Vocação Arcana: 2 / 9"), TimeSpan.FromSeconds(5));
    }
}
