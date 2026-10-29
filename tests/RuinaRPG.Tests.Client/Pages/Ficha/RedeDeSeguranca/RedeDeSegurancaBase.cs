using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;

/// <summary>
/// Rede de segurança do E (consolidação do cliente): renderiza a página inteira de cada tipo de ficha com
/// dados ricos (uma linha em cada lista) e grava cada requisição HTTP. As redes afirmam sobre o texto visível
/// e as requisições, nunca sobre a estrutura dos componentes, para sobreviver à divisão das páginas em
/// componentes compartilhados. O HTTP falso responde com JSON cru (objetos anônimos), nunca com tipos de
/// Contracts, para que a fusão dos records (E.2) não obrigue a mexer aqui.
/// </summary>
public abstract class RedeDeSegurancaBase : MudBunitContext
{
    protected const string SheetId = "s1";

    private sealed class FakeAuthStateProvider(string papel) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, papel) }, "test"))));
    }

    private readonly List<string> _requisicoes = new();
    private readonly List<(string Padrao, object? Json)> _respostas = new();
    private string _tipo = "Personagem";
    private string _papel = "GM";

    /// <summary>
    /// Cada requisição como <c>"{METHOD} {caminho}"</c> (sem o prefixo <c>/api</c>, com a query) e, fora o GET,
    /// o corpo em JSON compacto com as chaves em ordem alfabética (a ordem das propriedades no fio pode mudar
    /// no E.2; os nomes e os valores não). Um corpo multipart entra como as partes: o arquivo pelo nome, o texto com o valor: <c>[file, campaignId=c1]</c>.
    /// </summary>
    protected IReadOnlyList<string> Requisicoes
    {
        get
        {
            lock (_requisicoes)
                return _requisicoes.ToList();
        }
    }

    protected void LimparRequisicoes()
    {
        lock (_requisicoes)
            _requisicoes.Clear();
    }

    /// <summary>
    /// Troca a resposta de um endpoint (vale para qualquer método). <paramref name="sufixoOuRegex"/> é um
    /// sufixo do caminho (sem <c>/api</c>), ou uma regex quando começa com <c>^</c> ou termina com <c>$</c>.
    /// <paramref name="json"/> é um objeto (serializado) ou uma string com JSON cru; <c>null</c> responde 404.
    /// A última chamada para um caminho ganha.
    /// </summary>
    protected void Responder(string sufixoOuRegex, object? json) => _respostas.Insert(0, (sufixoOuRegex, json));

    /// <summary>
    /// Como <see cref="Responder"/>, mas recusa: responde <paramref name="status"/> com <paramref name="mensagem"/>
    /// como corpo (uma string JSON, como o servidor manda as mensagens de erro).
    /// </summary>
    protected void ResponderErro(string sufixoOuRegex, HttpStatusCode status, string mensagem) =>
        _respostas.Insert(0, (sufixoOuRegex, new Erro(status, mensagem)));

    private sealed record Erro(HttpStatusCode Status, string Mensagem);

    /// <summary>"character-sheets", "npc-sheets" ou "creature-sheets".</summary>
    protected static string Prefixo(string tipo) => tipo switch
    {
        "Personagem" => "character-sheets",
        "NPC" => "npc-sheets",
        "Criatura" => "creature-sheets",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    // Personagem sempre mostra as Condições; NPC e Criatura só quando concedidos a um jogador (os do GM nem perguntam).
    protected static bool MostraCondicoes(string tipo, string papel) => tipo == "Personagem" || papel == "Jogador";

    /// <summary>As três primeiras requisições da carga, em sequência: Perícias, a ficha (diz a campanha) e a galeria de imagens.</summary>
    protected static string[] InicioDaCarga(string tipo, string papel) =>
        new[] { "GET /pericias", $"GET /{Prefixo(tipo)}/s1", papel == "GM" ? "GET /images/mine" : "GET /campaigns/c1/available-images" };

    /// <summary>O resto da carga, pedido em paralelo (<c>Task.WhenAll</c>): vale o conjunto, não a ordem.</summary>
    protected static string[] CargaInicial(string tipo, string papel)
    {
        var k = $"/{Prefixo(tipo)}/s1";
        var jogador = papel == "Jogador";
        var lista = new List<string>
        {
            $"GET {k}/level-up-notice", $"GET {k}/attributes", $"GET {k}/attributes/budget", $"GET {k}/skills",
        };
        if (tipo == "Personagem")
            lista.Add($"GET {k}/skills/budget");
        if (tipo != "Criatura")
            lista.Add($"GET {k}/affinities");
        lista.AddRange(new[] { $"GET {k}/weapons", $"GET {k}/armor-slots", $"GET {k}/shields", $"GET {k}/sub-attributes", $"GET {k}/modificador-de-dano" });
        if (tipo != "Criatura")
            lista.AddRange(new[] { $"GET {k}/racial-ability", $"GET {k}/variantes-liberadas" });
        lista.Add($"GET {k}/spell-abilities");
        lista.Add(jogador ? "GET /campaigns/c1/available-spell-abilities" : "GET /spell-ability-bank");
        if (tipo != "Criatura")
            lista.AddRange(new[] { $"GET {k}/runes", jogador ? "GET /campaigns/c1/available-runes" : "GET /rune-bank" });
        lista.Add($"GET {k}/masteries");
        lista.Add(tipo == "Criatura" ? $"GET {k}/spoils" : $"GET {k}/inventory");
        lista.AddRange(new[] { $"GET {k}/artifacts", $"GET {k}/affections", $"GET {k}/traits" });
        if (tipo == "Personagem")
            lista.AddRange(new[] { $"GET {k}/diary", jogador ? "GET /campaigns/mine" : "GET /campaigns" });
        if (tipo != "Criatura")
            lista.Add($"GET {k}/racial-traits/pending");
        if (tipo == "Criatura")
            lista.Add("GET /auth/me");
        if (MostraCondicoes(tipo, papel))
            lista.Add($"GET {k}/condicoes-ativas");
        return lista.ToArray();
    }

    /// <summary>
    /// O que a página relê (em sequência, nesta ordem) depois de mexer em Atributos, Perícias, Afinidades ou no
    /// Combate (<c>LoadTabs2And3Async</c>).
    /// </summary>
    protected static string[] RecargaDasAbas(string tipo)
    {
        var k = $"/{Prefixo(tipo)}/s1";
        var lista = new List<string> { $"GET {k}/attributes", $"GET {k}/attributes/budget", $"GET {k}/skills" };
        if (tipo == "Personagem")
            lista.Add($"GET {k}/skills/budget");
        if (tipo != "Criatura")
            lista.Add($"GET {k}/affinities");
        lista.AddRange(new[] { $"GET {k}/weapons", $"GET {k}/armor-slots", $"GET {k}/shields", $"GET {k}/sub-attributes", $"GET {k}/modificador-de-dano" });
        return lista.ToArray();
    }

    /// <summary>A tabela (dentro de <paramref name="raiz"/>) cujo primeiro cabeçalho é <paramref name="primeiroCabecalho"/>.</summary>
    protected static IElement Tabela(IElement raiz, string primeiroCabecalho) =>
        raiz.QuerySelectorAll("table").Single(t => t.QuerySelector("th") is { } th && TextNormalization.Collapse(th.TextContent) == primeiroCabecalho);

    /// <summary>A linha do corpo de <paramref name="tabela"/> cuja primeira célula é <paramref name="primeiraCelula"/>.</summary>
    protected static IElement Linha(IElement tabela, string primeiraCelula) =>
        tabela.QuerySelectorAll("tbody tr").Single(tr => tr.QuerySelector("td") is { } td && TextNormalization.Collapse(td.TextContent) == primeiraCelula);

    /// <summary>O botão de <paramref name="raiz"/> com o texto visível <paramref name="texto"/> (tem de ser um só).</summary>
    protected static IElement Botao(IElement raiz, string texto) =>
        raiz.QuerySelectorAll("button").Single(b => TextNormalization.Collapse(b.TextContent) == texto);

    /// <summary>
    /// Renderiza a página do tipo (<c>"Personagem"</c>, <c>"NPC"</c>, <c>"Criatura"</c>) com SheetId "s1",
    /// ao lado dos provedores de diálogo e popover, e espera a carga terminar. <paramref name="papel"/> é
    /// "GM" ou "Jogador"; para o jogador, a ficha de NPC/Criatura é uma concedida a ele. A espera é positiva:
    /// até chegarem as requisições da carga (<see cref="InicioDaCarga"/> + <see cref="CargaInicial"/>, ou
    /// <paramref name="cargaEsperada"/> quando um <see cref="Responder"/> muda a carga), e então nenhuma a mais.
    /// </summary>
    protected async Task<IRenderedComponent<IComponent>> RenderFichaAsync(string tipo, string papel = "GM", IEnumerable<string>? cargaEsperada = null)
    {
        _tipo = tipo;
        _papel = papel;
        Prefixo(tipo);
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(ResponderRequisicao));
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider(papel));

        var pagina = tipo switch
        {
            "Personagem" => typeof(FichaDePersonagem),
            "NPC" => typeof(FichaDeNpc),
            _ => typeof(FichaDeCriatura),
        };
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent(2, pagina);
            builder.AddAttribute(3, "SheetId", SheetId);
            builder.CloseComponent();
        });
        var esperada = (cargaEsperada ?? InicioDaCarga(tipo, papel).Concat(CargaInicial(tipo, papel))).ToList();
        await EsperarRequisicoesAsync(cut, todas => todas.Should().BeEquivalentTo(esperada));
        return (IRenderedComponent<IComponent>)(object)cut;
    }

    /// <summary>
    /// O limite das esperas da rede (R-E4). É infraestrutura de teste, não asserção: com a suíte inteira rodando em
    /// paralelo, uma espera de 5 s estourava de vez em quando sem nada errado (a lista esperada não muda). Um teste
    /// realmente travado só demora mais para falhar.
    /// </summary>
    protected static readonly TimeSpan LimiteDeEspera = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Espera (no máximo <paramref name="limite"/>, <see cref="LimiteDeEspera"/> por padrão; estoura com a falha da asserção) até
    /// <paramref name="afirmar"/> passar sobre <see cref="Requisicoes"/>, depois espera a calmaria e afirma de novo,
    /// para que nenhuma requisição a mais passe despercebida nem vaze para o passo seguinte.
    /// </summary>
    protected async Task EsperarRequisicoesAsync<TComponente>(IRenderedComponent<TComponente> cut, Action<IReadOnlyList<string>> afirmar, TimeSpan? limite = null)
        where TComponente : IComponent
    {
        cut.WaitForAssertion(() => afirmar(Requisicoes), limite ?? LimiteDeEspera);
        await EsperarCalmariaAsync();
        afirmar(Requisicoes);
    }

    /// <summary>
    /// Só a verificação final de "nada a mais": espera <paramref name="quietoMs"/> sem requisição nova. Não serve
    /// para esperar uma carga (use <see cref="EsperarRequisicoesAsync"/>); se as requisições não param em
    /// <see cref="LimiteDeEspera"/>, estoura.
    /// </summary>
    protected async Task EsperarCalmariaAsync(int quietoMs = 100)
    {
        var contagem = Requisicoes.Count;
        var quieto = 0;
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        while (quieto < quietoMs)
        {
            if (relogio.Elapsed > LimiteDeEspera)
                throw new TimeoutException($"As requisições não pararam em {LimiteDeEspera.TotalSeconds} s ({Requisicoes.Count} até agora).");
            await Task.Delay(25);
            var agora = Requisicoes.Count;
            quieto = agora == contagem ? quieto + 25 : 0;
            contagem = agora;
        }
    }

    protected static async Task IrParaAbaAsync(IRenderedComponent<IComponent> cut, string aba)
    {
        var cabecalho = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == aba);
        await cut.InvokeAsync(() => cabecalho.Click());
    }

    protected static string TextoNormalizado(IElement e) => TextNormalization.Collapse(e.TextContent);

    /// <summary>
    /// O que se vê no elemento, em ordem do DOM: cada trecho de texto (espaços colapsados) e, para cada campo,
    /// <c>«valor»</c> (<c>[x]</c>/<c>[ ]</c> para caixa de seleção). Ícones e atributos não entram.
    /// </summary>
    protected static List<string> Leitura(IElement raiz)
    {
        var saida = new List<string>();
        void Visitar(INode no)
        {
            switch (no)
            {
                case IText texto:
                    var t = TextNormalization.Collapse(texto.Data);
                    if (t.Length > 0)
                        saida.Add(t);
                    break;
                case IElement el when el.LocalName is "script" or "style" or "svg":
                    break;
                case IElement el when el.LocalName == "input":
                    var tipo = el.GetAttribute("type");
                    if (tipo is "checkbox" or "radio")
                        saida.Add(el.HasAttribute("checked") ? "[x]" : "[ ]");
                    else if (tipo != "hidden" && tipo != "file")
                        saida.Add($"«{el.GetAttribute("value")}»");
                    break;
                case IElement el when el.LocalName == "textarea":
                    saida.Add($"«{el.GetAttribute("value") ?? el.TextContent}»");
                    break;
                default:
                    foreach (var filho in no.ChildNodes)
                        Visitar(filho);
                    break;
            }
        }
        Visitar(raiz);
        return saida;
    }

    /// <summary>O conteúdo da aba aberta.</summary>
    protected static IElement AbaAberta(IRenderedComponent<IComponent> cut) => cut.Find(".mud-tabs-panels");

    /// <summary>O &lt;input&gt; do campo MudBlazor cujo rótulo visível é <paramref name="rotulo"/>, dentro de <paramref name="raiz"/>.</summary>
    protected static IElement Campo(IElement raiz, string rotulo)
    {
        var label = raiz.QuerySelectorAll("label.mud-input-label").Single(l => TextNormalization.Collapse(l.TextContent) == rotulo);
        var controle = label.Closest(".mud-input-control") ?? throw new InvalidOperationException($"Campo {rotulo} sem .mud-input-control");
        return controle.QuerySelector("input") ?? throw new InvalidOperationException($"Campo {rotulo} sem input");
    }

    /// <summary>Requisições a partir da primeira que começa com <paramref name="inicio"/> (inclusive).</summary>
    protected List<string> RequisicoesDesde(string inicio)
    {
        var todas = Requisicoes;
        var i = todas.ToList().FindIndex(r => r.StartsWith(inicio, StringComparison.Ordinal));
        return i < 0 ? new() : todas.Skip(i).ToList();
    }

    /// <summary>Abre o MudSelect de rótulo <paramref name="rotulo"/> dentro de <paramref name="raiz"/> e clica na opção <paramref name="opcao"/>.</summary>
    protected static async Task EscolherOpcaoAsync(IRenderedComponent<IComponent> cut, Func<IElement> raiz, string rotulo, string opcao)
    {
        var label = raiz().QuerySelectorAll("label.mud-input-label").Single(l => TextoNormalizado(l) == rotulo);
        await cut.InvokeAsync(() => label.Closest(".mud-input-control")!.MouseDown());
        var item = cut.WaitForElements(".mud-popover-open .mud-list-item").Single(li => TextoNormalizado(li) == opcao);
        await cut.InvokeAsync(() => item.Click());
    }

    /// <summary>
    /// Busca (texto vazio) e escolhe um item no seletor de catálogo (<c>EntityPicker</c>, também o de dentro do
    /// <c>CatalogoItemPicker</c>) número <paramref name="indice"/> da página, na ordem do DOM.
    /// </summary>
    protected static async Task EscolherNoSeletorAsync(IRenderedComponent<IComponent> cut, int indice, string itemId, string nome)
    {
        var seletor = cut.FindComponents<RuinaRPG.Client.Shared.EntityPicker>()[indice];
        await cut.InvokeAsync(() => seletor.Instance.SearchAsyncForTests(""));
        await cut.InvokeAsync(() => seletor.Instance.SelectForTests(new RuinaRPG.Client.Shared.PickerOption(itemId, nome)));
    }

    private (System.Globalization.CultureInfo Cultura, System.Globalization.CultureInfo CulturaDaInterface)? _culturaOriginal;

    /// <summary>
    /// Fixa a cultura do teste em pt-BR (a do público do app; a página formata números e datas com a cultura
    /// corrente do navegador). Chamado no corpo do teste, a cultura vale para o resto dele (e para a
    /// renderização). A cultura de antes volta no <see cref="Dispose(bool)"/>, então o teste não depende de ser
    /// assíncrono (num método <c>async</c> a mudança já não escaparia dele) para não vazar a cultura.
    /// </summary>
    protected void FixarCulturaPtBr()
    {
        _culturaOriginal ??= (System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CultureInfo.CurrentUICulture);
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("pt-BR");
    }

    protected override void Dispose(bool disposing)
    {
        if (_culturaOriginal is { } original)
        {
            System.Globalization.CultureInfo.CurrentCulture = original.Cultura;
            System.Globalization.CultureInfo.CurrentUICulture = original.CulturaDaInterface;
            _culturaOriginal = null;
        }
        base.Dispose(disposing);
    }

    /// <summary>O <c>.mud-dialog</c> aberto (tem de ser um só).</summary>
    protected static IElement Dialogo(IRenderedComponent<IComponent> cut) => cut.WaitForElements(".mud-dialog").Single();

    // ---- HTTP falso ----

    private HttpResponseMessage ResponderRequisicao(HttpRequestMessage request)
    {
        var caminho = request.RequestUri!.AbsolutePath;
        if (caminho.StartsWith("/api", StringComparison.Ordinal))
            caminho = caminho[4..];
        var registro = $"{request.Method} {caminho}{request.RequestUri.Query}";
        if (request.Method != HttpMethod.Get && request.Content is { } conteudo
            && conteudo.Headers.ContentType?.MediaType == "application/json")
            registro += " " + JsonOrdenado(conteudo.ReadAsStringAsync().GetAwaiter().GetResult());
        else if (request.Content is MultipartFormDataContent partes)
            registro += " [" + string.Join(", ", partes.Select(p => p is StringContent texto
                ? $"{p.Headers.ContentDisposition?.Name?.Trim('"')}={texto.ReadAsStringAsync().GetAwaiter().GetResult()}"
                : p.Headers.ContentDisposition?.Name?.Trim('"'))) + "]";
        lock (_requisicoes)
            _requisicoes.Add(registro);

        foreach (var (padrao, json) in _respostas)
            if (Casa(padrao, caminho))
                return Resposta(json);

        if (request.Method != HttpMethod.Get)
            return request.Method == HttpMethod.Post && caminho == "/images"
                ? Resposta(new { id = "img-nova", url = "/img/nova.png" })
                : new HttpResponseMessage(HttpStatusCode.OK);
        return Resposta(Padrao(caminho));
    }

    private static bool Casa(string padrao, string caminho) =>
        padrao.StartsWith('^') || padrao.EndsWith('$') ? Regex.IsMatch(caminho, padrao) : caminho.EndsWith(padrao, StringComparison.Ordinal);

    private static HttpResponseMessage Resposta(object? json) => json switch
    {
        null => new HttpResponseMessage(HttpStatusCode.NotFound),
        Erro erro => new HttpResponseMessage(erro.Status)
        {
            Content = new StringContent(JsonSerializer.Serialize(erro.Mensagem), Encoding.UTF8, "application/json"),
        },
        string cru => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(cru, Encoding.UTF8, "application/json") },
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(json), Encoding.UTF8, "application/json"),
        },
    };

    private static string JsonOrdenado(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;
        return Ordenar(JsonNode.Parse(json))?.ToJsonString(SemEscape) ?? "null";
    }

    private static readonly JsonSerializerOptions SemEscape = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static JsonNode? Ordenar(JsonNode? no) => no switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Ordenar(p.Value)))),
        JsonArray arr => new JsonArray(arr.Select(Ordenar).ToArray()),
        null => null,
        _ => JsonNode.Parse(no.ToJsonString()),
    };

    // ---- dados padrão: uma linha em cada lista ----

    private bool EhJogador => _papel != "GM";

    private object? Padrao(string caminho)
    {
        var ficha = $"/{Prefixo(_tipo)}/{SheetId}";
        if (caminho == ficha)
            return Ficha();
        if (!caminho.StartsWith(ficha + "/", StringComparison.Ordinal))
            return Global(caminho);

        return caminho[(ficha.Length + 1)..] switch
        {
            "level-up-notice" => null,
            "condicoes-ativas" => Array.Empty<object>(),
            "equipment-penalties" => Array.Empty<object>(),
            "attributes" => new[] { new { atributo = "Vigor", gasto = 2, bonus = 1, temMaestria = false, total = 3 } },
            "attributes/budget" => new { gastoTotal = 2, pontosDisponiveis = 10 },
            "skills" => new[] { new { pericia = "Atletismo", gasto = 2, modificador = 1, atributoEscolhido = "Vigor", total = 3, contaHistorico = true, nome = "Atletismo", descricao = "Correr e saltar." } },
            "skills/budget" => new { gastoTotal = 2, pontosDisponiveis = 8 },
            "affinities" => new[] { new { id = "afi1", elemento = "Fogo", elementoValor = 2, segundaEssencia = (string?)null, segundaEssenciaValor = (int?)null, subElemento = (string?)null, subElementoValor = (int?)null, experiencia = 1, elementoValorComCondicoes = 2 } },
            "weapons" => new[]
            {
                new
                {
                    id = "w1", itemId = "i1", nome = "Espada Longa", tipoDeDano = "Cortante", alcance = 1, dados = "1d8", dano = 3, critico = "19-20", rank = "C",
                    peso = 1.5m, isEquipped = true, durabilidadeAtual = 8, durabilidadeMaxima = 10, durabilidadeMaximo = 10, descricao = "Uma espada reta.",
                },
            },
            "armor-slots" => new object[]
            {
                new { slot = "Capacete" },
                new
                {
                    slot = "Superior", itemId = "a1", nome = "Cota de Malha", categoria = "Media", defesa = 3, rf = 2, rm = 1, peso = 8m,
                    durabilidadeAtual = 5, durabilidadeMaxima = 6, durabilidadeMaximo = 6, descricao = "Anéis de ferro.",
                },
                new { slot = "Inferior" },
            },
            "shields" => new[] { new { id = "sh1", itemId = "i3", nome = "Broquel", categoria = "Leve", bonusDefesa = 1, peso = 2m, isEquipped = false, durabilidadeAtual = 4, durabilidadeMaxima = 4, descricao = "Escudo pequeno." } },
            "sub-attributes" => new
            {
                iniciativa = 5, movimentacao = 9, esquivaNatural = 11, defesaNatural = 13, reducaoFisica = 2, reducaoMagica = 1,
                pesoAtual = 12.5m, pesoMaximo = 40m, eficienciaElemental = 3, danoElemental = 2, bonusDeCargaDaCampanha = 0m,
            },
            "modificador-de-dano" => new { cortante = 1, perfurante = 0, contundente = 0, arcano = 0, danoElemental = new Dictionary<string, int>() },
            "racial-ability" => new { nome = "Adaptável", descricao = "Aprende rápido.", arcaRolada = (int?)null, arcaEvolucoes = Array.Empty<object>(), arcaDado = 20 },
            "variantes-liberadas" => Array.Empty<object>(),
            "spell-abilities" => new object[]
            {
                new { id = "m1", nome = "Bola de Fogo", tipo = "Magia", grau = 1, gastoEmPI = 2, custo = 3, descricao = "Queima.", efeitos = Array.Empty<object>(), criadaNaFicha = true },
                new { id = "m2", nome = "Golpe Duplo", tipo = "Habilidade", grau = 2, gastoEmPI = 0, custo = 1, descricao = "Dois golpes.", efeitos = Array.Empty<object>(), criadaNaFicha = false },
                new { id = "p1", nome = "Pele de Ferro", tipo = "Passiva", grau = 1, gastoEmPI = 0, custo = 0, descricao = "Dura.", efeitos = Array.Empty<object>(), categoria = "Defesa" },
            },
            "spell-abilities/passivas-disponiveis" => new[]
            {
                new
                {
                    entrada = new { id = "pb1", nome = "Couraça", tipo = "Passiva", grau = 1, gastoEmPI = 0, custo = 0, descricao = "Rija.", efeitos = Array.Empty<object>(), deCriatura = false, categoria = "Defesa" },
                    pendencias = Array.Empty<string>(),
                },
            },
            "runes" => new[] { new { id = "r1", nome = "Runa do Vento", descricao = "Sopra.", grau = 1, tipo = "Arcana", disciplina = "Emissao" } },
            "masteries" => new[] { new { id = "ma1", nome = "Golpe Firme", pericia = "Atletismo", atributo = "Vigor", gastoMaestria = 1, total = 4 } },
            "inventory" => new[] { new { id = "inv1", itemId = "i5", nome = "Corda", peso = 1m, qtd = 2, total = 2m, descricao = "Dez metros." } },
            "artifacts" => new[] { new { id = "art1", artifactItemId = "i4", nome = "Amuleto", tipoDeAlvo = "Atributo", alvo = "Vigor", valor = 1, descricao = "Brilha." } },
            "affections" => new[] { new { id = "afe1", nome = "Taverneiro", favorabilidade = 2 } },
            "traits" => new
            {
                positivas = new[] { new { id = "t1", traitId = "tr1", nome = "Corajoso", descricao = "Não foge.", custo = 2, polaridade = "Positiva", especificacao = (string?)null, requerEspecificacao = false, isRacial = false } },
                totalPositivas = 2,
                negativas = new[] { new { id = "t2", traitId = "tr2", nome = "Teimoso", descricao = "Não cede.", custo = -1, polaridade = "Negativa", especificacao = "com nobres", requerEspecificacao = true, isRacial = false } },
                totalNegativas = 1,
                pontosDisponiveis = 5,
                pontosDisponiveisNegativas = 3,
            },
            "racial-traits/pending" => new { pending = false, gratuita = Array.Empty<object>(), obrigatoria = Array.Empty<object>() },
            "spoils" => new[] { new { id = "sp1", itemId = "i6", nome = "Presa de Lobo", custo = 5, qtd = 2, custoTotal = 10, dt = 12, descricao = "Afiada." } },
            "diary" => new[] { new { id = "d1", texto = "Chegamos à vila.", createdAt = "2026-10-01T12:00:00Z", imageUrls = Array.Empty<string>() } },
            "historia/imagens" => Array.Empty<object>(),
            "equipagem/kits" => Array.Empty<object>(),
            _ => Array.Empty<object>(),
        };
    }

    private object? Global(string caminho) => caminho switch
    {
        "/pericias" => new[] { new { id = 1, chave = "Atletismo", nome = "Atletismo", descricao = "Correr e saltar.", atributoSugerido = "Vigor", disponivelParaCriaturas = true, protegida = false } },
        "/auth/me" => new { id = "u1", nickname = "Mestre", role = _papel, isRulesAuditor = !EhJogador, pendingChangelogVersion = (string?)null, mustChangePassword = false },
        // Uma coluna acumulativa (2 por nível) para a "Progressão do nível" mostrar um valor que depende do Nível da ficha.
        "/tabela-de-niveis" => new
        {
            colunas = new[] { new { id = "00000000-0000-0000-0000-0000000000a1", nome = "Pontos de Perícia", tipo = "Acumulativa", chaveDeSistema = "PontosDePericia", doSistema = true, ordem = 1 } },
            linhas = Enumerable.Range(1, 5).Select(n => new { nivel = n, outrosBonus = (string?)null, valores = new Dictionary<string, int?> { ["00000000-0000-0000-0000-0000000000a1"] = 2 } }).ToArray(),
            mostrarLimitesNoLivro = false,
        },
        "/spell-ability-bank" or "/campaigns/c1/available-spell-abilities" => new[]
        {
            new { id = "b1", nome = "Raio", tipo = "Magia", grau = 2, gastoEmPI = 3, custo = 2, descricao = "Zap.", efeitos = Array.Empty<object>(), deCriatura = false },
        },
        "/rune-bank" or "/campaigns/c1/available-runes" => new[]
        {
            new { id = "rb1", nome = "Runa da Terra", descricao = "Firme.", grau = 2, imageId = (string?)null, imageUrl = (string?)null, tipo = "Negra", disciplina = "Adicao" },
        },
        "/rulebook/estrelas-alkerianas" => new
        {
            slug = "estrelas-alkerianas", titulo = "As Estrelas Alkerianas", introHtml = (string?)null,
            sections = new[] { new { id = "aeurer", titulo = "I — AEURER", html = "<p><em>Estrela da Curiosidade</em></p><p>Busca o novo.</p>", grupo = (string?)null } },
        },
        "/historicos" => new[] { new { id = "h1", nome = "Ferreiro", descricao = "Cresceu na forja.", periciaMaisSeis = "Atletismo", periciaMaisTres = "Atletismo", isCustomized = false } },
        // Um Efeito de custo fixo, Grau 0, para o formulário "do zero" poder adicionar um.
        "/efeitos" => new[]
        {
            new
            {
                id = "e1", nome = "Luz", grau = 0, descricao = "Ilumina.", tipoDeCusto = "Fixo", custoFixo = 2, custoPorUnidade = (int?)null, unidadeLabel = (string?)null,
                quantidadeDerivadaDeEfeito = (string?)null, maxUnidades = (int?)null, maxEscalaPorGrau = false, maxContandoAPartirDoGrau = (int?)null,
                custoAlternativo = (int?)null, custoAlternativoAPartirDoGrau = (int?)null, preRequisitos = Array.Empty<string[]>(),
            },
        },
        "/campaigns" or "/campaigns/mine" => new[] { new { id = "c1", nome = "Crônicas de Alker", descricao = "A campanha.", imageUrl = (string?)null, bonusDeCarga = 0m } },
        "/images/mine" or "/campaigns/c1/available-images" => new[]
        {
            new { id = "img1", url = "/img/um.png", createdAt = "2026-10-01T12:00:00Z" },
            new { id = "img2", url = "/img/dois.png", createdAt = "2026-10-02T12:00:00Z" },
        },
        _ => Array.Empty<object>(),
    };

    // Personagem sempre tem campanha; NPC e Criatura do GM não, os do jogador são concedidos (dono e campanha).
    private object Ficha() => _tipo switch
    {
        "Personagem" => new
        {
            id = SheetId, campaignId = "c1", ownerId = "u2", imageUrl = "/img/um.png", imageId = "img1",
            nome = "Aria", linhagem = "Humano", variante = "Sinir", vocacao = "Feiticeiro", subVocacao = "Piromante", afinidade = "Fogo", propriedade = "Uma casa",
            nivel = 3, circulo = 1, grau = 2, possuiCoracaoDeMana = true, experienciaAtual = 120, eapAtual = 7,
            nucleosRankF = 1, nucleosRankE = 2, nucleosRankD = 0, nucleosRankC = 0, nucleosRankB = 0, nucleosRankA = 0, nucleosRankS = 0,
            pontosDeIgnicaoAtual = 12, pontosDeIgnicaoTotal = 40, vitalidadeAtual = 20, focoAtual = 10, adrenalinaAtual = 3, estresseAtual = 1,
            cobertura = "Nenhuma", ciclos = 4, graduacao = 1, graduacaoLabel = "Círculo",
            vitalidadeMaximo = 25, focoMaximo = 12, adrenalinaMaximo = 5, estresseMaximo = 10,
            xpParaProximoNivel = 30, pontosDeIgnicaoBonusManual = 2, pontosDePericiaBonusCritico = 1,
            arcaRolada = (int?)null, estrela = (string?)null, sinaAtual = 1, historicoId = (string?)null, equipmentKitId = (string?)null, historia = "Nasceu no porto.",
            afinidadeAdicional = 0, vocacaoArcanaGasta = 2, vocacaoArcanaMaxima = 6,
        },
        "NPC" => new
        {
            id = SheetId, ownerId = EhJogador ? "u2" : null, campaignId = EhJogador ? "c1" : null, campaignName = EhJogador ? "Crônicas de Alker" : null, imageUrl = "/img/um.png", imageId = "img1",
            nome = "Borin", linhagem = "Nephrytes", variante = "Koroanos", vocacao = "Campeao", subVocacao = "Duelista", afinidade = "Terra", propriedade = "Uma forja",
            nivel = 4, circulo = 0, grau = 2, possuiCoracaoDeMana = false, experienciaAtual = 200, eapAtual = 8,
            nucleosRankF = 0, nucleosRankE = 1, nucleosRankD = 1, nucleosRankC = 0, nucleosRankB = 0, nucleosRankA = 0, nucleosRankS = 0,
            pontosDeIgnicaoAtual = 9, pontosDeIgnicaoTotal = 30, vitalidadeAtual = 30, focoAtual = 6, adrenalinaAtual = 4, estresseAtual = 2,
            cobertura = "Nenhuma", ciclos = 2, graduacao = 2, graduacaoLabel = "Grau",
            vitalidadeMaximo = 32, focoMaximo = 8, adrenalinaMaximo = 6, estresseMaximo = 10,
            arcaRolada = (int?)null, estrela = (string?)null, sinaAtual = 2, historicoId = (string?)null, equipmentKitId = (string?)null, historia = "Ferreiro do vale.",
            afinidadeAdicional = 0, vocacaoArcanaGasta = 0, vocacaoArcanaMaxima = 0,
            // O servidor só manda a Experiência dada ao GM (Ficha de NPCs: o jogador que recebeu o NPC não a vê).
            abate = EhJogador ? (int?)null : 50, assistencia = EhJogador ? (int?)null : 25,
        },
        _ => new
        {
            id = SheetId, ownerId = EhJogador ? "u2" : null, campaignId = EhJogador ? "c1" : null, campaignName = EhJogador ? "Crônicas de Alker" : null, imageUrl = "/img/um.png", imageId = "img1",
            nome = "Lobo Cinzento", raca = "Lobo", arquetipo = "Fisico", subArquetipo = "Caçador", afinidade = "Ar", rank = "D",
            nivel = 2, experienciaAtual = 60, abate = 40, assistencia = 20, pontosDeIgnicao = 5,
            vitalidadeAtual = 18, focoAtual = 4, adrenalinaAtual = 2, cobertura = "Nenhuma",
            vitalidadeMaximo = 18, focoMaximo = 4, adrenalinaMaximo = 3,
        },
    };
}
