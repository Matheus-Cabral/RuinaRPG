using System.Net;
using System.Text;
using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A parte Posses do <see cref="FichaEstado"/> (E, Task 8): o Inventário (onde o tipo tem), os Espólios (onde o tipo
/// tem), os Artefatos e as Afeições, com as recargas na ordem de hoje, e a busca no Catálogo de Itens (a do GM, ou o
/// que a campanha liberou para o jogador).
/// </summary>
public class FichaEstadoPossesTests
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComInventario => new() { "Personagem", "NPC" };

    private static TipoDeFichaCliente Tipo(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private readonly List<string> _requisicoes = new();
    private readonly Dictionary<string, (HttpStatusCode Status, string Corpo)> _respostas = new();

    private const string Inventario = """[{"id":"inv1","itemId":"i1","nome":"Corda","peso":1,"qtd":2,"total":2}]""";

    private const string Espolios = """[{"id":"sp1","itemId":"i2","nome":"Presa de Lobo","custo":5,"qtd":2,"custoTotal":10,"dt":12}]""";

    private const string Artefatos = """[{"id":"art1","artifactItemId":"i4","nome":"Amuleto","tipoDeAlvo":"Atributo","alvo":"Vigor","valor":1}]""";

    private const string Afeicoes = """[{"id":"afe1","nome":"Taverneiro","favorabilidade":2}]""";

    private const string Itens = """[{"id":"i7","nome":"Tocha","tipo":"ItemGeral"}]""";

    private FichaEstado Estado(string tipo, bool gm = true)
    {
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            var caminho = req.RequestUri!.PathAndQuery.Replace("/api/", "");
            var corpo = req.Content is null ? "" : " " + req.Content.ReadAsStringAsync().Result;
            lock (_requisicoes)
                _requisicoes.Add($"{req.Method} {caminho}{corpo}");
            var resposta = _respostas.TryGetValue($"{req.Method} {caminho}", out var r) ? r
                : caminho.EndsWith("/inventory") ? (HttpStatusCode.OK, Inventario)
                : caminho.EndsWith("/spoils") ? (HttpStatusCode.OK, Espolios)
                : caminho.EndsWith("/artifacts") ? (HttpStatusCode.OK, Artefatos)
                : caminho.EndsWith("/affections") ? (HttpStatusCode.OK, Afeicoes)
                : caminho.EndsWith("/attributes") || caminho.EndsWith("/skills")
                    || caminho.EndsWith("/weapons") || caminho.EndsWith("/armor-slots") || caminho.EndsWith("/shields") ? (HttpStatusCode.OK, "[]")
                : caminho.StartsWith("items?") || caminho.StartsWith("campaigns/c1/available-items?") ? (HttpStatusCode.OK, Itens)
                : (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(resposta.Item1) { Content = new StringContent(resposta.Item2, Encoding.UTF8, "application/json") };
        });
        return new FichaEstado(http, Tipo(tipo), "s1") { IsGmCaller = gm, CampaignId = "c1" };
    }

    private static string K(string tipo) => Tipo(tipo).Url("s1");

    /// <summary>A recarga das abas 2 e 3 (<see cref="FichaEstado.CarregarAtributosECombateAsync"/>): o que o tipo tem, depois o Combate.</summary>
    private static string[] AbasDoisETres(string tipo)
    {
        var lista = new List<string> { $"GET {K(tipo)}/attributes", $"GET {K(tipo)}/attributes/budget", $"GET {K(tipo)}/skills" };
        if (tipo == "Personagem")
            lista.Add($"GET {K(tipo)}/skills/budget");
        if (tipo != "Criatura")
            lista.Add($"GET {K(tipo)}/afinidades");
        lista.AddRange(new[] { $"GET {K(tipo)}/weapons", $"GET {K(tipo)}/armor-slots", $"GET {K(tipo)}/shields", $"GET {K(tipo)}/sub-attributes", $"GET {K(tipo)}/modificador-de-dano" });
        return lista.ToArray();
    }

    // ---------------- Inventário ----------------

    [Theory]
    [MemberData(nameof(TiposComInventario))]
    public async Task Carregar_inventario_reads_the_sheet_inventory(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarInventarioAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/inventory");
        estado.Inventario.Select(i => i.Nome).Should().Equal("Corda");
    }

    [Theory]
    [MemberData(nameof(TiposComInventario))]
    public async Task Adicionar_item_posts_resets_the_form_and_reloads_inventory_then_tabs_2_and_3(string tipo)
    {
        var estado = Estado(tipo);
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        estado.FormularioDeInventario.ItemId = "i7";
        estado.FormularioDeInventario.Qtd = 3;

        await estado.AdicionarItemAoInventarioAsync();

        _requisicoes.Should().Equal(new[] { $"POST {K(tipo)}/inventory {{\"itemId\":\"i7\",\"qtd\":3}}", $"GET {K(tipo)}/inventory" }.Concat(AbasDoisETres(tipo)));
        estado.FormularioDeInventario.ItemId.Should().BeEmpty();
        estado.FormularioDeInventario.Qtd.Should().Be(1);
        notificacoes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_refused_item_shows_the_message_keeps_the_form_and_reloads_nothing()
    {
        var estado = Estado("NPC");
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        _respostas[$"POST {K("NPC")}/inventory"] = (HttpStatusCode.BadRequest, "\"Item inválido.\"");
        estado.FormularioDeInventario.ItemId = "i7";

        await estado.AdicionarItemAoInventarioAsync();

        estado.Erro.Should().Be("Não foi possível adicionar o item.");
        _requisicoes.Should().ContainSingle();
        estado.FormularioDeInventario.ItemId.Should().Be("i7");
        notificacoes.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(TiposComInventario))]
    public async Task A_quantidade_do_item_puts_the_number_and_reloads(string tipo)
    {
        var estado = Estado(tipo);

        await estado.AtualizarQtdDoItemAsync("inv1", 5);

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/inventory/inv1/qtd 5", $"GET {K(tipo)}/inventory" }.Concat(AbasDoisETres(tipo)));
    }

    [Fact]
    public async Task A_blank_quantidade_do_item_only_reloads()
    {
        var estado = Estado("Personagem");

        await estado.AtualizarQtdDoItemAsync("inv1", null);

        _requisicoes.Should().Equal(new[] { $"GET {K("Personagem")}/inventory" }.Concat(AbasDoisETres("Personagem")));
    }

    [Theory]
    [MemberData(nameof(TiposComInventario))]
    public async Task Remover_item_deletes_and_reloads(string tipo)
    {
        var estado = Estado(tipo);

        await estado.RemoverItemDoInventarioAsync("inv1");

        _requisicoes.Should().Equal(new[] { $"DELETE {K(tipo)}/inventory/inv1", $"GET {K(tipo)}/inventory" }.Concat(AbasDoisETres(tipo)));
    }

    // ---------------- Espólios ----------------

    [Fact]
    public async Task Carregar_espolios_reads_the_sheet_spoils()
    {
        var estado = Estado("Criatura");

        await estado.CarregarEspoliosAsync();

        _requisicoes.Should().Equal($"GET {K("Criatura")}/spoils");
        estado.Espolios.Select(e => e.Nome).Should().Equal("Presa de Lobo");
    }

    [Fact]
    public async Task Adicionar_espolio_posts_resets_the_form_and_reloads_the_spoils_only()
    {
        var estado = Estado("Criatura");
        estado.FormularioDeEspolio.ItemId = "i2";
        estado.FormularioDeEspolio.Qtd = 2;
        estado.FormularioDeEspolio.DT = 12;

        await estado.AdicionarEspolioAsync();

        _requisicoes.Should().Equal($"POST {K("Criatura")}/spoils {{\"itemId\":\"i2\",\"qtd\":2,\"dt\":12}}", $"GET {K("Criatura")}/spoils");
        estado.FormularioDeEspolio.ItemId.Should().BeEmpty();
        estado.FormularioDeEspolio.Qtd.Should().Be(1);
        estado.FormularioDeEspolio.DT.Should().Be(0);
    }

    [Fact]
    public async Task A_refused_espolio_shows_the_message_and_reloads_nothing()
    {
        var estado = Estado("Criatura");
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        _respostas[$"POST {K("Criatura")}/spoils"] = (HttpStatusCode.BadRequest, "");
        estado.FormularioDeEspolio.ItemId = "i2";

        await estado.AdicionarEspolioAsync();

        estado.Erro.Should().Be("Não foi possível adicionar o espólio.");
        _requisicoes.Should().ContainSingle();
        estado.FormularioDeEspolio.ItemId.Should().Be("i2");
        notificacoes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_quantidade_do_espolio_puts_the_number_and_reloads()
    {
        var estado = Estado("Criatura");

        await estado.AtualizarQtdDoEspolioAsync("sp1", 4);
        await estado.AtualizarQtdDoEspolioAsync("sp1", "x");

        _requisicoes.Should().Equal($"PUT {K("Criatura")}/spoils/sp1/qtd 4", $"GET {K("Criatura")}/spoils", $"GET {K("Criatura")}/spoils");
    }

    [Fact]
    public async Task Remover_espolio_deletes_and_reloads()
    {
        var estado = Estado("Criatura");

        await estado.RemoverEspolioAsync("sp1");

        _requisicoes.Should().Equal($"DELETE {K("Criatura")}/spoils/sp1", $"GET {K("Criatura")}/spoils");
    }

    // ---------------- Artefatos ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Carregar_artefatos_reads_the_sheet_artifacts(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarArtefatosAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/artifacts");
        estado.Artefatos.Select(a => a.Nome).Should().Equal("Amuleto");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adicionar_artefato_posts_resets_reloads_and_bumps_the_penalties(string tipo)
    {
        var estado = Estado(tipo);
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        estado.FormularioDeArtefato.ArtifactItemId = "i4b";

        await estado.AdicionarArtefatoAsync();

        _requisicoes.Should().Equal(new[] { $"POST {K(tipo)}/artifacts {{\"artifactItemId\":\"i4b\"}}", $"GET {K(tipo)}/artifacts" }.Concat(AbasDoisETres(tipo)));
        estado.FormularioDeArtefato.ArtifactItemId.Should().BeEmpty();
        estado.PenalidadesVersao.Should().Be(1);
        notificacoes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_refused_artefato_shows_the_message_and_changes_nothing_else()
    {
        var estado = Estado("Personagem");
        _respostas[$"POST {K("Personagem")}/artifacts"] = (HttpStatusCode.BadRequest, "\"Requisitos.\"");
        estado.FormularioDeArtefato.ArtifactItemId = "i4b";

        await estado.AdicionarArtefatoAsync();

        estado.Erro.Should().Be("Não foi possível adicionar o artefato.");
        _requisicoes.Should().ContainSingle();
        estado.FormularioDeArtefato.ArtifactItemId.Should().Be("i4b");
        estado.PenalidadesVersao.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Remover_artefato_deletes_reloads_and_bumps_the_penalties(string tipo)
    {
        var estado = Estado(tipo);

        await estado.RemoverArtefatoAsync("art1");

        _requisicoes.Should().Equal(new[] { $"DELETE {K(tipo)}/artifacts/art1", $"GET {K(tipo)}/artifacts" }.Concat(AbasDoisETres(tipo)));
        estado.PenalidadesVersao.Should().Be(1);
    }

    // ---------------- Afeições ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Carregar_afeicoes_reads_the_sheet_affections(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarAfeicoesAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/affections");
        estado.Afeicoes.Select(a => a.Nome).Should().Equal("Taverneiro");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adicionar_afeicao_posts_resets_and_reloads_the_affections_only(string tipo)
    {
        var estado = Estado(tipo);
        estado.FormularioDeAfeicao.Nome = "Ferreira";
        estado.FormularioDeAfeicao.Favorabilidade = 3;

        await estado.AdicionarAfeicaoAsync();

        _requisicoes.Should().Equal($"POST {K(tipo)}/affections {{\"nome\":\"Ferreira\",\"favorabilidade\":3}}", $"GET {K(tipo)}/affections");
        estado.FormularioDeAfeicao.Nome.Should().BeEmpty();
        estado.FormularioDeAfeicao.Favorabilidade.Should().Be(0);
    }

    [Fact]
    public async Task A_refused_afeicao_shows_the_message_and_reloads_nothing()
    {
        var estado = Estado("Criatura");
        _respostas[$"POST {K("Criatura")}/affections"] = (HttpStatusCode.BadRequest, "");
        estado.FormularioDeAfeicao.Nome = "Ferreira";

        await estado.AdicionarAfeicaoAsync();

        estado.Erro.Should().Be("Não foi possível adicionar a afeição.");
        _requisicoes.Should().ContainSingle();
        estado.FormularioDeAfeicao.Nome.Should().Be("Ferreira");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Remover_afeicao_deletes_and_reloads(string tipo)
    {
        var estado = Estado(tipo);

        await estado.RemoverAfeicaoAsync("afe1");

        _requisicoes.Should().Equal($"DELETE {K(tipo)}/affections/afe1", $"GET {K(tipo)}/affections");
    }

    [Theory]
    [InlineData("Nome", "Taverneira", "{\"nome\":\"Taverneira\",\"favorabilidade\":2}")]
    [InlineData("Favorabilidade", -1, "{\"nome\":\"Taverneiro\",\"favorabilidade\":-1}")]
    [InlineData("Favorabilidade", "x", "{\"nome\":\"Taverneiro\",\"favorabilidade\":2}")]
    public async Task Editar_afeicao_na_linha_puts_the_row_with_the_changed_field(string campo, object valor, string corpo)
    {
        var estado = Estado("Personagem");
        await estado.CarregarAfeicoesAsync();
        _requisicoes.Clear();

        await estado.AtualizarAfeicaoAsync("afe1", campo, valor);

        _requisicoes.Should().Equal($"PUT {K("Personagem")}/affections/afe1 {corpo}", $"GET {K("Personagem")}/affections");
    }

    // ---------------- Busca no Catálogo de Itens ----------------

    [Theory]
    [InlineData(true, "items?nome=T%C3%B3&tipo=Arma")]
    [InlineData(false, "campaigns/c1/available-items?nome=T%C3%B3&tipo=Arma")]
    public async Task Buscar_itens_por_tipo_searches_the_role_catalog(bool gm, string url)
    {
        var estado = Estado("NPC", gm);

        var opcoes = await estado.BuscarItensPorTipoAsync("Tó", "Arma");

        _requisicoes.Should().Equal($"GET {url}");
        opcoes.Should().Equal(new RuinaRPG.Client.Shared.PickerOption("i7", "Tocha"));
    }

    [Theory]
    [InlineData(true, "items?nome=Pr")]
    [InlineData(false, "campaigns/c1/available-items?nome=Pr")]
    public async Task Buscar_todos_os_itens_searches_every_type(bool gm, string url)
    {
        var estado = Estado("Criatura", gm);

        var opcoes = await estado.BuscarTodosOsItensAsync("Pr");

        _requisicoes.Should().Equal($"GET {url}");
        opcoes.Select(o => o.Id).Should().Equal("i7");
    }

    [Fact]
    public async Task A_failed_search_finds_nothing()
    {
        var estado = Estado("Personagem");
        _respostas["GET items?nome=a&tipo=Escudo"] = (HttpStatusCode.Forbidden, "");
        _respostas["GET items?nome=a"] = (HttpStatusCode.Forbidden, "");

        (await estado.BuscarItensPorTipoAsync("a", "Escudo")).Should().BeEmpty();
        (await estado.BuscarTodosOsItensAsync("a")).Should().BeEmpty();
    }

    // Toda mutação de Posses avisa a página: uma filha sem parâmetros da aba (AfeicoesNaLinha, EspoliosSection) só
    // re-renderiza pela cascata da página, não porque a aba re-renderizou.
    [Theory]
    [InlineData("AdicionarAfeicao")]
    [InlineData("RemoverAfeicao")]
    [InlineData("AtualizarAfeicao")]
    [InlineData("AdicionarEspolio")]
    [InlineData("AtualizarQtdDoEspolio")]
    [InlineData("RemoverEspolio")]
    [InlineData("AdicionarItem")]
    [InlineData("AtualizarQtdDoItem")]
    [InlineData("RemoverItem")]
    [InlineData("AdicionarArtefato")]
    [InlineData("RemoverArtefato")]
    public async Task Every_successful_mutation_notifies_the_page(string acao)
    {
        var estado = Estado(acao.Contains("Espolio") ? "Criatura" : "Personagem");
        await estado.CarregarAfeicoesAsync();
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await (acao switch
        {
            "AdicionarAfeicao" => estado.AdicionarAfeicaoAsync(),
            "RemoverAfeicao" => estado.RemoverAfeicaoAsync("afe1"),
            "AtualizarAfeicao" => estado.AtualizarAfeicaoAsync("afe1", "Nome", "Ferreira"),
            "AdicionarEspolio" => estado.AdicionarEspolioAsync(),
            "AtualizarQtdDoEspolio" => estado.AtualizarQtdDoEspolioAsync("sp1", 3),
            "RemoverEspolio" => estado.RemoverEspolioAsync("sp1"),
            "AdicionarItem" => estado.AdicionarItemAoInventarioAsync(),
            "AtualizarQtdDoItem" => estado.AtualizarQtdDoItemAsync("inv1", 3),
            "RemoverItem" => estado.RemoverItemDoInventarioAsync("inv1"),
            "AdicionarArtefato" => estado.AdicionarArtefatoAsync(),
            _ => estado.RemoverArtefatoAsync("art1"),
        });

        estado.Erro.Should().BeNull();
        notificacoes.Should().Be(1);
    }
}
