using System.Net;
using System.Text;
using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A parte Magias &amp; Habilidades do <see cref="FichaEstado"/> (E, Task 7): as Magias/Habilidades e as Runas da ficha,
/// com o Banco do papel (o do GM, ou o da campanha para o jogador), as ações e as recargas na ordem de hoje.
/// </summary>
public class FichaEstadoMagiasTests
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComRunas => new() { "Personagem", "NPC" };

    private static TipoDeFichaCliente Tipo(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private readonly List<string> _requisicoes = new();
    private readonly Dictionary<string, (HttpStatusCode Status, string Corpo)> _respostas = new();

    private const string Magias = """
        [{"id":"m1","nome":"Bola de Fogo","tipo":"Magia","grau":1,"descricao":"Queima.","gastoEmPI":2,"custo":3,"efeitos":[],"criadaNaFicha":true}]
        """;

    private const string BancoDeMagias = """[{"id":"b1","nome":"Raio","tipo":"Magia","grau":2,"descricao":"Fere."}]""";

    private const string Runas = """[{"id":"r1","nome":"Runa do Vento","descricao":"Sopra.","grau":1}]""";

    private const string BancoDeRunas = """[{"id":"rb1","nome":"Runa da Terra","descricao":"Pesa.","grau":2}]""";

    private FichaEstado Estado(string tipo, bool gm = true)
    {
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            var caminho = req.RequestUri!.PathAndQuery.Replace("/api/", "");
            var corpo = req.Content is null ? "" : " " + req.Content.ReadAsStringAsync().Result;
            lock (_requisicoes)
                _requisicoes.Add($"{req.Method} {caminho}{corpo}");
            var resposta = _respostas.TryGetValue($"{req.Method} {caminho}", out var r) ? r
                : caminho.EndsWith("/spell-abilities") ? (HttpStatusCode.OK, Magias)
                : caminho.EndsWith("/runes") ? (HttpStatusCode.OK, Runas)
                : caminho is "spell-ability-bank" or "campaigns/c1/available-spell-abilities" ? (HttpStatusCode.OK, BancoDeMagias)
                : caminho is "rune-bank" or "campaigns/c1/available-runes" ? (HttpStatusCode.OK, BancoDeRunas)
                : (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(resposta.Item1) { Content = new StringContent(resposta.Item2, Encoding.UTF8, "application/json") };
        });
        return new FichaEstado(http, Tipo(tipo), "s1") { IsGmCaller = gm, CampaignId = "c1" };
    }

    private static string K(string tipo) => Tipo(tipo).Url("s1");

    // ---------------- Magias e Habilidades ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Carregar_magias_reads_the_sheet_list_then_the_gm_bank(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarMagiasAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/spell-abilities", "GET spell-ability-bank");
        estado.Magias.Select(m => m.Nome).Should().Equal("Bola de Fogo");
        estado.BancoDeMagias.Select(m => m.Nome).Should().Equal("Raio");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Carregar_magias_as_a_player_reads_the_campaign_bank(string tipo)
    {
        var estado = Estado(tipo, gm: false);

        await estado.CarregarMagiasAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/spell-abilities", "GET campaigns/c1/available-spell-abilities");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adicionar_magia_do_zero_posts_the_form_resets_it_and_reloads_list_bank_and_pi_in_order(string tipo)
    {
        var estado = Estado(tipo);
        estado.RecarregarPontosDeIgnicao = () => { _requisicoes.Add("PI"); return Task.CompletedTask; };
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        estado.FormularioDeMagia.Nome = "Cura";
        estado.FormularioDeMagia.Tipo = "Habilidade";
        estado.FormularioDeMagia.Grau = 1;
        estado.FormularioDeMagia.Descricao = "Fecha feridas.";
        estado.AdicionarEfeitos(new List<EfeitoAdicionadoResult> { new("Luz", null, 2) });

        await estado.AdicionarMagiaAsync();

        _requisicoes.Should().Equal(
            $"POST {K(tipo)}/spell-abilities {{\"sourceBankEntryId\":null,\"nome\":\"Cura\",\"tipo\":\"Habilidade\",\"grau\":1,\"descricao\":\"Fecha feridas.\",\"efeitos\":[{{\"efeitoNome\":\"Luz\",\"quantidade\":null,\"custoPI\":2}}]}}",
            $"GET {K(tipo)}/spell-abilities", "GET spell-ability-bank", "PI");
        estado.FormularioDeMagia.Nome.Should().BeEmpty();
        estado.FormularioDeMagia.Tipo.Should().Be("Magia");
        estado.FormularioDeMagia.Grau.Should().Be(0);
        estado.FormularioDeMagia.Descricao.Should().BeEmpty();
        estado.FormularioDeMagia.Efeitos.Should().BeEmpty();
        notificacoes.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adicionar_magia_do_banco_posts_only_the_entry(string tipo)
    {
        var estado = Estado(tipo);
        estado.FormularioDeMagia.Origem = "Banco";
        estado.FormularioDeMagia.SourceBankEntryId = "b1";
        estado.FormularioDeMagia.Nome = "ignorado";

        await estado.AdicionarMagiaAsync();

        _requisicoes[0].Should().Be($"POST {K(tipo)}/spell-abilities {{\"sourceBankEntryId\":\"b1\",\"nome\":null,\"tipo\":null,\"grau\":null,\"descricao\":null,\"efeitos\":null}}");
        estado.FormularioDeMagia.SourceBankEntryId.Should().BeEmpty();
        estado.FormularioDeMagia.Origem.Should().Be("Banco");
    }

    [Fact]
    public async Task A_refused_magia_shows_the_server_message_keeps_the_form_and_reloads_nothing()
    {
        var estado = Estado("NPC");
        var pi = 0;
        estado.RecarregarPontosDeIgnicao = () => { pi++; return Task.CompletedTask; };
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        _respostas[$"POST {K("NPC")}/spell-abilities"] = (HttpStatusCode.BadRequest, "\"PI insuficiente.\"");
        estado.FormularioDeMagia.Nome = "Cura";

        await estado.AdicionarMagiaAsync();

        estado.Erro.Should().Be("PI insuficiente.");
        _requisicoes.Should().ContainSingle();
        estado.FormularioDeMagia.Nome.Should().Be("Cura");
        pi.Should().Be(0);
        notificacoes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_refused_magia_without_a_message_shows_the_default_one()
    {
        var estado = Estado("Criatura");
        _respostas[$"POST {K("Criatura")}/spell-abilities"] = (HttpStatusCode.InternalServerError, "");

        await estado.AdicionarMagiaAsync();

        estado.Erro.Should().Be("Não foi possível adicionar a magia/habilidade.");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Remover_magia_deletes_and_reloads_list_bank_and_pi(string tipo)
    {
        var estado = Estado(tipo, gm: false);
        estado.RecarregarPontosDeIgnicao = () => { _requisicoes.Add("PI"); return Task.CompletedTask; };

        await estado.RemoverMagiaAsync("m2");

        _requisicoes.Should().Equal($"DELETE {K(tipo)}/spell-abilities/m2",
            $"GET {K(tipo)}/spell-abilities", "GET campaigns/c1/available-spell-abilities", "PI");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task After_an_edit_reloads_list_bank_and_pi(string tipo)
    {
        var estado = Estado(tipo);
        estado.RecarregarPontosDeIgnicao = () => { _requisicoes.Add("PI"); return Task.CompletedTask; };

        await estado.RecarregarMagiasEPontosDeIgnicaoAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/spell-abilities", "GET spell-ability-bank", "PI");
    }

    [Fact]
    public async Task Without_a_pi_hook_the_magia_actions_still_work()
    {
        var estado = Estado("Criatura");

        await estado.RemoverMagiaAsync("m1");

        _requisicoes.Should().Equal($"DELETE {K("Criatura")}/spell-abilities/m1",
            $"GET {K("Criatura")}/spell-abilities", "GET spell-ability-bank");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public void The_magia_addresses_come_from_the_kind(string tipo)
    {
        var estado = Estado(tipo);

        estado.UrlDasMagias.Should().Be($"{K(tipo)}/spell-abilities");
        estado.UrlDaMagia("m1").Should().Be($"{K(tipo)}/spell-abilities/m1");
    }

    [Fact]
    public void The_effect_picker_sees_the_effects_already_in_the_form()
    {
        var estado = Estado("Personagem");

        estado.AdicionarEfeitos(new List<EfeitoAdicionadoResult> { new("Luz", null, 2), new("Dano", 3, 1) });

        estado.EfeitosExistentesParaPicker().Should().Equal(("Luz", (int?)null), ("Dano", (int?)3));
        estado.FormularioDeMagia.Efeitos.Select(e => e.CustoPI).Should().Equal(2, 1);
    }

    // ---------------- Runas ----------------

    [Theory]
    [MemberData(nameof(TiposComRunas))]
    public async Task Carregar_runas_reads_the_sheet_runes_then_the_bank_of_the_role(string tipo)
    {
        var gm = Estado(tipo);
        await gm.CarregarRunasAsync();
        var jogador = Estado(tipo, gm: false);
        await jogador.CarregarRunasAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/runes", "GET rune-bank", $"GET {K(tipo)}/runes", "GET campaigns/c1/available-runes");
        gm.Runas.Select(r => r.Nome).Should().Equal("Runa do Vento");
        gm.BancoDeRunas.Select(r => r.Nome).Should().Equal("Runa da Terra");
    }

    [Theory]
    [MemberData(nameof(TiposComRunas))]
    public async Task Adicionar_runa_do_zero_posts_the_fields_clears_the_form_and_reloads(string tipo)
    {
        var estado = Estado(tipo);
        estado.FormularioDeRuna.Nome = "Runa do Gelo";
        estado.FormularioDeRuna.Descricao = "Congela.";
        estado.FormularioDeRuna.Grau = 2;
        estado.FormularioDeRuna.Disciplina = "Emissao";

        await estado.AdicionarRunaAsync();

        _requisicoes.Should().Equal(
            $"POST {K(tipo)}/runes {{\"nome\":\"Runa do Gelo\",\"descricao\":\"Congela.\",\"grau\":2,\"sourceBankEntryId\":null,\"imageId\":null,\"tipo\":null,\"disciplina\":\"Emissao\"}}",
            $"GET {K(tipo)}/runes", "GET rune-bank");
        estado.FormularioDeRuna.Nome.Should().BeEmpty();
    }

    [Fact]
    public async Task Adicionar_runa_do_banco_posts_only_the_entry()
    {
        var estado = Estado("NPC", gm: false);
        estado.FormularioDeRuna.Origem = "Banco";
        estado.FormularioDeRuna.SourceBankEntryId = "rb1";

        await estado.AdicionarRunaAsync();

        _requisicoes.Should().Equal(
            $"POST {K("NPC")}/runes {{\"nome\":null,\"descricao\":null,\"grau\":null,\"sourceBankEntryId\":\"rb1\",\"imageId\":null,\"tipo\":null,\"disciplina\":null}}",
            $"GET {K("NPC")}/runes", "GET campaigns/c1/available-runes");
    }

    [Fact]
    public async Task An_invalid_runa_shows_what_is_missing_and_sends_nothing()
    {
        var estado = Estado("Personagem");
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await estado.AdicionarRunaAsync();

        estado.Erro.Should().Be("Informe o nome da runa.");
        _requisicoes.Should().BeEmpty();
        notificacoes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task A_refused_runa_shows_the_server_message_and_keeps_the_form()
    {
        var estado = Estado("Personagem");
        _respostas[$"POST {K("Personagem")}/runes"] = (HttpStatusCode.BadRequest, "");
        estado.FormularioDeRuna.Nome = "Runa do Gelo";
        estado.FormularioDeRuna.Disciplina = "Emissao";

        await estado.AdicionarRunaAsync();

        estado.Erro.Should().Be("Não foi possível adicionar a runa.");
        _requisicoes.Should().ContainSingle();
        estado.FormularioDeRuna.Nome.Should().Be("Runa do Gelo");
    }

    [Theory]
    [MemberData(nameof(TiposComRunas))]
    public async Task Remover_runa_deletes_and_reloads(string tipo)
    {
        var estado = Estado(tipo);

        await estado.RemoverRunaAsync("r1");

        _requisicoes.Should().Equal($"DELETE {K(tipo)}/runes/r1", $"GET {K(tipo)}/runes", "GET rune-bank");
    }
}
