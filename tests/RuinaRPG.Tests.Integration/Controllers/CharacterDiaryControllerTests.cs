using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Diary;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterDiaryControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    [Fact]
    public async Task Create_and_list_round_trips_an_entry_as_the_owner()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DiaryGm1", "diary1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DiaryPlayer1", "diaryplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/diary", playerToken,
            new CreateDiaryEntryRequest("Chegamos à vila hoje.", [])));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/diary", playerToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
        body!.Should().ContainSingle(e => e.Texto == "Chegamos à vila hoje.");
    }

    [Fact]
    public async Task Get_by_the_campaigns_gm_also_succeeds_readonly_visibility_shared_with_owner()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DiaryGm2", "diary2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DiaryPlayer2", "diaryplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/diary", playerToken,
            new CreateDiaryEntryRequest("Segredo do jogador... nada tão secreto assim.", [])));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/diary", gmToken));

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
        body!.Should().ContainSingle(e => e.Texto == "Segredo do jogador... nada tão secreto assim.");
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DiaryGm3", "diary3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DiaryPlayer3", "diaryplayer3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "DiaryPlayer3b", "diaryplayer3b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/diary", playerToken,
            new CreateDiaryEntryRequest("Texto original.", [])));
        var entryId = (await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>())!.Id;

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/diary/{entryId}", otherToken,
            new UpdateDiaryEntryRequest("Texto adulterado.", [])));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_removes_the_entry()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DiaryGm4", "diary4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DiaryPlayer4", "diaryplayer4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/diary", playerToken,
            new CreateDiaryEntryRequest("Para excluir.", [])));
        var entryId = (await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>())!.Id;

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/diary/{entryId}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/diary", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
        body!.Should().NotContain(e => e.Id == entryId);
    }

    [Fact]
    public async Task List_and_Update_by_an_unrelated_jogador_return_403_not_the_entries()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DiaryGm5", "diary5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "DiaryPlayer5", "diaryplayer5@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "DiaryPlayer5b", "diaryplayer5b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/diary", playerToken,
            new CreateDiaryEntryRequest("Não é da sua conta.", [])));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/diary", otherToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/diary", otherToken,
            new CreateDiaryEntryRequest("Intrusão.", [])));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
