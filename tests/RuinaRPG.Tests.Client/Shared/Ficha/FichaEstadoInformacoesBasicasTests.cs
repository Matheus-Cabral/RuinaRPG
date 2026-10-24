using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A parte Informações Básicas do <see cref="FichaEstado"/> (E, Task 10): as Características (lista, busca no catálogo
/// do tipo, adicionar, remover), o Auditor de Regras (cria característica no seletor), as Variantes liberadas, a
/// Habilidade Racial e o envio de uma imagem nova. Os campos auto-salvos ficam no formulário da página.
/// </summary>
public class FichaEstadoInformacoesBasicasTests
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComLinhagem => new() { "Personagem", "NPC" };

    private static TipoDeFichaCliente Tipo(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private readonly List<string> _requisicoes = new();
    private readonly Dictionary<string, (HttpStatusCode Status, string Corpo)> _respostas = new();

    // A lista da Criatura não tem IsRacial (CreatureTraitsListResponse); a do Personagem/NPC tem.
    private const string Caracteristicas = """
        {"positivas":[{"id":"t1","traitId":"tr1","nome":"Corajoso","descricao":"Não foge.","custo":2,"polaridade":"Positiva","especificacao":null,"requerEspecificacao":false,"isRacial":true}],
         "totalPositivas":2,
         "negativas":[{"id":"t2","traitId":"tr2","nome":"Teimoso","descricao":"Não cede.","custo":-1,"polaridade":"Negativa","especificacao":"com nobres","requerEspecificacao":true}],
         "totalNegativas":1,"pontosDisponiveis":5,"pontosDisponiveisNegativas":3}
        """;

    private const string Busca = """[{"id":"tr9","nome":"Ágil","descricao":"Rápido.","custo":3,"polaridade":"Positiva","requerEspecificacao":false}]""";

    private FichaEstado Estado(string tipo, bool gm = true, string? campanha = "c1")
    {
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            var caminho = req.RequestUri!.PathAndQuery.Replace("/api/", "");
            var corpo = req.Content switch
            {
                null => "",
                MultipartFormDataContent partes => " [" + string.Join(", ", partes.Select(p => p is StringContent texto
                    ? $"{p.Headers.ContentDisposition?.Name}={texto.ReadAsStringAsync().Result}"
                    : $"{p.Headers.ContentDisposition?.Name}:{p.Headers.ContentDisposition?.FileName}")) + "]",
                var c => " " + c.ReadAsStringAsync().Result,
            };
            lock (_requisicoes)
                _requisicoes.Add($"{req.Method} {caminho}{corpo}");
            var resposta = _respostas.TryGetValue($"{req.Method} {caminho}", out var r) ? r
                : caminho.EndsWith("/traits") ? (HttpStatusCode.OK, Caracteristicas)
                : caminho.Contains("traits?nome=") ? (HttpStatusCode.OK, Busca)
                : caminho.EndsWith("/variantes-liberadas") ? (HttpStatusCode.OK, """[{"variante":"Laonir","rotulo":"Laonir (Lua)"}]""")
                : caminho.EndsWith("/racial-ability") ? (HttpStatusCode.OK, """{"nome":"Adaptável","descricao":"Aprende rápido.","arcaRolada":null,"arcaEvolucoes":[],"arcaDado":20}""")
                : caminho == "auth/me" ? (HttpStatusCode.OK, """{"id":"u1","nickname":"Mestre","role":"GM","isRulesAuditor":true,"mustChangePassword":false}""")
                : caminho == "images" ? (HttpStatusCode.OK, """{"id":"img-nova","url":"/img/nova.png"}""")
                : (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(resposta.Item1) { Content = new StringContent(resposta.Item2, Encoding.UTF8, "application/json") };
        });
        return new FichaEstado(http, Tipo(tipo), "s1") { IsGmCaller = gm, CampaignId = campanha };
    }

    private static string K(string tipo) => Tipo(tipo).Url("s1");

    // ---------------- Características ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Carregar_caracteristicas_reads_the_sheet_list(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarCaracteristicasAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/traits");
        estado.Caracteristicas!.Positivas.Select(t => t.Nome).Should().Equal("Corajoso");
        estado.Caracteristicas.Negativas.Select(t => t.Especificacao).Should().Equal("com nobres");
        (estado.Caracteristicas.TotalPositivas, estado.Caracteristicas.TotalNegativas, estado.Caracteristicas.PontosDisponiveis, estado.Caracteristicas.PontosDisponiveisNegativas)
            .Should().Be((2, 1, 5, 3));
    }

    // A Criatura não tem Característica Racial: a lista dela passa pelo adaptador (IsRacial false).
    [Theory]
    [InlineData("Personagem", true)]
    [InlineData("NPC", true)]
    [InlineData("Criatura", false)]
    public async Task Only_kinds_with_racial_traits_read_the_racial_mark(string tipo, bool racial)
    {
        var estado = Estado(tipo);

        await estado.CarregarCaracteristicasAsync();

        estado.Caracteristicas!.Positivas.Single().IsRacial.Should().Be(racial);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adicionar_caracteristica_posts_resets_the_form_reloads_and_notifies(string tipo)
    {
        var estado = Estado(tipo);
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        estado.FormularioDeCaracteristica.TraitId = "tr9";
        estado.FormularioDeCaracteristica.Especificacao = "com lobos";

        await estado.AdicionarCaracteristicaAsync();

        _requisicoes.Should().Equal($"POST {K(tipo)}/traits {{\"traitId\":\"tr9\",\"especificacao\":\"com lobos\"}}", $"GET {K(tipo)}/traits");
        estado.FormularioDeCaracteristica.TraitId.Should().BeEmpty();
        estado.FormularioDeCaracteristica.Especificacao.Should().BeEmpty();
        estado.Caracteristicas.Should().NotBeNull();
        estado.Erro.Should().BeNull();
        notificacoes.Should().Be(1);
    }

    [Theory]
    [InlineData("\"Essa característica exige especificação.\"", "Essa característica exige especificação.")]
    [InlineData("", "Não foi possível adicionar a característica.")]
    public async Task A_refused_caracteristica_shows_the_message_keeps_the_form_and_reloads_nothing(string corpo, string mensagem)
    {
        var estado = Estado("NPC");
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        _respostas[$"POST {K("NPC")}/traits"] = (HttpStatusCode.BadRequest, corpo);
        estado.FormularioDeCaracteristica.TraitId = "tr9";

        await estado.AdicionarCaracteristicaAsync();

        estado.Erro.Should().Be(mensagem);
        _requisicoes.Should().ContainSingle();
        estado.FormularioDeCaracteristica.TraitId.Should().Be("tr9");
        notificacoes.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Remover_caracteristica_deletes_reloads_and_notifies(string tipo)
    {
        var estado = Estado(tipo);
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await estado.RemoverCaracteristicaAsync("t2");

        _requisicoes.Should().Equal($"DELETE {K(tipo)}/traits/t2", $"GET {K(tipo)}/traits");
        notificacoes.Should().Be(1);
    }

    // O catálogo de cada tipo: Personagem e NPC buscam em traits, a Criatura só no exclusivo dela (Ficha de Criaturas 5.d).
    [Theory]
    [InlineData("Personagem", "traits")]
    [InlineData("NPC", "traits")]
    [InlineData("Criatura", "creature-exclusive-traits")]
    public async Task Buscar_caracteristicas_searches_the_catalog_of_the_kind(string tipo, string catalogo)
    {
        var estado = Estado(tipo);

        var opcoes = await estado.BuscarCaracteristicasAsync("ág il");

        _requisicoes.Should().Equal($"GET {catalogo}?nome=%C3%A1g%20il");
        opcoes.Should().ContainSingle();
        opcoes[0].Id.Should().Be("tr9");
        opcoes[0].Label.Should().Be("Ágil (3, Positiva)");
    }

    [Fact]
    public async Task A_failed_search_finds_nothing()
    {
        var estado = Estado("Criatura");
        _respostas["GET creature-exclusive-traits?nome=x"] = (HttpStatusCode.InternalServerError, "");

        (await estado.BuscarCaracteristicasAsync("x")).Should().BeEmpty();
    }

    // ---------------- Auditor de Regras (cria característica no seletor) ----------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Carregar_auditor_reads_the_flag_from_auth_me(bool auditor)
    {
        var estado = Estado("Criatura");
        _respostas["GET auth/me"] = (HttpStatusCode.OK, $$"""{"id":"u1","nickname":"X","role":"GM","isRulesAuditor":{{(auditor ? "true" : "false")}},"mustChangePassword":false}""");

        await estado.CarregarAuditorDeRegrasAsync();

        _requisicoes.Should().Equal("GET auth/me");
        estado.EhAuditorDeRegras.Should().Be(auditor);
    }

    [Fact]
    public async Task Without_auth_me_nobody_is_auditor()
    {
        var estado = Estado("Criatura");
        _respostas["GET auth/me"] = (HttpStatusCode.Unauthorized, "");

        await estado.CarregarAuditorDeRegrasAsync();

        estado.EhAuditorDeRegras.Should().BeFalse();
    }

    // ---------------- Variantes liberadas e Habilidade Racial (Personagem e NPC) ----------------

    [Theory]
    [MemberData(nameof(TiposComLinhagem))]
    public async Task Carregar_variantes_liberadas_reads_the_sheet_list(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarVariantesLiberadasAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/variantes-liberadas");
        estado.VariantesLiberadas.Select(v => v.Rotulo).Should().Equal("Laonir (Lua)");
    }

    [Theory]
    [MemberData(nameof(TiposComLinhagem))]
    public async Task Carregar_habilidade_racial_reads_the_sheet_ability(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarHabilidadeRacialAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/racial-ability");
        estado.HabilidadeRacial!.Nome.Should().Be("Adaptável");
    }

    // ---------------- envio de imagem ----------------

    private sealed class ArquivoFalso(string nome) : IBrowserFile
    {
        public string Name => nome;
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 8;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream(Encoding.UTF8.GetBytes("conteudo"));
    }

    [Theory]
    [InlineData(true, "c1", "POST images [file:foto.png]")]
    [InlineData(false, "c1", "POST images [file:foto.png, campaignId=c1]")]
    [InlineData(false, null, "POST images [file:foto.png]")]
    public async Task Enviar_imagem_uploads_it_where_the_caller_may_and_adds_it_to_the_gallery(bool gm, string? campanha, string requisicao)
    {
        var estado = Estado("NPC", gm, campanha);
        estado.MinhasImagens = new() { new ImageSummaryResponse("img1", "/img/um.png", DateTime.UtcNow) };

        var enviada = await estado.EnviarImagemAsync(new ArquivoFalso("foto.png"));

        _requisicoes.Should().Equal(requisicao);
        enviada.Should().Be(new ImageUploadResponse("img-nova", "/img/nova.png"));
        estado.MinhasImagens.Select(i => i.Id).Should().Equal("img-nova", "img1");
        estado.Erro.Should().BeNull();
    }

    [Fact]
    public async Task A_refused_upload_shows_the_message_and_returns_nothing()
    {
        var estado = Estado("Personagem");
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        _respostas["POST images"] = (HttpStatusCode.BadRequest, "\"grande demais\"");

        var enviada = await estado.EnviarImagemAsync(new ArquivoFalso("foto.png"));

        enviada.Should().BeNull();
        estado.Erro.Should().Be("Não foi possível enviar a imagem.");
        estado.MinhasImagens.Should().BeEmpty();
        notificacoes.Should().Be(1);
    }
}
