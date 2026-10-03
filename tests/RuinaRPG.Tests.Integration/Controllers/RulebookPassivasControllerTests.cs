using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>Requisitos - Livro de Regras R0010: a aba Habilidades Passivas, por papel e por campanha.</summary>
public class RulebookPassivasControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RulebookPassivasControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAsync(string tag)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"LivroGm{tag}", $"livrogm{tag}@teste.com", "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string Id, string Token)> RegisterJogadorAsync(string gmToken, string tag)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"LivroPl{tag}", $"livropl{tag}@teste.com", "Senha!123", "Senha!123", code));
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome, string? memberId = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        var id = (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        if (memberId is not null)
            await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{id}/members", gmToken, new AddCampaignMemberRequest(memberId)));
        return id;
    }

    private async Task<string> CreateEntryAsync(string gmToken, string nome, string tipo, string? categoria, RequisitosDePassivaDto? requisitos = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, tipo, tipo == "Passiva" ? 0 : 1, "Descrição de " + nome, [], false, categoria, requisitos)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private async Task AttachAsync(string gmToken, string campaignId, string entryId, bool publica)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, entryId, null)));
        var attachmentId = (await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        if (publica)
            await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<List<PassivaDoLivroResponse>> GetAsync(string token, string? campaignId = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook/passivas" + (campaignId is null ? "" : $"?campaignId={campaignId}"), token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<PassivaDoLivroResponse>>())!;
    }

    [Fact]
    public async Task Anonymous_is_unauthorized_and_the_public_rulebook_still_answers()
    {
        (await _client.GetAsync("/api/rulebook/passivas")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/rulebook/graus-e-circulos")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Gm_sees_every_passiva_of_their_own_bank_sorted_by_name_and_nothing_else()
    {
        var gm = await RegisterGmAsync("A");
        var outroGm = await RegisterGmAsync("A2");
        await CreateEntryAsync(gm, "Zelo", "Passiva", "DeClasse");
        await CreateEntryAsync(gm, "Ardor", "Passiva", "Livre", new RequisitosDePassivaDto(Nivel: 10, CoracaoDeMana: true));
        await CreateEntryAsync(gm, "Bola de Fogo", "Magia", null);
        await CreateEntryAsync(outroGm, "Do Outro", "Passiva", "Livre");

        var passivas = await GetAsync(gm);

        passivas.Select(p => p.Nome).Should().Equal("Ardor", "Zelo");
        passivas[0].Should().Match<PassivaDoLivroResponse>(p => p.Categoria == "Livre" && p.Descricao == "Descrição de Ardor");
        passivas[0].Requisitos.Should().Equal("Nível 10", "Coração de Mana");
        passivas[1].Should().Match<PassivaDoLivroResponse>(p => p.Categoria == "DeClasse" && p.Requisitos.Count == 0);
    }

    [Fact]
    public async Task Jogador_sees_only_the_passivas_public_in_the_chosen_campaign()
    {
        var gm = await RegisterGmAsync("B");
        var (playerId, player) = await RegisterJogadorAsync(gm, "B");
        var campanha = await CreateCampaignAsync(gm, "Campanha B", playerId);
        var outraCampanha = await CreateCampaignAsync(gm, "Campanha B2", playerId);
        var publica = await CreateEntryAsync(gm, "Pública", "Passiva", "Vocacional");
        var privada = await CreateEntryAsync(gm, "Privada", "Passiva", "Livre");
        var naoAnexada = await CreateEntryAsync(gm, "Não Anexada", "Passiva", "Livre");
        var magia = await CreateEntryAsync(gm, "Magia Pública", "Magia", null);
        var daOutra = await CreateEntryAsync(gm, "Da Outra", "Passiva", "Livre");
        await AttachAsync(gm, campanha, publica, publica: true);
        await AttachAsync(gm, campanha, privada, publica: false);
        await AttachAsync(gm, campanha, magia, publica: true);
        await AttachAsync(gm, outraCampanha, daOutra, publica: true);

        (await GetAsync(player, campanha)).Select(p => p.Nome).Should().Equal("Pública");
        (await GetAsync(player, outraCampanha)).Select(p => p.Nome).Should().Equal("Da Outra");
        naoAnexada.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Jogador_without_campaign_id_gets_400_unknown_campaign_404_and_non_member_403()
    {
        var gm = await RegisterGmAsync("C");
        var (_, player) = await RegisterJogadorAsync(gm, "C");
        var outroGm = await RegisterGmAsync("C2");
        var alheia = await CreateCampaignAsync(outroGm, "Campanha Alheia");
        await AttachAsync(outroGm, alheia, await CreateEntryAsync(outroGm, "Segredo", "Passiva", "Livre"), publica: true);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook/passivas", player))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/rulebook/passivas?campaignId={Guid.NewGuid()}", player))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/rulebook/passivas?campaignId={alheia}", player))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
