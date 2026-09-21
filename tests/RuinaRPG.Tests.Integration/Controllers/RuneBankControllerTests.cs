using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.Runes;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RuneBankControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RuneBankControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<RuneBankEntryResponse> CreateAsync(string token, string nome, string descricao, int grau)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", token, new CreateRuneBankEntryRequest(nome, descricao, grau)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!;
    }

    private async Task<List<RuneBankEntryResponse>> ListAsync(string token, string query = "")
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/rune-bank{query}", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/rune-bank", new CreateRuneBankEntryRequest("Runa", "Desc.", 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_as_the_gm_returns_201_with_the_saved_fields()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm1", "runebankgm1@teste.com");

        var created = await CreateAsync(token, "Runa do Fogo", "Queima o alvo.", 2);

        created.Nome.Should().Be("Runa do Fogo");
        created.Descricao.Should().Be("Queima o alvo.");
        created.Grau.Should().Be(2);
        created.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Every_endpoint_is_forbidden_to_a_jogador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBankGm2", "runebankgm2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "RuneBankJogador2", "runebankjogador2@teste.com");
        var entry = await CreateAsync(gmToken, "Runa Secreta", "Só o GM vê.", 1);

        var post = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", jogadorToken, new CreateRuneBankEntryRequest("X", "Y", 1)));
        var get = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rune-bank", jogadorToken));
        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", jogadorToken, new UpdateRuneBankEntryRequest("X", "Y", 1)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", jogadorToken));

        post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        get.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_returns_only_the_entries_of_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("RuneBankGmA3", "runebankgma3@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("RuneBankGmB3", "runebankgmb3@teste.com");
        var minha = await CreateAsync(tokenA, "Runa do GM A", "A.", 1);
        var alheia = await CreateAsync(tokenB, "Runa do GM B", "B.", 1);

        var body = await ListAsync(tokenA);

        body.Should().Contain(e => e.Id == minha.Id);
        body.Should().NotContain(e => e.Id == alheia.Id);
    }

    [Fact]
    public async Task List_filters_by_nome_case_insensitively_and_by_grau()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm4", "runebankgm4@teste.com");
        var fogo1 = await CreateAsync(token, "Runa do Fogo", "F.", 1);
        var fogo3 = await CreateAsync(token, "Runa do FOGO Maior", "F3.", 3);
        var gelo1 = await CreateAsync(token, "Runa do Gelo", "G.", 1);

        var porNome = await ListAsync(token, "?nome=fogo");
        porNome.Select(e => e.Id).Should().BeEquivalentTo([fogo1.Id, fogo3.Id]);

        var porGrau = await ListAsync(token, "?grau=1");
        porGrau.Select(e => e.Id).Should().BeEquivalentTo([fogo1.Id, gelo1.Id]);

        var combinado = await ListAsync(token, "?nome=fogo&grau=3");
        combinado.Select(e => e.Id).Should().BeEquivalentTo([fogo3.Id]);
    }

    [Fact]
    public async Task Update_replaces_the_fields_and_returns_204()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm5", "runebankgm5@teste.com");
        var entry = await CreateAsync(token, "Runa Velha", "Antiga.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", token, new UpdateRuneBankEntryRequest("Runa Nova", "Atual.", 4)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var atualizada = (await ListAsync(token)).Single(e => e.Id == entry.Id);
        atualizada.Nome.Should().Be("Runa Nova");
        atualizada.Descricao.Should().Be("Atual.");
        atualizada.Grau.Should().Be(4);
    }

    [Fact]
    public async Task Update_and_delete_of_another_gms_entry_return_404()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("RuneBankGmA6", "runebankgma6@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("RuneBankGmB6", "runebankgmb6@teste.com");
        var entry = await CreateAsync(tokenA, "Runa do A", "A.", 1);

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", tokenB, new UpdateRuneBankEntryRequest("Roubada", "X.", 9)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", tokenB));

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ListAsync(tokenA)).Single(e => e.Id == entry.Id).Nome.Should().Be("Runa do A");
    }

    [Fact]
    public async Task Delete_removes_the_entry_and_returns_204()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm7", "runebankgm7@teste.com");
        var entry = await CreateAsync(token, "Runa Efêmera", "Some.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", token));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(token)).Should().NotContain(e => e.Id == entry.Id);
    }

    [Fact]
    public async Task Update_and_delete_of_an_unknown_id_return_404()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm8", "runebankgm8@teste.com");

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{Guid.NewGuid()}", token, new UpdateRuneBankEntryRequest("X", "Y", 1)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{Guid.NewGuid()}", token));

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- Imagem opcional ----

    private async Task<(string Id, string Url)> UploadImageAsync(string token)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Images.ImageUploadResponse>();
        return (body!.Id, body.Url);
    }

    [Fact]
    public async Task Create_with_the_gms_own_image_returns_201_with_ImageId_and_ImageUrl()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankImgGm1", "runebankimg1@teste.com");
        var (imageId, imageUrl) = await UploadImageAsync(token);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", token, new CreateRuneBankEntryRequest("Runa", "Desc.", 1, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!;
        created.ImageId.Should().Be(imageId);
        created.ImageUrl.Should().Be(imageUrl);
    }

    [Fact]
    public async Task Create_with_another_users_image_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankImgGm2", "runebankimg2@teste.com");
        var otherToken = await RegisterGmAndGetTokenAsync("RuneBankImgGm2b", "runebankimg2b@teste.com");
        var (foreignImage, _) = await UploadImageAsync(otherToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", token, new CreateRuneBankEntryRequest("Runa", "Desc.", 1, foreignImage)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Create_with_a_malformed_or_unknown_image_id_returns_400(string imageId)
    {
        var token = await RegisterGmAndGetTokenAsync($"RuneBankImgGm3{imageId.Length}", $"runebankimg3{imageId.Length}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", token, new CreateRuneBankEntryRequest("Runa", "Desc.", 1, imageId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_a_blank_image_id_means_no_image()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankImgGm4", "runebankimg4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", token, new CreateRuneBankEntryRequest("Runa", "Desc.", 1, "")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!;
        created.ImageId.Should().BeNull();
        created.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Update_sets_and_then_clears_the_image_and_the_list_carries_the_ImageUrl()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankImgGm5", "runebankimg5@teste.com");
        var (imageId, imageUrl) = await UploadImageAsync(token);
        var entry = await CreateAsync(token, "Runa", "Desc.", 1);

        var set = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", token, new UpdateRuneBankEntryRequest("Runa", "Desc.", 1, imageId)));
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var comImagem = (await ListAsync(token)).Single(e => e.Id == entry.Id);
        comImagem.ImageId.Should().Be(imageId);
        comImagem.ImageUrl.Should().Be(imageUrl);

        var clear = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", token, new UpdateRuneBankEntryRequest("Runa", "Desc.", 1, "")));
        clear.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var semImagem = (await ListAsync(token)).Single(e => e.Id == entry.Id);
        semImagem.ImageId.Should().BeNull();
        semImagem.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Update_with_another_users_or_malformed_image_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankImgGm6", "runebankimg6@teste.com");
        var otherToken = await RegisterGmAndGetTokenAsync("RuneBankImgGm6b", "runebankimg6b@teste.com");
        var (foreignImage, _) = await UploadImageAsync(otherToken);
        var entry = await CreateAsync(token, "Runa", "Desc.", 1);

        var foreign = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", token, new UpdateRuneBankEntryRequest("Runa", "Desc.", 1, foreignImage)));
        var malformed = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", token, new UpdateRuneBankEntryRequest("Runa", "Desc.", 1, "xx")));

        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Imagem de um jogador que chega à cópia automática do banco ----

    private async Task<(string Id, string Url)> UploadImageAsync(string token, string campaignId)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        content.Add(new StringContent(campaignId), "campaignId");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Images.ImageUploadResponse>();
        return (body!.Id, body.Url);
    }

    private sealed record PlayerRuneSetup(string GmToken, string PlayerToken, string CampaignId, string ImageId, string ImageUrl, RuneBankEntryResponse BankCopy);

    /// <summary>
    /// Um jogador sobe uma imagem (auto-anexada como pública à campanha) e adiciona uma Runa do zero com ela;
    /// a cópia automática no banco do GM passa a guardar a imagem do jogador, que o GM não subiu.
    /// </summary>
    private async Task<PlayerRuneSetup> BuildPlayerRuneSetupAsync(string suffix)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"RuneBankPvGm{suffix}", $"runebankpvgm{suffix}@teste.com");

        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;
        var register = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"RuneBankPvPl{suffix}", $"runebankpvpl{suffix}@teste.com", "Senha!123", "Senha!123", code));
        var playerToken = (await register.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", playerToken));
        var playerId = (await me.Content.ReadFromJsonAsync<MeResponse>())!.Id;

        var campaign = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheet = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheet.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var (imageId, imageUrl) = await UploadImageAsync(playerToken, campaignId);
        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa do Jogador", "Com imagem dele.", 1, null, imageId)));
        add.StatusCode.Should().Be(HttpStatusCode.Created);

        var bankCopy = (await ListAsync(gmToken)).Single(e => e.Nome == "Runa do Jogador");
        return new PlayerRuneSetup(gmToken, playerToken, campaignId, imageId, imageUrl, bankCopy);
    }

    [Fact]
    public async Task A_jogadors_image_reaches_the_bank_copy_and_the_public_attachment()
    {
        var setup = await BuildPlayerRuneSetupAsync("1");

        setup.BankCopy.ImageId.Should().Be(setup.ImageId);
        setup.BankCopy.ImageUrl.Should().Be(setup.ImageUrl);
        var available = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-runes", setup.PlayerToken));
        (await available.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!
            .Should().ContainSingle(e => e.Id == setup.BankCopy.Id && e.ImageId == setup.ImageId && e.ImageUrl == setup.ImageUrl);
    }

    [Fact]
    public async Task The_gm_can_resend_the_entrys_current_image_even_though_a_jogador_uploaded_it()
    {
        var setup = await BuildPlayerRuneSetupAsync("2");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{setup.BankCopy.Id}", setup.GmToken,
            new UpdateRuneBankEntryRequest("Runa Renomeada", "Nova.", 2, setup.ImageId)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var entry = (await ListAsync(setup.GmToken)).Single(e => e.Id == setup.BankCopy.Id);
        entry.Nome.Should().Be("Runa Renomeada");
        entry.ImageId.Should().Be(setup.ImageId);
        entry.ImageUrl.Should().Be(setup.ImageUrl);
    }

    [Fact]
    public async Task The_gm_cannot_switch_to_a_different_image_they_did_not_upload()
    {
        var setup = await BuildPlayerRuneSetupAsync("3");
        var otherGmToken = await RegisterGmAndGetTokenAsync("RuneBankPvGm3b", "runebankpvgm3b@teste.com");
        var (foreignImage, _) = await UploadImageAsync(otherGmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{setup.BankCopy.Id}", setup.GmToken,
            new UpdateRuneBankEntryRequest("Runa do Jogador", "Com imagem dele.", 1, foreignImage)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Imagem não encontrada.");
        (await ListAsync(setup.GmToken)).Single(e => e.Id == setup.BankCopy.Id).ImageId.Should().Be(setup.ImageId);
    }

    [Fact]
    public async Task The_gm_can_replace_the_jogadors_image_with_one_they_uploaded()
    {
        var setup = await BuildPlayerRuneSetupAsync("4");
        var (ownImage, ownUrl) = await UploadImageAsync(setup.GmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{setup.BankCopy.Id}", setup.GmToken,
            new UpdateRuneBankEntryRequest("Runa do Jogador", "Com imagem dele.", 1, ownImage)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var entry = (await ListAsync(setup.GmToken)).Single(e => e.Id == setup.BankCopy.Id);
        entry.ImageId.Should().Be(ownImage);
        entry.ImageUrl.Should().Be(ownUrl);
    }

    [Fact]
    public async Task The_gm_can_clear_the_jogadors_image()
    {
        var setup = await BuildPlayerRuneSetupAsync("5");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{setup.BankCopy.Id}", setup.GmToken,
            new UpdateRuneBankEntryRequest("Runa do Jogador", "Com imagem dele.", 1, "")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var entry = (await ListAsync(setup.GmToken)).Single(e => e.Id == setup.BankCopy.Id);
        entry.ImageId.Should().BeNull();
        entry.ImageUrl.Should().BeNull();
    }
}
