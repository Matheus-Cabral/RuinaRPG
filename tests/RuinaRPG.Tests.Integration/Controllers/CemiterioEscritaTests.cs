using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Tests.Integration.Controllers.Fichas;

namespace RuinaRPG.Tests.Integration.Controllers;

// Cemitério de fichas, "Leitura × escrita": toda escrita numa Ficha de Personagem no cemitério é 409; ler,
// as exceções ([PermitidoNoCemiterio]), quem não tem acesso e NPC/Criatura não mudam.
public class CemiterioEscritaTests(ApiFixture fixture) : TestesDeFicha(fixture)
{
    private const string Recusa = "Esta ficha está no cemitério e não pode ser editada.";

    /// <summary>
    /// Toda ação não-GET da Ficha de Personagem (template <c>api/character-sheets/{id|sheetId}…</c>) sem
    /// <see cref="PermitidoNoCemiterioAttribute"/>: verbo, template e o tipo de cada parâmetro de rota
    /// (<c>nome=Tipo</c>, separados por <c>;</c>). Lida das ações registradas de um host que nunca abre o banco.
    /// </summary>
    public static TheoryData<string, string, string> EscritasDaFicha()
    {
        using var host = new ApiFactory("Host=nenhum;Database=nenhum");
        var dados = new TheoryData<string, string, string>();
        foreach (var (verbo, template, parametros) in Escritas(host.Services))
            dados.Add(verbo, template, parametros);
        return dados;
    }

    private static IEnumerable<(string Verbo, string Template, string Parametros)> Escritas(IServiceProvider services) =>
        services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => !a.MethodInfo.IsDefined(typeof(PermitidoNoCemiterioAttribute), true))
            .SelectMany(a => (a.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods) ?? [])
                .Where(v => v != "GET")
                .Select(v => (Verbo: v, Template: (a.AttributeRouteInfo?.Template ?? "").TrimStart('/'), Acao: a)))
            .Where(e => DaFichaDePersonagem(e.Template))
            .Select(e => (e.Verbo, e.Template, string.Join(";", e.Acao.Parameters
                .Where(p => p.BindingInfo?.BindingSource?.Id == "Path")
                .Select(p => $"{p.Name}={(p.ParameterType.IsEnum ? Enum.GetNames(p.ParameterType)[0] : p.ParameterType.Name)}"))))
            .OrderBy(e => e.Template, StringComparer.Ordinal).ThenBy(e => e.Verbo, StringComparer.Ordinal);

    private static bool DaFichaDePersonagem(string template)
    {
        var segmentos = template.Split('/');
        return segmentos.Length >= 3 && segmentos[0] == "api" && segmentos[1] == "character-sheets"
            && segmentos[2].StartsWith('{') && segmentos[2].Trim('{', '}').Split(':')[0] is "id" or "sheetId";
    }

    [Theory]
    [MemberData(nameof(EscritasDaFicha), DisableDiscoveryEnumeration = true)]
    public async Task Toda_escrita_da_ficha_de_personagem_e_recusada_no_cemiterio(string verbo, string template, string parametros)
    {
        var c = await FichaMortaAsync();
        var url = await UrlAsync(template, parametros, c.SheetId);

        var resposta = await Client.SendAsync(AuthedRequest(new HttpMethod(verbo), url, c.Token, new { }));

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, $"{verbo} {url}");
        (await resposta.Content.ReadAsStringAsync()).Should().Be(Recusa);
    }

    [Fact]
    public void O_inventario_de_escritas_cobre_a_ficha_inteira()
    {
        var escritas = EscritasDaFicha();
        escritas.Count().Should().BeGreaterThanOrEqualTo(40);
        Escritas(Factory.Services).Select(e => $"{e.Verbo} {e.Template}")
            .Should().Equal(escritas.Select(d => $"{d[0]} {d[1]}"), "o host de verdade tem as mesmas ações");
    }

    [Fact]
    public async Task As_excecoes_continuam_funcionando()
    {
        var c = await FichaMortaAsync();

        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{c.SheetId}/dismiss-level-up-notice", c.Token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/morte", c.GmToken, new DefinirMorteRequest(true))))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{c.SheetId}", c.GmToken)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Estranho_recebe_o_mesmo_erro_de_antes()
    {
        var viva = await new TipoDeTestePersonagem().CriarFichaAsync(this);
        var morta = await FichaMortaAsync();
        var estranho = (await JogadorAsync(morta.GmToken)).Token;
        var corpoDaFicha = new TipoDeTestePersonagem().AtualizacaoComPi(0);
        var corpoDoInventario = new { ItemId = Guid.NewGuid().ToString(), Qtd = 1 };

        foreach (var (metodo, sufixo, corpo) in new (HttpMethod, string, object)[] { (HttpMethod.Put, "", corpoDaFicha), (HttpMethod.Post, "/inventory", corpoDoInventario) })
        {
            var naViva = await Client.SendAsync(AuthedRequest(metodo, $"/api/character-sheets/{viva.SheetId}{sufixo}", estranho, corpo));
            var naMorta = await Client.SendAsync(AuthedRequest(metodo, $"/api/character-sheets/{morta.SheetId}{sufixo}", estranho, corpo));
            naMorta.StatusCode.Should().Be(naViva.StatusCode, $"{metodo} {sufixo}");
            naMorta.StatusCode.Should().NotBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task Ficha_viva_continua_editavel()
    {
        var c = await new TipoDeTestePersonagem().CriarFichaAsync(this);
        var resposta = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}", c.Token, new TipoDeTestePersonagem().AtualizacaoComPi(0)));
        resposta.IsSuccessStatusCode.Should().BeTrue(await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NPC_e_Criatura_nao_sao_afetados()
    {
        var c = await FichaConcedidaAsync("Npc");
        var resposta = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{c.SheetId}", c.Token, new TipoDeTesteNpc().AtualizacaoComPi(0)));
        resposta.IsSuccessStatusCode.Should().BeTrue(await resposta.Content.ReadAsStringAsync());
    }

    /// <summary>Ficha de Personagem de um Jogador, marcada morta pelo GM.</summary>
    private async Task<FichaDeTeste> FichaMortaAsync()
    {
        var c = await new TipoDeTestePersonagem().CriarFichaAsync(this);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/morte", c.GmToken, new DefinirMorteRequest(true))))
            .EnsureSuccessStatusCode();
        return c;
    }

    /// <summary>A URL da ação na ficha dada: Guids novos nos outros ids, o primeiro valor do enum, uma Perícia existente.</summary>
    private async Task<string> UrlAsync(string template, string parametros, string sheetId)
    {
        var url = "/" + template;
        foreach (var parametro in parametros.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var (nome, tipo) = (parametro.Split('=')[0], parametro.Split('=')[1]);
            var valor = nome is "id" or "sheetId" && Segmento(template, 2) == nome ? sheetId
                : tipo == nameof(Guid) ? Guid.NewGuid().ToString()
                : tipo == nameof(String) && nome == "pericia" ? await PericiaExistenteAsync()
                : tipo;
            url = System.Text.RegularExpressions.Regex.Replace(url, $@"\{{{nome}(:[^}}]*)?\}}", valor);
        }
        return url;
    }

    private static string Segmento(string template, int indice) => template.Split('/')[indice].Trim('{', '}').Split(':')[0];

    private async Task<string> PericiaExistenteAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return await db.Pericias.Where(p => !p.IsDeleted).OrderBy(p => p.Id).Select(p => p.Chave).FirstAsync();
    }
}
