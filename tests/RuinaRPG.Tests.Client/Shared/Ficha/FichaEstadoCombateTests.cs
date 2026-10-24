using System.Net;
using System.Text;
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A parte Combate do <see cref="FichaEstado"/> (E, Task 12): a carga do Combate no fim da recarga das abas 2 e 3 (as
/// mesmas cinco requisições de hoje, na mesma ordem), as ações de Armas, Armaduras, Escudos e Maestrias com a recarga,
/// a versão das penalidades e o aviso à página, e a arma da Criatura (manual, ou do Catálogo sem Durabilidade: R-E2).
/// </summary>
public class FichaEstadoCombateTests
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    private static TipoDeFichaCliente Tipo(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private readonly List<string> _requisicoes = new();
    private readonly Dictionary<string, (HttpStatusCode Status, string Corpo)> _respostas = new();

    // As duas grafias da Durabilidade máxima vão juntas: cada tipo lê a do seu record.
    private const string Armas = """[{"id":"w1","itemId":"i1","nome":"Espada","tipoDeDano":"Cortante","alcance":1,"dados":"1d8","dano":3,"critico":"20","rank":"C","peso":1.5,"isEquipped":true,"durabilidadeAtual":8,"durabilidadeMaxima":10,"durabilidadeMaximo":10}]""";

    private const string Slots = """[{"slot":"Capacete"},{"slot":"Superior","itemId":"a1","nome":"Cota","durabilidadeAtual":5,"durabilidadeMaxima":6,"durabilidadeMaximo":6},{"slot":"Inferior"}]""";

    private const string Escudos = """[{"id":"sh1","itemId":"i3","nome":"Broquel","peso":2,"isEquipped":false,"durabilidadeAtual":4,"durabilidadeMaxima":4}]""";

    private const string Maestrias = """[{"id":"ma1","nome":"Golpe Firme","pericia":"Atletismo","atributo":"Vigor","gastoMaestria":1,"total":4}]""";

    private FichaEstado Estado(string tipo)
    {
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            var caminho = req.RequestUri!.PathAndQuery.Replace("/api/", "");
            var corpo = req.Content is null ? "" : " " + req.Content.ReadAsStringAsync().Result;
            lock (_requisicoes)
                _requisicoes.Add($"{req.Method} {caminho}{corpo}");
            var resposta = _respostas.TryGetValue($"{req.Method} {caminho}", out var r) ? r
                : req.Method != HttpMethod.Get ? (HttpStatusCode.OK, "{}")
                : caminho.EndsWith("/weapons") ? (HttpStatusCode.OK, Armas)
                : caminho.EndsWith("/armor-slots") ? (HttpStatusCode.OK, Slots)
                : caminho.EndsWith("/shields") ? (HttpStatusCode.OK, Escudos)
                : caminho.EndsWith("/masteries") ? (HttpStatusCode.OK, Maestrias)
                : caminho.EndsWith("/sub-attributes") ? (HttpStatusCode.OK, """{"iniciativa":5,"defesaNatural":13}""")
                : caminho.EndsWith("/modificador-de-dano") ? (HttpStatusCode.OK, """{"cortante":1,"perfurante":0,"contundente":0,"arcano":0,"danoElemental":{}}""")
                : caminho.EndsWith("/attributes") || caminho.EndsWith("/skills") || caminho.EndsWith("/affinities") ? (HttpStatusCode.OK, "[]")
                : (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(resposta.Item1) { Content = new StringContent(resposta.Item2, Encoding.UTF8, "application/json") };
        });
        return new FichaEstado(http, Tipo(tipo), "s1");
    }

    private static string K(string tipo) => Tipo(tipo).Url("s1");

    private static string[] Combate(string tipo) =>
        new[] { $"GET {K(tipo)}/weapons", $"GET {K(tipo)}/armor-slots", $"GET {K(tipo)}/shields", $"GET {K(tipo)}/sub-attributes", $"GET {K(tipo)}/modificador-de-dano" };

    /// <summary>A recarga das abas 2 e 3: o que o tipo tem de Atributos &amp; Perícias, e então o Combate.</summary>
    private static string[] Recarga(string tipo)
    {
        var lista = new List<string> { $"GET {K(tipo)}/attributes", $"GET {K(tipo)}/attributes/budget", $"GET {K(tipo)}/skills" };
        if (tipo == "Personagem")
            lista.Add($"GET {K(tipo)}/skills/budget");
        if (tipo != "Criatura")
            lista.Add($"GET {K(tipo)}/affinities");
        lista.AddRange(Combate(tipo));
        return lista.ToArray();
    }

    private async Task<(FichaEstado Estado, Func<int> Notificacoes)> CarregadoAsync(string tipo)
    {
        var estado = Estado(tipo);
        await estado.CarregarAtributosECombateAsync();
        await estado.CarregarMaestriasAsync();
        _requisicoes.Clear();
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        return (estado, () => notificacoes);
    }

    // ---------------- carga ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_reload_ends_with_todays_five_combat_requests_in_order(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarAtributosECombateAsync();

        _requisicoes.Should().Equal(Recarga(tipo));
        estado.Armas.Select(a => (a.Arma.Id, a.Arma.Nome, a.DurabilidadeAtual, a.DurabilidadeMaxima)).Should().Equal(("w1", "Espada", (int?)8, (int?)10));
        estado.SlotsDeArmadura.Select(s => (s.Slot, s.DurabilidadeMaxima)).Should().Equal(("Capacete", null), ("Superior", (int?)6), ("Inferior", null));
        estado.Escudos.Select(e => e.Id).Should().Equal("sh1");
        estado.SubAtributos!.DefesaNatural.Should().Be(13);
        estado.ModificadorDeDano!.Cortante.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Missing_combat_lists_become_empty(string tipo)
    {
        var estado = Estado(tipo);
        _respostas[$"GET {K(tipo)}/weapons"] = (HttpStatusCode.OK, "null");
        _respostas[$"GET {K(tipo)}/armor-slots"] = (HttpStatusCode.OK, "null");
        _respostas[$"GET {K(tipo)}/shields"] = (HttpStatusCode.OK, "null");
        _respostas[$"GET {K(tipo)}/masteries"] = (HttpStatusCode.OK, "null");

        await estado.CarregarAtributosECombateAsync();
        await estado.CarregarMaestriasAsync();

        estado.Armas.Should().BeEmpty();
        estado.SlotsDeArmadura.Should().BeEmpty();
        estado.Escudos.Should().BeEmpty();
        estado.Maestrias.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_maestrias_load_on_their_own(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarMaestriasAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/masteries");
        estado.Maestrias.Select(m => (m.Nome, m.Total)).Should().Equal(("Golpe Firme", 4));
    }

    // R-E2: a arma manual (sem ItemId) e a do Catálogo sem Durabilidade chegam com a Durabilidade nula; a linha a guarda
    // nula (a tela a mostra em branco), embora o record compartilhado tenha 0.
    [Fact]
    public async Task Creature_weapons_keep_a_null_durability_for_the_screen()
    {
        var estado = Estado("Criatura");
        _respostas[$"GET {K("Criatura")}/weapons"] = (HttpStatusCode.OK,
            """[{"id":"w2","itemId":null,"nome":"Mordida","dados":"2d6","dano":4,"isEquipped":false},{"id":"w3","itemId":"i7","nome":"Clava","isEquipped":false,"durabilidadeAtual":null,"durabilidadeMaximo":null}]""");

        await estado.CarregarAtributosECombateAsync();

        estado.Armas.Select(a => (a.Arma.Id, (string?)a.Arma.ItemId, a.DurabilidadeAtual, a.DurabilidadeMaxima)).Should()
            .Equal(("w2", (string?)null, (int?)null, (int?)null), ("w3", "i7", null, null));
        estado.Armas.Select(a => a.Arma.DurabilidadeAtual).Should().Equal(0, 0);
    }

    // ---------------- armas ----------------

    [Theory]
    [InlineData("Personagem", """{"itemId":"i9"}""")]
    [InlineData("NPC", """{"itemId":"i9"}""")]
    [InlineData("Criatura", """{"itemId":"i9","manualNome":null,"manualTipoDeDano":null,"manualDados":null,"manualDano":null}""")]
    public async Task Adding_a_catalog_weapon_posts_reloads_bumps_and_clears(string tipo, string corpo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);
        estado.FormularioDeArma.ItemId = "i9";

        await estado.AdicionarArmaAsync();

        _requisicoes.Should().Equal(new[] { $"POST {K(tipo)}/weapons {corpo}" }.Concat(Recarga(tipo)));
        estado.FormularioDeArma.ItemId.Should().BeEmpty();
        estado.PenalidadesVersao.Should().Be(1);
        notificacoes().Should().Be(1);
    }

    [Fact]
    public async Task On_a_kind_with_manual_weapons_a_manual_weapon_posts_its_fields_and_clears_them()
    {
        var (estado, notificacoes) = await CarregadoAsync("Criatura");
        var form = estado.FormularioDeArma;
        form.Origem = "Manual";
        form.ItemId = "i9";
        form.ManualNome = "Mordida";
        form.ManualTipoDeDano = "Perfurante";
        form.ManualDados = "2d6";
        form.ManualDano = 4;

        await estado.AdicionarArmaAsync();

        _requisicoes.Should().Equal(new[]
        {
            $"POST {K("Criatura")}/weapons {{\"itemId\":null,\"manualNome\":\"Mordida\",\"manualTipoDeDano\":\"Perfurante\",\"manualDados\":\"2d6\",\"manualDano\":4}}",
        }.Concat(Recarga("Criatura")));
        (form.Origem, form.ItemId, form.ManualNome, form.ManualTipoDeDano, form.ManualDados, form.ManualDano).Should().Be(("Manual", "", "", "", "", 0));
        notificacoes().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_refused_weapon_shows_the_error_keeps_the_form_and_reloads_nothing(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);
        _respostas[$"POST {K(tipo)}/weapons"] = (HttpStatusCode.BadRequest, "\"Arma inválida.\"");
        estado.FormularioDeArma.ItemId = "i9";

        await estado.AdicionarArmaAsync();

        _requisicoes.Should().ContainSingle();
        estado.Erro.Should().Be("Não foi possível adicionar a arma.");
        estado.FormularioDeArma.ItemId.Should().Be("i9");
        estado.PenalidadesVersao.Should().Be(0);
        notificacoes().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Equipping_and_removing_a_weapon_write_reload_and_bump(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);

        await estado.EquiparArmaAsync("w1", false);
        await estado.RemoverArmaAsync("w1");

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/weapons/w1 false" }.Concat(Recarga(tipo))
            .Concat(new[] { $"DELETE {K(tipo)}/weapons/w1" }).Concat(Recarga(tipo)));
        estado.PenalidadesVersao.Should().Be(2);
        notificacoes().Should().Be(2);
    }

    // A Durabilidade não mexe nas penalidades; um valor que não se lê não grava, mas a recarga vem igual.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Weapon_durability_puts_only_a_number_and_always_reloads(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);

        await estado.AtualizarDurabilidadeDaArmaAsync("w1", 7);
        await estado.AtualizarDurabilidadeDaArmaAsync("w1", null);

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/weapons/w1/durabilidade 7" }.Concat(Recarga(tipo)).Concat(Recarga(tipo)));
        estado.PenalidadesVersao.Should().Be(0);
        notificacoes().Should().Be(2);
    }

    // ---------------- armaduras ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Assigning_and_clearing_an_armor_slot_write_reload_bump_and_clear_the_picker(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);
        estado.FormularioDosSlots["Capacete"] = "a9";

        await estado.AtribuirSlotDeArmaduraAsync("Capacete", estado.FormularioDosSlots["Capacete"]);
        await estado.AtribuirSlotDeArmaduraAsync("Superior", null);
        await estado.AtribuirSlotDeArmaduraAsync("Inferior", " ");

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/armor-slots/Capacete {{\"itemId\":\"a9\"}}" }.Concat(Recarga(tipo))
            .Concat(new[] { $"PUT {K(tipo)}/armor-slots/Superior {{\"itemId\":null}}" }).Concat(Recarga(tipo))
            .Concat(new[] { $"PUT {K(tipo)}/armor-slots/Inferior {{\"itemId\":null}}" }).Concat(Recarga(tipo)));
        estado.FormularioDosSlots.Should().BeEquivalentTo(new Dictionary<string, string> { ["Capacete"] = "", ["Superior"] = "", ["Inferior"] = "" });
        estado.PenalidadesVersao.Should().Be(3);
        notificacoes().Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Armor_durability_puts_only_a_number_and_always_reloads(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);

        await estado.AtualizarDurabilidadeDoSlotAsync("Superior", 4);
        await estado.AtualizarDurabilidadeDoSlotAsync("Superior", "x");

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/armor-slots/Superior/durabilidade 4" }.Concat(Recarga(tipo)).Concat(Recarga(tipo)));
        estado.PenalidadesVersao.Should().Be(0);
        notificacoes().Should().Be(2);
    }

    // ---------------- escudos ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adding_a_shield_posts_reloads_bumps_and_clears(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);
        estado.FormularioDeEscudo.ItemId = "i8";

        await estado.AdicionarEscudoAsync();

        _requisicoes.Should().Equal(new[] { $"POST {K(tipo)}/shields {{\"itemId\":\"i8\"}}" }.Concat(Recarga(tipo)));
        estado.FormularioDeEscudo.ItemId.Should().BeEmpty();
        estado.PenalidadesVersao.Should().Be(1);
        notificacoes().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_refused_shield_shows_the_error_and_reloads_nothing(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);
        _respostas[$"POST {K(tipo)}/shields"] = (HttpStatusCode.BadRequest, "\"x\"");
        estado.FormularioDeEscudo.ItemId = "i8";

        await estado.AdicionarEscudoAsync();

        _requisicoes.Should().ContainSingle();
        estado.Erro.Should().Be("Não foi possível adicionar o escudo.");
        estado.FormularioDeEscudo.ItemId.Should().Be("i8");
        notificacoes().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Equipping_removing_and_durability_of_a_shield(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);

        await estado.EquiparEscudoAsync("sh1", true);
        await estado.RemoverEscudoAsync("sh1");
        await estado.AtualizarDurabilidadeDoEscudoAsync("sh1", 3);

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/shields/sh1 true" }.Concat(Recarga(tipo))
            .Concat(new[] { $"DELETE {K(tipo)}/shields/sh1" }).Concat(Recarga(tipo))
            .Concat(new[] { $"PUT {K(tipo)}/shields/sh1/durabilidade 3" }).Concat(Recarga(tipo)));
        estado.PenalidadesVersao.Should().Be(2);
        notificacoes().Should().Be(3);
    }

    // ---------------- maestrias ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adding_a_maestria_posts_rereads_the_list_and_clears_name_and_cost(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);
        var form = estado.FormularioDeMaestria;
        form.Nome = "Defesa";
        form.Pericia = "Atletismo";
        form.Atributo = "Vigor";
        form.GastoMaestria = 2;

        await estado.AdicionarMaestriaAsync();

        _requisicoes.Should().Equal(
            $"POST {K(tipo)}/masteries {{\"nome\":\"Defesa\",\"pericia\":\"Atletismo\",\"atributo\":\"Vigor\",\"gastoMaestria\":2}}",
            $"GET {K(tipo)}/masteries");
        // Como hoje, a Perícia e o Atributo escolhidos ficam para a próxima.
        (form.Nome, form.Pericia, form.Atributo, form.GastoMaestria).Should().Be(("", "Atletismo", "Vigor", 0));
        estado.PenalidadesVersao.Should().Be(0);
        notificacoes().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_refused_maestria_shows_the_error_and_rereads_nothing(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);
        _respostas[$"POST {K(tipo)}/masteries"] = (HttpStatusCode.BadRequest, "\"x\"");
        estado.FormularioDeMaestria.Nome = "Defesa";

        await estado.AdicionarMaestriaAsync();

        _requisicoes.Should().ContainSingle();
        estado.Erro.Should().Be("Não foi possível adicionar a maestria.");
        estado.FormularioDeMaestria.Nome.Should().Be("Defesa");
        notificacoes().Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Removing_a_maestria_deletes_and_rereads(string tipo)
    {
        var (estado, notificacoes) = await CarregadoAsync(tipo);

        await estado.RemoverMaestriaAsync("ma1");

        _requisicoes.Should().Equal($"DELETE {K(tipo)}/masteries/ma1", $"GET {K(tipo)}/masteries");
        notificacoes().Should().Be(1);
    }

    [Theory]
    [InlineData("Nome", "Golpe Certeiro", """{"nome":"Golpe Certeiro","pericia":"Atletismo","atributo":"Vigor","gastoMaestria":1}""")]
    [InlineData("Pericia", "Luta", """{"nome":"Golpe Firme","pericia":"Luta","atributo":"Vigor","gastoMaestria":1}""")]
    [InlineData("Atributo", "Agilidade", """{"nome":"Golpe Firme","pericia":"Atletismo","atributo":"Agilidade","gastoMaestria":1}""")]
    [InlineData("GastoMaestria", 3, """{"nome":"Golpe Firme","pericia":"Atletismo","atributo":"Vigor","gastoMaestria":3}""")]
    [InlineData("GastoMaestria", "x", """{"nome":"Golpe Firme","pericia":"Atletismo","atributo":"Vigor","gastoMaestria":1}""")]
    [InlineData("Nome", null, """{"nome":"Golpe Firme","pericia":"Atletismo","atributo":"Vigor","gastoMaestria":1}""")]
    public async Task Editing_a_maestria_inline_puts_the_row_with_the_changed_field(string campo, object? valor, string corpo)
    {
        var (estado, notificacoes) = await CarregadoAsync("Personagem");
        estado.Erro = "antigo";

        await estado.AtualizarMaestriaAsync("ma1", campo, valor);

        _requisicoes.Should().Equal($"PUT {K("Personagem")}/masteries/ma1 {corpo}", $"GET {K("Personagem")}/masteries");
        estado.Erro.Should().BeNull();
        notificacoes().Should().Be(1);
    }

    [Fact]
    public async Task A_refused_maestria_edit_shows_the_server_message_and_still_rereads()
    {
        var (estado, notificacoes) = await CarregadoAsync("Personagem");
        _respostas[$"PUT {K("Personagem")}/masteries/ma1"] = (HttpStatusCode.BadRequest, "\"Gasto acima do limite.\"");

        await estado.AtualizarMaestriaAsync("ma1", "GastoMaestria", 30);

        _requisicoes.Should().HaveCount(2).And.EndWith($"GET {K("Personagem")}/masteries");
        estado.Erro.Should().Be("Gasto acima do limite.");
        notificacoes().Should().Be(1);
    }

    // ---------------- o nome da Perícia ----------------

    // O nome vem da própria resposta das perícias da ficha (sempre fresca); sem ela, o do catálogo da sessão.
    [Fact]
    public void The_skill_name_comes_from_the_sheet_skills_then_from_the_catalog()
    {
        var estado = Estado("NPC");
        estado.Pericias = new() { new("Atletismo", 2, 1, "Vigor", 3, false, "Atletismo Renomeado", null) };
        var catalogo = new PericiaCatalogo(FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        estado.NomeDaPericia("Atletismo", catalogo).Should().Be("Atletismo Renomeado");
        estado.NomeDaPericia("Luta", catalogo).Should().Be(catalogo.Label("Luta"));
    }
}
