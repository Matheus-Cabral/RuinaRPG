using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Images;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest("Nome", "Desc"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_the_campaign()
    {
        var token = await RegisterGmAndGetTokenAsync("CampGm1", "camp1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", token, new CreateCampaignRequest("A Ruína Aguarda", "Descrição")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignResponse>();
        body!.Nome.Should().Be("A Ruína Aguarda");
    }

    [Fact]
    public async Task List_returns_only_campaigns_owned_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("CampGmA", "campgma@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("CampGmB", "campgmb@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", tokenA, new CreateCampaignRequest("Campanha A", "")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", tokenB, new CreateCampaignRequest("Campanha B", "")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/campaigns", tokenA));

        var body = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>();
        body!.Should().ContainSingle(c => c.Nome == "Campanha A");
    }

    private async Task<string> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        return meBody!.Id;
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private static MultipartFormDataContent BuildImageUpload(string fileName = "test.png")
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private async Task<string> UploadImageAsync(string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildImageUpload() };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task AddMember_with_a_malformed_UserId_returns_400_instead_of_throwing()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampMemberGmMalformed", "campmembermalformed@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Malformada");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest("not-a-guid")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddMember_a_player_linked_to_the_caller_returns_204_and_they_appear_in_members()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampMemberGm1", "campmember1@teste.com");
        var playerId = await RegisterJogadorLinkedToAsync(gmToken, "CampMemberPlayer1", "campmemberplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Membro");

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/members", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CampaignMemberResponse>>();
        body!.Should().ContainSingle(m => m.Nickname == "CampMemberPlayer1");
    }

    [Fact]
    public async Task AddMember_a_player_not_linked_to_the_caller_returns_400()
    {
        var gmTokenA = await RegisterGmAndGetTokenAsync("CampMemberGmA", "campmembergma@teste.com");
        var gmTokenB = await RegisterGmAndGetTokenAsync("CampMemberGmB", "campmembergmb@teste.com");
        var playerOfB = await RegisterJogadorLinkedToAsync(gmTokenB, "CampMemberPlayerB", "campmemberplayerb@teste.com");
        var campaignOfA = await CreateCampaignAsync(gmTokenA, "Campanha de A");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignOfA}/members", gmTokenA, new AddCampaignMemberRequest(playerOfB)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddMember_to_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CampMemberOwner", "campmemberowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CampMemberOther", "campmemberother@teste.com");
        var playerOfOther = await RegisterJogadorLinkedToAsync(gmTokenOther, "CampMemberPlayerOther", "campmemberplayerother@teste.com");
        var campaignOfOwner = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignOfOwner}/members", gmTokenOther, new AddCampaignMemberRequest(playerOfOther)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Diary_create_and_list_round_trips_an_entry()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGm1", "campdiary1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Diário");

        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
            new CreateDiaryEntryRequest("Os jogadores chegaram à vila.", [])));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/diary", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
        body!.Should().ContainSingle(e => e.Texto == "Os jogadores chegaram à vila.");
    }

    [Fact]
    public async Task Diary_update_changes_the_text()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGm2", "campdiary2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Diário 2");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
            new CreateDiaryEntryRequest("Texto original.", [])));
        var entryId = (await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>())!.Id;

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/diary/{entryId}", gmToken,
            new UpdateDiaryEntryRequest("Texto revisado.", [])));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/diary", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
        body!.Should().ContainSingle(e => e.Texto == "Texto revisado.");
    }

    [Fact]
    public async Task Diary_delete_removes_the_entry()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGm3", "campdiary3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Diário 3");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
            new CreateDiaryEntryRequest("Para excluir.", [])));
        var entryId = (await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>())!.Id;

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campaignId}/diary/{entryId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/diary", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
        body!.Should().BeEmpty();
    }

    [Fact]
    public async Task Diary_actions_on_a_campaign_owned_by_another_gm_return_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CampDiaryOwner", "campdiaryowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CampDiaryOther", "campdiaryother@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Privada");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmTokenOther,
            new CreateDiaryEntryRequest("Invasão.", [])));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Diary_create_with_an_existing_image_returns_ImageUrls_with_the_images_prefix()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGmImg", "campdiaryimg@teste.com");
        var imageId = await UploadImageAsync(gmToken);
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Imagem no Diário");

        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
            new CreateDiaryEntryRequest("Encontraram um mapa.", [imageId])));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>();
        body!.ImageUrls.Should().ContainSingle(url => url.StartsWith("/images/"));
    }

    [Fact]
    public async Task Diary_create_with_a_malformed_ImageId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGmBadImg", "campdiarybadimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Imagem Inválida");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
            new CreateDiaryEntryRequest("Texto qualquer.", ["not-a-guid"])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Diary_update_with_a_malformed_ImageId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGmBadImgUpd", "campdiarybadimgupd@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Imagem Inválida na Edição");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
            new CreateDiaryEntryRequest("Texto original.", [])));
        var entryId = (await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>())!.Id;

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/diary/{entryId}", gmToken,
            new UpdateDiaryEntryRequest("Texto revisado.", ["not-a-guid"])));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Diary_create_with_an_image_owned_by_another_gm_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CampDiaryImgOwner", "campdiaryimgowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CampDiaryImgOther", "campdiaryimgother@teste.com");
        var imageOfOther = await UploadImageAsync(gmTokenOther);
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha com Imagem Alheia");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmTokenOwner,
            new CreateDiaryEntryRequest("Texto qualquer.", [imageOfOther])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Diary_create_with_a_nonexistent_ImageId_returns_400_instead_of_500()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGmMissingImg", "campdiarymissingimg@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Imagem Inexistente");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
            new CreateDiaryEntryRequest("Texto qualquer.", [Guid.NewGuid().ToString()])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Diary_update_with_an_image_owned_by_another_gm_returns_400()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CampDiaryImgOwnerUpd", "campdiaryimgownerupd@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CampDiaryImgOtherUpd", "campdiaryimgotherupd@teste.com");
        var imageOfOther = await UploadImageAsync(gmTokenOther);
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha com Imagem Alheia na Edição");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmTokenOwner,
            new CreateDiaryEntryRequest("Texto original.", [])));
        var entryId = (await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>())!.Id;

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/diary/{entryId}", gmTokenOwner,
            new UpdateDiaryEntryRequest("Texto revisado.", [imageOfOther])));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddMember_the_same_player_twice_returns_204_both_times()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CampMemberGmTwice", "campmembertwice@teste.com");
        var playerId = await RegisterJogadorLinkedToAsync(gmToken, "CampMemberPlayerTwice", "campmemberplayertwice@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Membro Duplicado");

        var firstResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var secondResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
