using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Tests.Integration.Hubs;

public class NotificationHubTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NotificationHubTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<List<UnreadSecretNotesResponse>> UnreadAsync(string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/secret-notes/unread", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<UnreadSecretNotesResponse>>())!;
    }

    private async Task<string> CreateNoteAsync(string gmToken, string campaignId, params string[] recipientIds)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/secret-notes", gmToken,
            new CreateSecretNoteRequest("Pista.", recipientIds.ToList(), [])));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SecretNoteResponse>())!.Id;
    }

    private async Task<HubConnection> ConnectAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    /// <summary>Records every event a connection receives, so a test can assert both arrival and absence.</summary>
    private sealed class Inbox
    {
        private readonly TaskCompletionSource _any = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<SecretNoteNotification> Received { get; } = new();
        public int Changed { get; private set; }

        public Inbox(HubConnection connection)
        {
            connection.On<SecretNoteNotification>(NotificationEvents.SecretNoteReceived, n => { Received.Add(n); _any.TrySetResult(); });
            connection.On(NotificationEvents.SecretNotesChanged, () => { Changed++; _any.TrySetResult(); });
        }

        public async Task<bool> WaitForAnyAsync(TimeSpan timeout) =>
            await Task.WhenAny(_any.Task, Task.Delay(timeout)) == _any.Task;
    }

    private static readonly TimeSpan Arrives = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaysSilent = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Recipient_receives_SecretNoteReceived_and_a_non_recipient_member_receives_nothing()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var (otherId, otherToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId, otherId);
        await using var recipientConnection = await ConnectAsync(recipientToken);
        await using var otherConnection = await ConnectAsync(otherToken);
        var recipientInbox = new Inbox(recipientConnection);
        var otherInbox = new Inbox(otherConnection);

        await CreateNoteAsync(gmToken, campanha, recipientId);

        (await recipientInbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        recipientInbox.Received.Should().ContainSingle().Which.Should().Be(new SecretNoteNotification(campanha, "Ruína"));
        (await otherInbox.WaitForAnyAsync(StaysSilent)).Should().BeFalse();
    }

    [Fact]
    public async Task A_recipient_listed_twice_is_notified_once()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        await using var connection = await ConnectAsync(recipientToken);
        var inbox = new Inbox(connection);

        await CreateNoteAsync(gmToken, campanha, recipientId, recipientId);

        (await inbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        await Task.Delay(StaysSilent);
        inbox.Received.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_notifies_only_the_added_recipient_and_keeps_the_read_state_of_the_existing_one()
    {
        var gmToken = await RegisterGmAsync();
        var (existingId, existingToken) = await RegisterJogadorAsync(gmToken);
        var (addedId, addedToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", existingId, addedId);
        var noteId = await CreateNoteAsync(gmToken, campanha, existingId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/secret-notes/mark-read", existingToken));
        await using var existingConnection = await ConnectAsync(existingToken);
        await using var addedConnection = await ConnectAsync(addedToken);
        var existingInbox = new Inbox(existingConnection);
        var addedInbox = new Inbox(addedConnection);

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campanha}/secret-notes/{noteId}", gmToken,
            new UpdateSecretNoteRequest("Pista revista.", [existingId, addedId], [])));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await addedInbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        addedInbox.Received.Should().ContainSingle().Which.Should().Be(new SecretNoteNotification(campanha, "Ruína"));
        (await existingInbox.WaitForAnyAsync(StaysSilent)).Should().BeFalse();
        (await UnreadAsync(addedToken)).Should().ContainSingle().Which.Count.Should().Be(1);
        (await UnreadAsync(existingToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Update_that_only_changes_the_text_notifies_nobody()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        var noteId = await CreateNoteAsync(gmToken, campanha, recipientId);
        await using var connection = await ConnectAsync(recipientToken);
        var inbox = new Inbox(connection);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campanha}/secret-notes/{noteId}", gmToken,
            new UpdateSecretNoteRequest("Só o texto mudou.", [recipientId], [])));

        (await inbox.WaitForAnyAsync(StaysSilent)).Should().BeFalse();
    }

    [Fact]
    public async Task Update_that_removes_a_recipient_sends_them_SecretNotesChanged()
    {
        var gmToken = await RegisterGmAsync();
        var (keptId, _) = await RegisterJogadorAsync(gmToken);
        var (removedId, removedToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", keptId, removedId);
        var noteId = await CreateNoteAsync(gmToken, campanha, keptId, removedId);
        await using var connection = await ConnectAsync(removedToken);
        var inbox = new Inbox(connection);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campanha}/secret-notes/{noteId}", gmToken,
            new UpdateSecretNoteRequest("Pista.", [keptId], [])));

        (await inbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        inbox.Changed.Should().Be(1);
        inbox.Received.Should().BeEmpty();
        (await UnreadAsync(removedToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_a_note_sends_SecretNotesChanged_and_drops_the_unread_count()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        var noteId = await CreateNoteAsync(gmToken, campanha, recipientId);
        await using var connection = await ConnectAsync(recipientToken);
        var inbox = new Inbox(connection);

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campanha}/secret-notes/{noteId}", gmToken));

        (await inbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        inbox.Changed.Should().Be(1);
        (await UnreadAsync(recipientToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_the_campaign_sends_SecretNotesChanged_to_its_note_recipients()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        await CreateNoteAsync(gmToken, campanha, recipientId);
        await using var connection = await ConnectAsync(recipientToken);
        var inbox = new Inbox(connection);

        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campanha}", gmToken, new DeleteCampaignRequest("Senha!123")));

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await inbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        inbox.Changed.Should().Be(1);
        (await UnreadAsync(recipientToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Note_is_still_created_when_the_hub_throws()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        await using var brokenFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IHubContext<NotificationHub>>(new ThrowingHubContext())));
        using var brokenClient = brokenFactory.CreateClient();

        var response = await brokenClient.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/secret-notes", gmToken,
            new CreateSecretNoteRequest("Pista.", [recipientId], [])));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await UnreadAsync(recipientToken)).Should().ContainSingle().Which.Count.Should().Be(1);
    }

    private sealed class ThrowingHubContext : IHubContext<NotificationHub>
    {
        public IHubClients Clients => throw new InvalidOperationException("hub down");
        public IGroupManager Groups => throw new InvalidOperationException("hub down");
    }

    [Fact]
    public async Task Anonymous_connection_is_refused()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/hubs/notifications"), options =>
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        await FluentActions.Awaiting(() => connection.StartAsync()).Should().ThrowAsync<HttpRequestException>();
    }
}
