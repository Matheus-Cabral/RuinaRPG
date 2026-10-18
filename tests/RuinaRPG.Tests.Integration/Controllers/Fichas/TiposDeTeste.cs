using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// Uma ficha criada para um teste: o Mestre dono, quem edita a ficha (o Jogador dono no Personagem e numa
/// ficha concedida; o próprio Mestre no NPC/Criatura) e o id.
/// </summary>
public sealed record FichaDeTeste(string GmToken, string GmEmail, string Token, string SheetId);

/// <summary>
/// O tipo de ficha em que um teste genérico roda. Tudo o que os testes por tipo afirmavam com valores
/// diferentes (status de acesso negado, contagens, ordem dos Atributos) é uma propriedade daqui, nunca um
/// <c>if</c> no corpo do teste.
/// </summary>
public interface ITipoDeTeste
{
    /// <summary>Segmento da rota: <c>/api/{Rota}/{id}/...</c>.</summary>
    string Rota { get; }

    /// <summary>Status de quem não tem acesso à ficha: 403 no Personagem, 404 no NPC/Criatura.</summary>
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
        return new FichaDeTeste(gm, email, playerToken, (await sheet.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id);
    }

    public async Task<string> CriarIntrusoAsync(TestesDeFicha t, FichaDeTeste ficha) => (await t.JogadorAsync(ficha.GmToken)).Token;

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
        return new FichaDeTeste(gm, email, playerToken, (await grant.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId);
    }
}

/// <summary>Base dos testes genéricos: cada subclasse selada roda os casos num tipo de ficha.</summary>
public abstract class TestesDeFicha<TTipo>(ApiFixture fixture) : TestesDeFicha(fixture) where TTipo : ITipoDeTeste, new()
{
    protected static readonly TTipo Tipo = new();

    protected Task<FichaDeTeste> NovaFichaAsync() => Tipo.CriarFichaAsync(this);

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
