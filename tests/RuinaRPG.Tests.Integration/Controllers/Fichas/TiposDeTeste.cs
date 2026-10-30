using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// Uma ficha criada para um teste: o Mestre dono, quem edita a ficha (o Jogador dono no Personagem e numa
/// ficha concedida; o próprio Mestre no NPC/Criatura), o id e a campanha (se a ficha está numa).
/// </summary>
public sealed record FichaDeTeste(string GmToken, string GmEmail, string Token, string SheetId, string? CampaignId = null);

/// <summary>
/// O tipo de ficha em que um teste genérico roda. Tudo o que os testes por tipo afirmavam com valores
/// diferentes (status de acesso negado, contagens, ordem dos Atributos) é uma propriedade daqui, nunca um
/// <c>if</c> no corpo do teste.
/// </summary>
public interface ITipoDeTeste
{
    /// <summary>Segmento da rota: <c>/api/{Rota}/{id}/...</c>.</summary>
    string Rota { get; }

    /// <summary>Status de quem não tem acesso à ficha numa escrita: 403 no Personagem, 404 no NPC/Criatura (leitura: 404 nos três).</summary>
    HttpStatusCode StatusNegado { get; }

    int QuantidadeDePericias { get; }

    string[] AtributosEmOrdem { get; }

    /// <summary>Ficha nova; <see cref="FichaDeTeste.Token"/> é quem a edita.</summary>
    Task<FichaDeTeste> CriarFichaAsync(TestesDeFicha t);

    /// <summary>Ficha editada por um Jogador: a dele no Personagem, uma concedida a ele no NPC/Criatura.</summary>
    Task<FichaDeTeste> CriarFichaDeJogadorAsync(TestesDeFicha t);

    /// <summary>
    /// Token de quem não tem acesso à ficha, no cenário que os testes por tipo usavam: um Jogador do mesmo
    /// Mestre que não é dono (Personagem) ou outro Mestre (NPC/Criatura).
    /// </summary>
    Task<string> CriarIntrusoAsync(TestesDeFicha t, FichaDeTeste ficha);

    /// <summary>Pontos de Ignição atuais da ficha, lidos direto do banco.</summary>
    Task<int> PiAsync(RuinaRpgDbContext db, Guid id);

    Task DefinirPiAsync(RuinaRpgDbContext db, Guid id, int pi);

    /// <summary>Corpo do PUT da ficha com o campo de PI no valor dado (um formulário aberto antes de um débito).</summary>
    object AtualizacaoComPi(int pi);

    /// <summary>DeCriatura das entradas que a ficha manda ao Banco de Magias.</summary>
    bool MagiasDeCriatura { get; }
}

/// <summary>Personagem e NPC: os dois têm Histórico.</summary>
public interface ITipoDeTestePersonagemOuNpc : ITipoDeTeste
{
    /// <summary>Corpo do PUT da ficha que só define o Histórico.</summary>
    object AtualizacaoComHistorico(string? historicoId);
}

public sealed class TipoDeTestePersonagem : ITipoDeTestePersonagemOuNpc
{
    public string Rota => "character-sheets";
    public HttpStatusCode StatusNegado => HttpStatusCode.Forbidden;
    public int QuantidadeDePericias => 39;
    public string[] AtributosEmOrdem => ["Forca", "Vigor", "Agilidade", "Destreza", "Astucia", "Instinto", "Influencia", "Vontade"];

    public Task<FichaDeTeste> CriarFichaAsync(TestesDeFicha t) => CriarFichaDeJogadorAsync(t);

    public async Task<FichaDeTeste> CriarFichaDeJogadorAsync(TestesDeFicha t)
    {
        var (gm, email) = await t.MestreAsync();
        var (playerId, playerToken) = await t.JogadorAsync(gm);
        var campaignId = await t.CampanhaComMembroAsync(gm, playerId);
        var sheet = await t.SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gm, new CreateCharacterSheetRequest(playerId));
        return new FichaDeTeste(gm, email, playerToken, (await sheet.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id, campaignId);
    }

    public async Task<string> CriarIntrusoAsync(TestesDeFicha t, FichaDeTeste ficha) => (await t.JogadorAsync(ficha.GmToken)).Token;

    public async Task<int> PiAsync(RuinaRpgDbContext db, Guid id) => (await db.CharacterSheets.AsNoTracking().SingleAsync(s => s.Id == id)).PontosDeIgnicaoAtual;

    public async Task DefinirPiAsync(RuinaRpgDbContext db, Guid id, int pi)
    {
        (await db.CharacterSheets.FindAsync(id))!.PontosDeIgnicaoAtual = pi;
        await db.SaveChangesAsync();
    }

    public object AtualizacaoComPi(int pi) => new UpdateCharacterSheetRequest(null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", null, null,
        true, 0, 0, 0, 0, 0, 0, 0, 0, 0, pi, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, null, null, 0);

    public bool MagiasDeCriatura => false;

    public object AtualizacaoComHistorico(string? historicoId) => new UpdateCharacterSheetRequest(
        null, "Teste", null, null, null, null, null, null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, historicoId, null, 0);
}

public sealed class TipoDeTesteNpc : ITipoDeTestePersonagemOuNpc
{
    public string Rota => "npc-sheets";
    public HttpStatusCode StatusNegado => HttpStatusCode.NotFound;
    public int QuantidadeDePericias => 39;
    public string[] AtributosEmOrdem => ["Forca", "Vigor", "Agilidade", "Destreza", "Astucia", "Instinto", "Influencia", "Vontade"];

    public async Task<FichaDeTeste> CriarFichaAsync(TestesDeFicha t)
    {
        var (gm, email) = await t.MestreAsync();
        var response = await t.SendOkAsync(HttpMethod.Post, "/api/npc-sheets", gm);
        return new FichaDeTeste(gm, email, gm, (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id);
    }

    public Task<FichaDeTeste> CriarFichaDeJogadorAsync(TestesDeFicha t) => t.FichaConcedidaAsync("Npc");

    public Task<string> CriarIntrusoAsync(TestesDeFicha t, FichaDeTeste ficha) => t.OutroMestreAsync();

    public async Task<int> PiAsync(RuinaRpgDbContext db, Guid id) => (await db.NpcSheets.AsNoTracking().SingleAsync(s => s.Id == id)).PontosDeIgnicaoAtual;

    public async Task DefinirPiAsync(RuinaRpgDbContext db, Guid id, int pi)
    {
        (await db.NpcSheets.FindAsync(id))!.PontosDeIgnicaoAtual = pi;
        await db.SaveChangesAsync();
    }

    public object AtualizacaoComPi(int pi) => new UpdateNpcSheetRequest(null, "Sentinela", "Humano", "Sinir", "Campeao", "Duelista", null, null,
        1, true, 0, 0, 0, 0, 0, 0, 0, 0, 0, pi, 0, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, null, null, 0);

    public bool MagiasDeCriatura => false;

    public object AtualizacaoComHistorico(string? historicoId) => new UpdateNpcSheetRequest(
        null, "Teste", null, null, null, null, null, null,
        1, true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, historicoId, null, 0);
}

public sealed class TipoDeTesteCriatura : ITipoDeTeste
{
    public string Rota => "creature-sheets";
    public HttpStatusCode StatusNegado => HttpStatusCode.NotFound;
    public int QuantidadeDePericias => 20;
    public string[] AtributosEmOrdem => ["Forca", "Vigor", "Agilidade", "Destreza", "Astucia", "Ego"];

    public async Task<FichaDeTeste> CriarFichaAsync(TestesDeFicha t)
    {
        var (gm, email) = await t.MestreAsync();
        var response = await t.SendOkAsync(HttpMethod.Post, "/api/creature-sheets", gm);
        return new FichaDeTeste(gm, email, gm, (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id);
    }

    public Task<FichaDeTeste> CriarFichaDeJogadorAsync(TestesDeFicha t) => t.FichaConcedidaAsync("Creature");

    public Task<string> CriarIntrusoAsync(TestesDeFicha t, FichaDeTeste ficha) => t.OutroMestreAsync();

    public async Task<int> PiAsync(RuinaRpgDbContext db, Guid id) => (await db.CreatureSheets.AsNoTracking().SingleAsync(s => s.Id == id)).PontosDeIgnicao;

    public async Task DefinirPiAsync(RuinaRpgDbContext db, Guid id, int pi)
    {
        (await db.CreatureSheets.FindAsync(id))!.PontosDeIgnicao = pi;
        await db.SaveChangesAsync();
    }

    public object AtualizacaoComPi(int pi) => new UpdateCreatureSheetRequest(null, "Lobo das Ruínas", "Lobo", "Fisico", "Predador", "Terra", "F", 1, 0, pi, 0, 0, 0, "Nenhuma");

    public bool MagiasDeCriatura => true;
}

/// <summary>Os ajudantes de <see cref="ApiTestBase"/> que os tipos de teste usam para montar as fichas.</summary>
public abstract class TestesDeFicha(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static string Apelido(string prefixo) => $"{prefixo}{Guid.NewGuid():N}"[..20];

    internal async Task<(string Token, string Email)> MestreAsync()
    {
        var email = $"{Guid.NewGuid():N}@teste.com";
        return (await RegisterGmAndGetTokenAsync(Apelido("gm"), email), email);
    }

    internal async Task<string> OutroMestreAsync() => (await MestreAsync()).Token;

    internal Task<(string PlayerId, string Token)> JogadorAsync(string gmToken) =>
        RegisterJogadorLinkedToAsync(gmToken, Apelido("jog"), $"{Guid.NewGuid():N}@teste.com");

    /// <summary>Envia e afirma que deu certo: a montagem das fichas usa isto, e os testes de penalidade também.</summary>
    protected internal async Task<HttpResponseMessage> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await Client.SendAsync(AuthedRequest(method, url, token, body));
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} returned {(int)response.StatusCode}");
        return response;
    }

    protected async Task<string> CreateItemGeralAsync(string gmToken, string nome, decimal peso, int preco, string? imageId = null, string descricao = "Um item qualquer")
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("ItemGeral", nome, peso, preco, imageId, "Diversos", descricao, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    protected async Task<string> CreateArtefatoItemAsync(string gmToken, string nome, string tipoDeAlvo, string alvo, int valor, string? imageId = null, string? descricao = null)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", nome, 0.1m, 500, imageId, null, descricao, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    protected async Task<string> UploadImageAsync(string token)
    {
        var content = new MultipartFormDataContent();
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(message);
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Images.ImageUploadResponse>())!.Id;
    }

    internal async Task<string> CampanhaComMembroAsync(string gmToken, string playerId)
    {
        var campaign = await SendOkAsync(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", ""));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId));
        return campaignId;
    }

    internal async Task<FichaDeTeste> FichaConcedidaAsync(string tipo)
    {
        var (gm, email) = await MestreAsync();
        var (playerId, playerToken) = await JogadorAsync(gm);
        var campaignId = await CampanhaComMembroAsync(gm, playerId);
        var grant = await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gm, new GrantSheetRequest(playerId, tipo, null));
        return new FichaDeTeste(gm, email, playerToken, (await grant.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId, campaignId);
    }
}

/// <summary>Base dos testes genéricos: cada subclasse selada roda os casos num tipo de ficha.</summary>
public abstract class TestesDeFicha<TTipo>(ApiFixture fixture) : TestesDeFicha(fixture) where TTipo : ITipoDeTeste, new()
{
    protected static readonly TTipo Tipo = new();

    protected Task<FichaDeTeste> NovaFichaAsync() => Tipo.CriarFichaAsync(this);

    protected Task<FichaDeTeste> FichaDeJogadorAsync() => Tipo.CriarFichaDeJogadorAsync(this);

    protected async Task SetPiAsync(FichaDeTeste f, int pi)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await Tipo.DefinirPiAsync(scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>(), Guid.Parse(f.SheetId), pi);
    }

    protected async Task<int> GetPiAsync(FichaDeTeste f)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await Tipo.PiAsync(scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>(), Guid.Parse(f.SheetId));
    }

    /// <summary>
    /// Cria um Histórico (o Mestre da ficha vira Auditor de Regras para isso) com as duas Perícias e o põe
    /// na ficha. Só Personagem e NPC têm Histórico.
    /// </summary>
    protected async Task DefinirHistoricoAsync(FichaDeTeste ficha, ITipoDeTestePersonagemOuNpc tipo, string nome, string pericia1, string pericia2)
    {
        await GrantRulesAuditorAsync(ficha.GmEmail);
        var historicoResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", ficha.GmToken,
            new CreateHistoricoRequest(nome, "Descrição de teste.", pericia1, pericia2)));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{tipo.Rota}/{ficha.SheetId}", ficha.Token, tipo.AtualizacaoComHistorico(historico!.Id)));
    }

    /// <summary><c>/api/{rota}/{id}/{sufixo}</c> da ficha.</summary>
    protected static string Url(FichaDeTeste ficha, string sufixo) => $"/api/{Tipo.Rota}/{ficha.SheetId}/{sufixo}";
}

// Leitores das respostas. As respostas dos três tipos têm o mesmo JSON nos campos lidos aqui, exceto
// DurabilidadeMaxima de arma e armadura, que na Criatura se chama DurabilidadeMaximo: só os testes de
// Personagem/NPC a leem; os da Criatura leem CreatureWeaponResponse/CreatureArmorSlotResponse.
public sealed record PericiaLida(string Pericia, int Gasto, int Modificador, string? AtributoEscolhido, int? Total, bool ContaHistorico);
public sealed record AtributoLido(string Atributo, int Gasto, int Bonus, bool TemMaestria, int Total);
public sealed record MaestriaLida(string Id, string Nome, string Pericia, string Atributo, int GastoMaestria, int Total);
public sealed record ArmaLida(string Id, string? ItemId, string Nome, int? Dano, bool IsEquipped, int? DurabilidadeAtual, int? DurabilidadeMaxima, string? ImageUrl, string? Descricao,
    List<string>? Requisitos, List<string>? RequisitosPendentes, List<string>? Penalidade, string? OutrasPenalidades);
public sealed record SlotLido(string Slot, string? ItemId, string? Nome, int? DurabilidadeAtual, int? DurabilidadeMaxima, string? ImageUrl, string? Descricao);
public sealed record EscudoLido(string Id, string ItemId, string Nome, int? BonusDefesa, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima,
    string? ImageUrl, string? Descricao, List<string>? RequisitosPendentes);
public sealed record ArtefatoLido(string Id, string Nome, List<string>? Penalidade);
public sealed record FichaLida(string Id, string? EquipmentKitId, int Ciclos, string? Nome, int Nivel, int ExperienciaAtual, string? CampaignId, string? OwnerId, int AdrenalinaMaximo,
    string? Afinidade, string? HistoricoId, string? CampaignName = null);
public sealed record ResumoDeFichaLido(string Id, string? OwnerNickname);
