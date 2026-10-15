using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SecretNoteUnreadTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SecretNoteUnreadTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAsync()
    {
        var nick = Unique("Gm");
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nick, $"{nick}@t.com", "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string Id, string Token)> RegisterJogadorAsync(string gmToken)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var nick = Unique("Jg");
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nick, $"{nick}@t.com", "Senha!123", "Senha!123", code));
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignWithMembersAsync(string gmToken, string nome, params string[] memberIds)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        var campaignId = (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        foreach (var memberId in memberIds)
            (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(memberId))))
                .EnsureSuccessStatusCode();
        return campaignId;
    }

    private async Task CreateNoteAsync(string gmToken, string campaignId, params string[] recipientIds) =>
        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/secret-notes", gmToken,
            new CreateSecretNoteRequest("Pista.", recipientIds.ToList(), [])))).EnsureSuccessStatusCode();

    private async Task<List<UnreadSecretNotesResponse>> UnreadAsync(string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/secret-notes/unread", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<UnreadSecretNotesResponse>>())!;
    }

    [Fact]
    public async Task Unread_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/secret-notes/unread");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unread_counts_notes_per_campaign_for_the_recipient_only()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var (otherId, otherToken) = await RegisterJogadorAsync(gmToken);
        var campanhaA = await CreateCampaignWithMembersAsync(gmToken, "Campanha A", recipientId, otherId);
        var campanhaB = await CreateCampaignWithMembersAsync(gmToken, "Campanha B", recipientId);
        await CreateNoteAsync(gmToken, campanhaA, recipientId);
        await CreateNoteAsync(gmToken, campanhaA, recipientId);
        await CreateNoteAsync(gmToken, campanhaB, recipientId);

        (await UnreadAsync(recipientToken)).Should().BeEquivalentTo(new[]
        {
            new UnreadSecretNotesResponse(campanhaA, "Campanha A", 2),
            new UnreadSecretNotesResponse(campanhaB, "Campanha B", 1),
        });
        (await UnreadAsync(otherToken)).Should().BeEmpty();
        (await UnreadAsync(gmToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task MarkRead_clears_only_that_campaign_for_the_caller_and_is_idempotent()
    {
        var gmToken = await RegisterGmAsync();
        var (aId, aToken) = await RegisterJogadorAsync(gmToken);
        var (bId, bToken) = await RegisterJogadorAsync(gmToken);
        var campanhaA = await CreateCampaignWithMembersAsync(gmToken, "Campanha A", aId, bId);
        var campanhaB = await CreateCampaignWithMembersAsync(gmToken, "Campanha B", aId);
        await CreateNoteAsync(gmToken, campanhaA, aId, bId);
        await CreateNoteAsync(gmToken, campanhaB, aId);

        var first = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanhaA}/secret-notes/mark-read", aToken));
        var second = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanhaA}/secret-notes/mark-read", aToken));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UnreadAsync(aToken)).Should().BeEquivalentTo(new[] { new UnreadSecretNotesResponse(campanhaB, "Campanha B", 1) });
        (await UnreadAsync(bToken)).Should().BeEquivalentTo(new[] { new UnreadSecretNotesResponse(campanhaA, "Campanha A", 1) });
    }

    [Fact]
    public async Task A_note_created_after_MarkRead_counts_as_unread_again()
    {
        var gmToken = await RegisterGmAsync();
        var (playerId, playerToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Campanha", playerId);
        await CreateNoteAsync(gmToken, campanha, playerId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/secret-notes/mark-read", playerToken));

        await CreateNoteAsync(gmToken, campanha, playerId);

        (await UnreadAsync(playerToken)).Should().BeEquivalentTo(new[] { new UnreadSecretNotesResponse(campanha, "Campanha", 1) });
    }

    [Fact]
    public async Task MarkRead_by_a_non_member_returns_403_and_an_unknown_campaign_404()
    {
        var gmToken = await RegisterGmAsync();
        var (_, outsiderToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Campanha");

        var forbidden = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/secret-notes/mark-read", outsiderToken));
        var notFound = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{Guid.NewGuid()}/secret-notes/mark-read", outsiderToken));

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
