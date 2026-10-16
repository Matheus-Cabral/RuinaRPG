using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Hubs;

public class EncounterHubTests(ApiFixture fixture) : ApiTestBase(fixture)
{

    private async Task<string> CreateEncounterAsync(string gmToken, string campaignId, string nome)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gmToken, new CreateEncounterRequest(nome)));
        return (await response.Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
    }

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private static UpdateNpcSheetRequest ValidNpcUpdate(string nome, int vitalidadeAtual) => new(
        null, nome, "Humano", "Sinir", "Campeao", "Duelista", null, "Descrição de Teste",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, vitalidadeAtual, 15, 8, 3, "Parcial", 100, null, null, 0, null, null, 0);

    private async Task<string> CreateCharacterSheetAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private static UpdateCharacterSheetRequest ValidCharacterUpdate(string nome, int vitalidadeAtual) => new(
        null, nome, "Humano", "Sinir", "Campeao", "Duelista", null, "Descrição de Teste",
        // 749 XP is one below Nível 6's threshold (750) — Nível is derived now, and reaching a
        // threshold exactly already counts as that Nível, so 749 keeps this at Nível 5.
        true, 749, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, vitalidadeAtual, 15, 8, 3, "Parcial", 100, 0, null, null, 0, null, null, 0);

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.CreatureSheets.CreatureSheetResponse>())!.Id;
    }

    private static RuinaRPG.Contracts.CreatureSheets.UpdateCreatureSheetRequest ValidCreatureUpdate(string nome, int vitalidadeAtual) => new(
        null, nome, "Lobo", "Fisico", "Predador", "Terra", "F", 3, 200, 5, vitalidadeAtual, 8, 10, "Parcial");

    private async Task<HubConnection> ConnectAndJoinAsync(string token, string encounterId)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/encounters"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        await connection.StartAsync();
        await connection.InvokeAsync("JoinEncounter", encounterId);
        return connection;
    }

    private static async Task<bool> WaitForParticipantsChangedAsync(HubConnection connection)
    {
        var tcs = new TaskCompletionSource();
        using var registration = connection.On("ParticipantsChanged", () => tcs.TrySetResult());
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        return completed == tcs.Task;
    }

    /// <summary>
    /// Deliberately does NOT set AccessTokenProvider — the .NET SignalR client only sends
    /// AccessTokenProvider's token via the ?access_token= query string when running under a
    /// browser runtime (an internal IsBrowser check); against TestServer it would otherwise send
    /// the token as an Authorization header on every transport, which would never exercise
    /// Program.cs's OnMessageReceived query-string handling. Appending the token to the URL's
    /// query string directly is the only way to prove that handler actually works from this
    /// (non-browser) test host.
    /// </summary>
    private async Task<HubConnection> ConnectWithQueryStringTokenAndJoinAsync(string token, string encounterId)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, $"/hubs/encounters?access_token={token}"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        await connection.StartAsync();
        await connection.InvokeAsync("JoinEncounter", encounterId);
        return connection;
    }

    [Fact]
    public async Task Updating_a_live_sourced_CharacterSheet_participant_broadcasts_ParticipantsChanged_to_its_encounter_group()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HubGm1", "hub1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "HubPlayer1", "hubplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Hub Personagem");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Hub Personagem");
        var sheetId = await CreateCharacterSheetAsync(gmToken, campaignId, playerId);

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(sheetId, null, null, 8)));

        await using var connection = await ConnectAndJoinAsync(gmToken, encounterId);
        var waitTask = WaitForParticipantsChangedAsync(connection);

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidCharacterUpdate("Vann Astrel", 5)));
        updateResponse.EnsureSuccessStatusCode();

        (await waitTask).Should().BeTrue("updating a live-sourced CharacterSheet participant should broadcast ParticipantsChanged to the encounter's group");
    }

    [Fact]
    public async Task Updating_a_granted_live_sourced_NpcSheet_participant_broadcasts_ParticipantsChanged_to_its_encounter_group()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HubGm2", "hub2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "HubPlayer2", "hubplayer2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Hub Npc Concedido");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Hub Npc Concedido");

        // Grant a fresh NPC to the player — this sets NpcSheet.OwnerId, making it live-sourced
        // per EncounterParticipantsController's own logic (mirrors Task 4's pattern).
        var grantResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new GrantSheetRequest(playerId, "Npc", null)));
        grantResponse.EnsureSuccessStatusCode();
        var npcId = (await grantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, npcId, null, 10)));
        addResponse.EnsureSuccessStatusCode();
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();
        added!.IsLiveSourced.Should().BeTrue();

        await using var connection = await ConnectAndJoinAsync(gmToken, encounterId);
        var waitTask = WaitForParticipantsChangedAsync(connection);

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}", gmToken,
            ValidNpcUpdate("Goblin Concedido", 7)));
        updateResponse.EnsureSuccessStatusCode();

        (await waitTask).Should().BeTrue("updating a granted (live-sourced) NpcSheet participant should broadcast ParticipantsChanged to the encounter's group");
    }

    [Fact]
    public async Task Updating_a_granted_live_sourced_CreatureSheet_participant_broadcasts_ParticipantsChanged_to_its_encounter_group()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HubGm5", "hub5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "HubPlayer5", "hubplayer5@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Hub Criatura Concedida");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Hub Criatura Concedida");

        var grantResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new GrantSheetRequest(playerId, "Creature", null)));
        grantResponse.EnsureSuccessStatusCode();
        var creatureId = (await grantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, null, creatureId, 10)));
        addResponse.EnsureSuccessStatusCode();
        var added = await addResponse.Content.ReadFromJsonAsync<EncounterParticipantResponse>();
        added!.IsLiveSourced.Should().BeTrue();

        await using var connection = await ConnectAndJoinAsync(gmToken, encounterId);
        var waitTask = WaitForParticipantsChangedAsync(connection);

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}", gmToken,
            ValidCreatureUpdate("Lobo Concedido", 7)));
        updateResponse.EnsureSuccessStatusCode();

        (await waitTask).Should().BeTrue("updating a granted (live-sourced) CreatureSheet participant should broadcast ParticipantsChanged to the encounter's group");
    }

    [Fact]
    public async Task Connecting_with_the_token_only_as_a_querystring_access_token_still_authenticates_and_receives_broadcasts()
    {
        // Proves Program.cs's OnMessageReceived handler (Correction 1) actually reads
        // ?access_token= for /hubs paths — see ConnectWithQueryStringTokenAndJoinAsync's remarks
        // for why the existing AccessTokenProvider-based tests above don't exercise this path.
        var gmToken = await RegisterGmAndGetTokenAsync("HubGm3", "hub3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "HubPlayer3", "hubplayer3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Hub Querystring");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Hub Querystring");
        var sheetId = await CreateCharacterSheetAsync(gmToken, campaignId, playerId);

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(sheetId, null, null, 8)));

        await using var connection = await ConnectWithQueryStringTokenAndJoinAsync(gmToken, encounterId);
        var waitTask = WaitForParticipantsChangedAsync(connection);

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidCharacterUpdate("Vann Querystring", 3)));
        updateResponse.EnsureSuccessStatusCode();

        (await waitTask).Should().BeTrue("a connection authenticated purely via ?access_token= query string should still join the group and receive broadcasts");
    }

    [Fact]
    public async Task Connecting_without_any_token_is_rejected()
    {
        // EncounterHub is [Authorize] — an unauthenticated connection attempt must be rejected,
        // not silently allowed through. No AccessTokenProvider and no ?access_token= query
        // string, so negotiate hits the hub with no credentials at all.
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/encounters"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        var act = async () => await connection.StartAsync();

        await act.Should().ThrowAsync<HttpRequestException>("an unauthenticated connection to an [Authorize] hub must be rejected, not silently allowed through");

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Connecting_as_a_jogador_is_rejected()
    {
        // EncounterHub is [Authorize(Roles = "GM")] — the plan's own Global Constraint and R0002's
        // "acesso 100% do GM... Não há visualização de jogador para o Encontro" both require this.
        // A valid, linked Jogador token must still be rejected, not silently allowed through.
        var gmToken = await RegisterGmAndGetTokenAsync("HubGm4", "hub4@teste.com");
        var (_, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "HubPlayer4", "hubplayer4@teste.com");

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/encounters"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(playerToken);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        var act = async () => await connection.StartAsync();

        await act.Should().ThrowAsync<HttpRequestException>("a valid Jogador token must still be rejected by a GM-only hub");

        await connection.DisposeAsync();
    }
}
