using System.Net;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A aba Combate compartilhada (E, Task 12) dentro das três páginas. O que se vê e as requisições das ações de
/// equipamento e de Maestria ficam com a rede de segurança (RedeCombateTests, RedePartesPorTipoTests.Maestrias); aqui
/// fica o que ela não vê: dados novos depois de cada gravação (a rede responde sempre as mesmas linhas), os campos do
/// formulário auto-salvo da página dentro da aba (Sina, recursos, Cobertura, Afinidade), a arma da Criatura sem
/// Durabilidade (R-E2) e os erros de dentro da aba no alerta da página.
/// </summary>
public class AbaCombateTests : RedeDeSegurancaBase
{
    private const string NomeDaAba = "Combate";

    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComSina => new() { "Personagem", "NPC" };

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

    private static object Arma(string id, string nome, bool equipada = true, int durabilidade = 8) => new
    {
        id, itemId = "i-" + id, nome, tipoDeDano = "Cortante", alcance = 1, dados = "1d8", dano = 3, critico = "19-20", rank = "C",
        peso = 1.5m, isEquipped = equipada, durabilidadeAtual = durabilidade, durabilidadeMaxima = 10, durabilidadeMaximo = 10, descricao = "Uma arma.",
    };

    private static object Escudo(string id, string nome, bool equipado = false, int durabilidade = 4) => new
    {
        id, itemId = "i-" + id, nome, categoria = "Leve", bonusDefesa = 1, peso = 2m, isEquipped = equipado,
        durabilidadeAtual = durabilidade, durabilidadeMaxima = 4, descricao = "Um escudo.",
    };

    private static object Maestria(string id, string nome, int gasto = 1, int total = 4) =>
        new { id, nome, pericia = "Atletismo", atributo = "Vigor", gastoMaestria = gasto, total };

    private static object SlotCom(string slot, string nome, int durabilidade = 5) => new
    {
        slot, itemId = "a-" + slot, nome, categoria = "Media", defesa = 3, rf = 2, rm = 1, peso = 8m,
        durabilidadeAtual = durabilidade, durabilidadeMaxima = 6, durabilidadeMaximo = 6, descricao = "Anéis.",
    };

    private static IElement TabelaCom(IRenderedComponent<IComponent> cut, string cabecalho) =>
        AbaAberta(cut).QuerySelectorAll("table").Single(t => t.QuerySelectorAll("th").Any(th => TextoNormalizado(th) == cabecalho));

    private static List<string> NomesDasArmas(IRenderedComponent<IComponent> cut) =>
        TabelaCom(cut, "Tipo de Dano").QuerySelectorAll("tbody tr").Select(tr => TextoNormalizado(tr.QuerySelector("td")!)).ToList();

    private static IElement LinhaDaArma(IRenderedComponent<IComponent> cut, string nome) => Linha(TabelaCom(cut, "Tipo de Dano"), nome);

    private static List<IElement> LinhasDosEscudos(IRenderedComponent<IComponent> cut) =>
        TabelaCom(cut, "Bônus de Defesa").QuerySelectorAll("tbody tr").ToList();

    private static IElement LinhaDoSlot(IRenderedComponent<IComponent> cut, string slot) => Linha(Tabela(AbaAberta(cut), "Slot"), slot);

    private static IElement FormularioDe(IRenderedComponent<IComponent> cut, string botao) => Botao(AbaAberta(cut), botao).Closest("form")!;

    /// <summary>As linhas das Maestrias: a tabela que vem logo depois do formulário "Adicionar Maestria".</summary>
    private static List<IElement> LinhasDasMaestrias(IRenderedComponent<IComponent> cut)
    {
        var secao = FormularioDe(cut, "Adicionar Maestria").ParentElement!;
        return secao.QuerySelectorAll("table tbody tr").ToList();
    }

    private static async Task EscolherNoCatalogoAsync(IRenderedComponent<IComponent> cut, int indice, string itemId, string nome)
    {
        var seletor = cut.FindComponents<EntityPicker>()[indice];
        await cut.InvokeAsync(() => seletor.Instance.SearchAsyncForTests(""));
        await cut.InvokeAsync(() => seletor.Instance.SelectForTests(new PickerOption(itemId, nome)));
    }

    /// <summary>A gravação, a recarga das abas na ordem e, quando a versão das penalidades sobe, o alerta delas.</summary>
    private async Task EsperarGravacaoAsync(IRenderedComponent<IComponent> cut, string tipo, string gravacao, bool recarregaPenalidades, params string[] antes)
    {
        var esperado = new List<string>(antes) { gravacao };
        esperado.AddRange(RecargaDasAbas(tipo));
        if (recarregaPenalidades)
            esperado.Add($"GET /{Prefixo(tipo)}/{SheetId}/equipment-penalties");
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(esperado));
    }

    private static readonly TimeSpan Limite = LimiteDeEspera;

    // ---------------- a aba, o estado e o formulário da página ----------------

    private static object Pagina(IRenderedComponent<IComponent> cut, string tipo) => cut.FindComponents<IComponent>().Select(c => c.Instance)
        .Single(c => c.GetType().Name == tipo switch { "Personagem" => "FichaDePersonagem", "NPC" => "FichaDeNpc", _ => "FichaDeCriatura" });

    private static object? CampoDaPagina(object pagina, string nome) =>
        pagina.GetType().GetField(nome, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(pagina);

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_tab_is_the_shared_component_with_the_page_state_form_and_saves(string tipo)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponents<AbaCombate>().Should().ContainSingle();
        var aba = cut.FindComponent<AbaCombate>().Instance;
        aba.Estado.Should().BeSameAs(cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value);
        var pagina = Pagina(cut, tipo);
        aba.Formulario.Should().NotBeNull().And.BeSameAs(CampoDaPagina(pagina, "_form"));
        aba.AutoSave.Should().NotBeNull().And.BeSameAs(CampoDaPagina(pagina, "_autoSave"));
        // Os recursos vão no autosave; a Cobertura (e a Afinidade) salvam na hora e releem as abas; a Sina salva na hora.
        aba.AoMudar.HasDelegate.Should().BeTrue();
        aba.SalvarERecarregar.HasDelegate.Should().BeTrue();
        aba.SalvarAgora.HasDelegate.Should().Be(tipo != "Criatura");
    }

    [Theory]
    [InlineData("Personagem", 0, 1)]
    [InlineData("NPC", 0, 0)]
    [InlineData("Criatura", 1, 0)]
    public async Task The_parts_follow_the_kind(string tipo, int armaManual, int maestriasNaLinha)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponents<ArmaManualForm>().Should().HaveCount(armaManual);
        cut.FindComponents<MaestriasNaLinha>().Should().HaveCount(maestriasNaLinha);
        // Arma, os dois slots vazios e o escudo: o seletor do Catálogo do tipo (também o da Origem "Catálogo" da Criatura).
        cut.FindComponents<SeletorDeItem>().Select(s => s.Instance.Tipo).Should().Equal("Arma", "Armadura", "Armadura", "Escudo");
        cut.FindComponents<MudRating>().Should().HaveCount(tipo == "Criatura" ? 0 : 1);
    }

    // ---------------- Review Focus (5): a arma manual da Criatura ----------------

    // O ataque natural (sem ItemId, sem Peso, DurabilidadeMaximo) e uma arma do Catálogo sem Durabilidade (R-E2): a aba
    // renderiza sem erro, com as colunas de hoje (sem Peso), "Ataque natural" na Durabilidade da manual e a Durabilidade
    // em branco (nunca 0) na do Catálogo.
    [Theory]
    [InlineData("GM")]
    [InlineData("Jogador")]
    public async Task Creature_manual_weapon_renders_like_today(string papel)
    {
        FixarCulturaPtBr();
        Responder("/creature-sheets/s1/weapons", new object[]
        {
            new
            {
                id = "w2", itemId = (string?)null, nome = "Mordida", tipoDeDano = "Perfurante", alcance = (int?)null, dados = "2d6", dano = 4,
                critico = (string?)null, rank = (string?)null, isEquipped = true, durabilidadeAtual = (int?)null, durabilidadeMaximo = (int?)null, descricao = (string?)null,
            },
            new
            {
                id = "w3", itemId = "i7", nome = "Clava Velha", tipoDeDano = "Contundente", alcance = 1, dados = "1d6", dano = 2,
                critico = "20", rank = "E", isEquipped = false, durabilidadeAtual = (int?)null, durabilidadeMaximo = (int?)null, descricao = "Gasta.",
            },
        });
        var cut = await AbrirAsync("Criatura", papel);

        TabelaCom(cut, "Tipo de Dano").QuerySelectorAll("th").Select(TextoNormalizado).Should()
            .Equal("Nome", "Tipo de Dano", "Alcance", "Dados", "Dano", "Crítico", "Rank", "Durabilidade", "Equipada", "");
        Leitura(LinhaDaArma(cut, "Mordida")).Should().Equal("Mordida", "Perfurante", "—", "2d6", "4", "—", "—", "Ataque natural", "Equipada", "Desequipar", "Remover");
        Leitura(LinhaDaArma(cut, "Clava Velha")).Should().Equal("Clava Velha", "Contundente", "1", "1d6", "2", "20", "E", "«»", "/", "Equipar", "Remover");
        LinhaDaArma(cut, "Mordida").QuerySelectorAll("input").Should().BeEmpty();
    }

    // As colunas da tabela de armas por tipo: o Peso só onde o tipo o mostra.
    [Theory]
    [InlineData("Personagem", true)]
    [InlineData("NPC", true)]
    [InlineData("Criatura", false)]
    public async Task The_weapon_table_has_a_weight_column_only_where_the_kind_shows_it(string tipo, bool peso)
    {
        var cut = await AbrirAsync(tipo);

        TabelaCom(cut, "Tipo de Dano").QuerySelectorAll("th").Select(TextoNormalizado).Contains("Peso").Should().Be(peso);
        // "Adicionar Arma" ganha uma margem de cima só abaixo da Origem e dos campos manuais.
        Botao(AbaAberta(cut), "Adicionar Arma").ClassList.Contains("mt-3").Should().Be(tipo == "Criatura");
    }

    // ---------------- dados novos depois de gravar (a rede não vê: responde sempre as mesmas linhas) ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adding_a_weapon_shows_the_reloaded_list(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/weapons", new[] { Arma("w1", "Espada Longa"), Arma("w9", "Machado", equipada: false) });

        await EscolherNoCatalogoAsync(cut, 0, "i9", "Machado");
        await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Arma").Submit());

        var corpo = tipo == "Criatura"
            ? "{\"itemId\":\"i9\",\"manualDados\":null,\"manualDano\":null,\"manualNome\":null,\"manualTipoDeDano\":null}"
            : "{\"itemId\":\"i9\"}";
        await EsperarGravacaoAsync(cut, tipo, $"POST /{Prefixo(tipo)}/s1/weapons {corpo}", recarregaPenalidades: true, "GET /items?nome=&tipo=Arma");
        cut.WaitForAssertion(() => NomesDasArmas(cut).Should().Equal("Espada Longa", "Machado"), Limite);
    }

    [Theory]
    [InlineData("GM")]
    [InlineData("Jogador")]
    public async Task Adding_a_natural_attack_shows_the_reloaded_list_and_clears_the_manual_fields(string papel)
    {
        var cut = await AbrirAsync("Criatura", papel);
        await EscolherOpcaoAsync(cut, () => FormularioDe(cut, "Adicionar Arma"), "Origem", "Preencher manualmente (ataque natural)");
        await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Arma"), "Nome").Change("Mordida"));
        await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Arma"), "Dados").Change("2d6"));
        await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Arma"), "Dano").Change("4"));
        Responder("/creature-sheets/s1/weapons", new object[]
        {
            Arma("w1", "Espada Longa"),
            new
            {
                id = "w2", itemId = (string?)null, nome = "Mordida", tipoDeDano = (string?)null, alcance = (int?)null, dados = "2d6", dano = 4,
                critico = (string?)null, rank = (string?)null, isEquipped = false, durabilidadeAtual = (int?)null, durabilidadeMaximo = (int?)null, descricao = (string?)null,
            },
        });

        await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Arma").Submit());

        await EsperarGravacaoAsync(cut, "Criatura",
            "POST /creature-sheets/s1/weapons {\"itemId\":null,\"manualDados\":\"2d6\",\"manualDano\":4,\"manualNome\":\"Mordida\",\"manualTipoDeDano\":\"\"}",
            recarregaPenalidades: true);
        cut.WaitForAssertion(() =>
        {
            NomesDasArmas(cut).Should().Equal("Espada Longa", "Mordida");
            Leitura(LinhaDaArma(cut, "Mordida")).Should().Contain("Ataque natural");
            // A Origem continua manual; os campos manuais esvaziam.
            Campo(FormularioDe(cut, "Adicionar Arma"), "Nome").GetAttribute("value").Should().BeNullOrEmpty();
            Campo(FormularioDe(cut, "Adicionar Arma"), "Dados").GetAttribute("value").Should().BeNullOrEmpty();
            Campo(FormularioDe(cut, "Adicionar Arma"), "Dano").GetAttribute("value").Should().Be("0");
        }, Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Removing_a_weapon_shows_the_reloaded_list(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/weapons", Array.Empty<object>());

        await cut.InvokeAsync(() => Botao(LinhaDaArma(cut, "Espada Longa"), "Remover").Click());

        await EsperarGravacaoAsync(cut, tipo, $"DELETE /{Prefixo(tipo)}/s1/weapons/w1", recarregaPenalidades: true);
        cut.WaitForAssertion(() => NomesDasArmas(cut).Should().BeEmpty(), Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Unequipping_a_weapon_shows_the_reloaded_row_and_penalties(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/weapons", new[] { Arma("w1", "Espada Longa", equipada: false) });
        Responder("/equipment-penalties", new[] { new { itemNome = "Cota de Malha", requisitosPendentes = new[] { "Vigor 3" }, penalidade = new[] { "Esquiva −1" } } });

        await cut.InvokeAsync(() => Botao(LinhaDaArma(cut, "Espada Longa"), "Desequipar").Click());

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/weapons/w1 false", recarregaPenalidades: true);
        cut.WaitForAssertion(() =>
        {
            Leitura(LinhaDaArma(cut, "Espada Longa")).Should().Contain("Equipar").And.NotContain("Equipada");
            TextoNormalizado(AbaAberta(cut)).Should().Contain("Cota de Malha");
        }, Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Changing_weapon_durability_shows_the_reloaded_value(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        // O servidor guarda outro valor (ex.: limitado ao máximo): a tela mostra o dele.
        Responder("/weapons", new[] { Arma("w1", "Espada Longa", durabilidade: 6) });

        await cut.InvokeAsync(() => LinhaDaArma(cut, "Espada Longa").QuerySelector("input")!.Change("7"));

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/weapons/w1/durabilidade 7", recarregaPenalidades: false);
        cut.WaitForAssertion(() => LinhaDaArma(cut, "Espada Longa").QuerySelector("input")!.GetAttribute("value").Should().Be("6"), Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Assigning_an_armor_shows_the_reloaded_slot_and_clears_the_picker(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/armor-slots", new object[] { SlotCom("Capacete", "Elmo"), SlotCom("Superior", "Cota de Malha"), new { slot = "Inferior" } });

        await EscolherNoCatalogoAsync(cut, 1, "a9", "Elmo");
        await cut.InvokeAsync(() => Botao(LinhaDoSlot(cut, "Capacete"), "Atribuir").Click());

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/armor-slots/Capacete {{\"itemId\":\"a9\"}}", recarregaPenalidades: true, "GET /items?nome=&tipo=Armadura");
        cut.WaitForAssertion(() =>
        {
            Leitura(LinhaDoSlot(cut, "Capacete")).Should().Contain("Elmo").And.Contain("Remover");
            // O seletor que sobra (Inferior) começa vazio.
            Leitura(LinhaDoSlot(cut, "Inferior")).Should().Contain("«»");
        }, Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Removing_an_armor_shows_the_empty_slot(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/armor-slots", new object[] { new { slot = "Capacete" }, new { slot = "Superior" }, new { slot = "Inferior" } });

        await cut.InvokeAsync(() => Botao(LinhaDoSlot(cut, "Superior"), "Remover").Click());

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/armor-slots/Superior {{\"itemId\":null}}", recarregaPenalidades: true);
        cut.WaitForAssertion(() => Leitura(LinhaDoSlot(cut, "Superior")).Should().Contain("Atribuir").And.NotContain("Cota de Malha"), Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Changing_armor_durability_shows_the_reloaded_value(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/armor-slots", new object[] { new { slot = "Capacete" }, SlotCom("Superior", "Cota de Malha", durabilidade: 3), new { slot = "Inferior" } });

        await cut.InvokeAsync(() => LinhaDoSlot(cut, "Superior").QuerySelector("input")!.Change("4"));

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/armor-slots/Superior/durabilidade 4", recarregaPenalidades: false);
        cut.WaitForAssertion(() => LinhaDoSlot(cut, "Superior").QuerySelector("input")!.GetAttribute("value").Should().Be("3"), Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adding_a_shield_shows_the_reloaded_list(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/shields", new[] { Escudo("sh1", "Broquel"), Escudo("sh2", "Pavês") });

        await EscolherNoCatalogoAsync(cut, 3, "i8", "Pavês");
        await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Escudo").Submit());

        await EsperarGravacaoAsync(cut, tipo, $"POST /{Prefixo(tipo)}/s1/shields {{\"itemId\":\"i8\"}}", recarregaPenalidades: true, "GET /items?nome=&tipo=Escudo");
        cut.WaitForAssertion(() => LinhasDosEscudos(cut).Select(l => TextoNormalizado(l.QuerySelector("td")!)).Should().Equal("Broquel", "Pavês"), Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Equipping_a_shield_shows_the_reloaded_row(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/shields", new[] { Escudo("sh1", "Broquel", equipado: true) });

        await cut.InvokeAsync(() => Botao(LinhasDosEscudos(cut).Single(), "Equipar").Click());

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/shields/sh1 true", recarregaPenalidades: true);
        cut.WaitForAssertion(() => Leitura(LinhasDosEscudos(cut).Single()).Should().Contain("Equipado").And.Contain("Desequipar"), Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Removing_a_shield_shows_the_reloaded_list(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/shields", Array.Empty<object>());

        await cut.InvokeAsync(() => Botao(LinhasDosEscudos(cut).Single(), "Remover").Click());

        await EsperarGravacaoAsync(cut, tipo, $"DELETE /{Prefixo(tipo)}/s1/shields/sh1", recarregaPenalidades: true);
        cut.WaitForAssertion(() => LinhasDosEscudos(cut).Should().BeEmpty(), Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Changing_shield_durability_shows_the_reloaded_value(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/shields", new[] { Escudo("sh1", "Broquel", durabilidade: 2) });

        await cut.InvokeAsync(() => LinhasDosEscudos(cut).Single().QuerySelector("input")!.Change("3"));

        await EsperarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/shields/sh1/durabilidade 3", recarregaPenalidades: false);
        cut.WaitForAssertion(() => LinhasDosEscudos(cut).Single().QuerySelector("input")!.GetAttribute("value").Should().Be("2"), Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adding_a_maestria_shows_the_reloaded_list_and_clears_the_form(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Maestria"), "Nome").Change("Defesa Firme"));
        await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Maestria"), "Gasto Maestria").Change("2"));
        Responder("/masteries", new[] { Maestria("ma1", "Golpe Firme"), Maestria("ma2", "Defesa Firme", 2, 6) });

        await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Maestria").Submit());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
            $"POST /{Prefixo(tipo)}/s1/masteries {{\"atributo\":\"\",\"gastoMaestria\":2,\"nome\":\"Defesa Firme\",\"pericia\":\"\"}}",
            $"GET /{Prefixo(tipo)}/s1/masteries"));
        cut.WaitForAssertion(() =>
        {
            LinhasDasMaestrias(cut).Should().HaveCount(2);
            if (tipo == "Personagem")
                LinhasDasMaestrias(cut)[1].QuerySelector("input")!.GetAttribute("value").Should().Be("Defesa Firme");
            else
                TextoNormalizado(LinhasDasMaestrias(cut)[1]).Should().StartWith("Defesa Firme — Atletismo / Vigor — Gasto 2 — Total 6");
            Campo(FormularioDe(cut, "Adicionar Maestria"), "Nome").GetAttribute("value").Should().BeNullOrEmpty();
            Campo(FormularioDe(cut, "Adicionar Maestria"), "Gasto Maestria").GetAttribute("value").Should().Be("0");
        }, Limite);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Removing_a_maestria_shows_the_reloaded_list(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        Responder("/masteries", Array.Empty<object>());

        await cut.InvokeAsync(() => Botao(LinhasDasMaestrias(cut).Single(), "Remover").Click());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"DELETE /{Prefixo(tipo)}/s1/masteries/ma1", $"GET /{Prefixo(tipo)}/s1/masteries"));
        cut.WaitForAssertion(() => LinhasDasMaestrias(cut).Should().BeEmpty(), Limite);
    }

    // Só o Personagem edita a Maestria na linha.
    [Theory]
    [InlineData("GM")]
    [InlineData("Jogador")]
    public async Task Editing_a_maestria_inline_shows_the_reloaded_row(string papel)
    {
        var cut = await AbrirAsync("Personagem", papel);
        Responder("/masteries", new[] { Maestria("ma1", "Golpe Certeiro", 3, 7) });

        await cut.InvokeAsync(() => LinhasDasMaestrias(cut).Single().QuerySelectorAll("td")[3].QuerySelector("input")!.Change("3"));

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
            "PUT /character-sheets/s1/masteries/ma1 {\"atributo\":\"Vigor\",\"gastoMaestria\":3,\"nome\":\"Golpe Firme\",\"pericia\":\"Atletismo\"}",
            "GET /character-sheets/s1/masteries"));
        cut.WaitForAssertion(() =>
        {
            var linha = LinhasDasMaestrias(cut).Single();
            linha.QuerySelector("input")!.GetAttribute("value").Should().Be("Golpe Certeiro");
            TextoNormalizado(linha.QuerySelectorAll("td")[4]).Should().Be("7");
        }, Limite);
    }

    // ---------------- erros de dentro da aba no alerta da página ----------------

    [Theory]
    [InlineData("Personagem", "Adicionar Arma", "/weapons", "Não foi possível adicionar a arma.")]
    [InlineData("NPC", "Adicionar Arma", "/weapons", "Não foi possível adicionar a arma.")]
    [InlineData("Criatura", "Adicionar Arma", "/weapons", "Não foi possível adicionar a arma.")]
    [InlineData("Personagem", "Adicionar Escudo", "/shields", "Não foi possível adicionar o escudo.")]
    [InlineData("NPC", "Adicionar Escudo", "/shields", "Não foi possível adicionar o escudo.")]
    [InlineData("Criatura", "Adicionar Escudo", "/shields", "Não foi possível adicionar o escudo.")]
    [InlineData("Personagem", "Adicionar Maestria", "/masteries", "Não foi possível adicionar a maestria.")]
    [InlineData("NPC", "Adicionar Maestria", "/masteries", "Não foi possível adicionar a maestria.")]
    [InlineData("Criatura", "Adicionar Maestria", "/masteries", "Não foi possível adicionar a maestria.")]
    public async Task A_refused_add_shows_in_the_page_alert_and_reloads_nothing(string tipo, string botao, string caminho, string mensagem)
    {
        var cut = await AbrirAsync(tipo);
        ResponderErro($"/{Prefixo(tipo)}/s1{caminho}", HttpStatusCode.BadRequest, "Recusado.");

        await cut.InvokeAsync(() => FormularioDe(cut, botao).Submit());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().ContainSingle().Which.Should().StartWith($"POST /{Prefixo(tipo)}/s1{caminho}"));
        cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Select(TextoNormalizado).Should().Equal(mensagem), Limite);
    }

    // ---------------- o formulário auto-salvo da página dentro da aba ----------------

    // A Sina salva na hora (fora do autosave): o PUT e a recarga do salvamento; a tela mostra a Sina relida.
    [Theory]
    [MemberData(nameof(TiposComSina))]
    public async Task Changing_sina_saves_now_and_shows_the_reloaded_value(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        var ficha = await FichaMudadaAsync(tipo, f => f["sinaAtual"] = 3);
        Responder($"^/{Prefixo(tipo)}/s1$", ficha);

        await cut.InvokeAsync(() => cut.FindComponent<MudRating>().Instance.SelectedValueChanged.InvokeAsync(3));

        var antes = tipo == "Personagem" ? "\"sinaAtual\":1" : "\"sinaAtual\":2";
        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: tipo == "NPC" ? "Borin" : "Aria").Replace(antes, "\"sinaAtual\":3");
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.First().Should().Be($"PUT /{Prefixo(tipo)}/s1 {corpo}");
            todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo)
                .Append($"GET /{Prefixo(tipo)}/s1/equipment-penalties"));
            todas.Last().Should().Be($"GET /{Prefixo(tipo)}/s1/equipment-penalties");
        });
        cut.WaitForAssertion(() =>
        {
            var leitura = Leitura(AbaAberta(cut));
            leitura.Skip(leitura.IndexOf("Pontos de Sina") + 1).Take(3).Should().Equal("[ ]", "[ ]", "[x]");
        }, Limite);
    }

    // Os recursos vão no autosave da página: o indicador da aba mostra o salvamento e a tela, o máximo relido.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Editing_vitalidade_autosaves_and_shows_the_reloaded_maximum(string tipo)
    {
        var cut = await AbrirAsync(tipo, "Jogador");
        var ficha = await FichaMudadaAsync(tipo, f =>
        {
            f["vitalidadeAtual"] = 15;
            f["vitalidadeMaximo"] = 40;
        });
        Responder($"^/{Prefixo(tipo)}/s1$", ficha);
        var atual = tipo switch { "Personagem" => 20, "NPC" => 30, _ => 18 };

        await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Vitalidade").Change("15"));

        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: tipo switch { "NPC" => "Borin", "Criatura" => "Lobo Cinzento", _ => "Aria" })
            .Replace($"\"vitalidadeAtual\":{atual}", "\"vitalidadeAtual\":15");
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.First().Should().Be($"PUT /{Prefixo(tipo)}/s1 {corpo}");
            todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo)
                .Append($"GET /{Prefixo(tipo)}/s1/equipment-penalties"));
        });
        cut.WaitForAssertion(() =>
        {
            Leitura(AbaAberta(cut)).Should().ContainInOrder("«15»", "/ 40", "Vitalidade");
            AbaAberta(cut).QuerySelector(".rr-autosave-indicator")!.TextContent.Should().StartWith("Salvo às");
        }, Limite);
    }

    // A Cobertura salva na hora e relê as abas (ela entra na Defesa Natural, dos Sub-Atributos); a tela mostra os dois.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Changing_cobertura_saves_reloads_the_tabs_and_shows_the_new_defense(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        var ficha = await FichaMudadaAsync(tipo, f => f["cobertura"] = "Parcial");
        Responder($"^/{Prefixo(tipo)}/s1$", ficha);
        Responder("/sub-attributes", new
        {
            iniciativa = 5, movimentacao = 9, esquivaNatural = 11, defesaNatural = 18, reducaoFisica = 2, reducaoMagica = 1,
            pesoAtual = 12.5m, pesoMaximo = 40m, eficienciaElemental = 3, danoElemental = 2, bonusDeCargaDaCampanha = 0m,
        });

        await cut.InvokeAsync(() => AbaAberta(cut).QuerySelector("button[aria-label='Cobertura parcial (+5)']")!.Click());

        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: tipo switch { "NPC" => "Borin", "Criatura" => "Lobo Cinzento", _ => "Aria" })
            .Replace("\"cobertura\":\"Nenhuma\"", "\"cobertura\":\"Parcial\"");
        await EsperarSalvarERecarregarAsync(cut, tipo, corpo);
        cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Contain("Defesa Natural: 18").And.Contain("Parcial (+5)"), Limite);
    }

    // A Afinidade (Personagem e NPC) salva na hora e relê as abas (a Eficiência e o Dano Elemental vêm dela).
    [Theory]
    [MemberData(nameof(TiposComSina))]
    public async Task Changing_afinidade_saves_reloads_the_tabs_and_shows_the_new_efficiency(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        var ficha = await FichaMudadaAsync(tipo, f => f["afinidade"] = "Agua");
        Responder($"^/{Prefixo(tipo)}/s1$", ficha);
        Responder("/sub-attributes", new
        {
            iniciativa = 5, movimentacao = 9, esquivaNatural = 11, defesaNatural = 13, reducaoFisica = 2, reducaoMagica = 1,
            pesoAtual = 12.5m, pesoMaximo = 40m, eficienciaElemental = 7, danoElemental = 4, bonusDeCargaDaCampanha = 0m,
        });

        await EscolherOpcaoAsync(cut, () => AbaAberta(cut), "Afinidade", "Água");

        var antes = tipo == "Personagem" ? "\"afinidade\":\"Fogo\"" : "\"afinidade\":\"Terra\"";
        var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: tipo == "NPC" ? "Borin" : "Aria").Replace(antes, "\"afinidade\":\"Agua\"");
        await EsperarSalvarERecarregarAsync(cut, tipo, corpo);
        cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().ContainInOrder("Água", "Eficiência Elemental", "7", "conforme a Tabela de Afinidades", "Dano Elemental", "4"), Limite);
    }

    /// <summary>O PUT, a recarga do salvamento (o conjunto), a recarga das abas em sequência e, no fim, o alerta das penalidades.</summary>
    private async Task EsperarSalvarERecarregarAsync(IRenderedComponent<IComponent> cut, string tipo, string corpo)
    {
        var depoisDeSalvar = RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo);
        var recarga = RecargaDasAbas(tipo).Append($"GET /{Prefixo(tipo)}/s1/equipment-penalties").ToArray();
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.Should().HaveCount(1 + depoisDeSalvar.Length + recarga.Length);
            todas.First().Should().Be($"PUT /{Prefixo(tipo)}/s1 {corpo}");
            todas.Skip(1).Take(depoisDeSalvar.Length).Should().BeEquivalentTo(depoisDeSalvar);
            todas.TakeLast(recarga.Length).Should().Equal(recarga);
        });
    }
}
