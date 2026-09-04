using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Images;

namespace RuinaRPG.Tests.Integration.Controllers;

public class ImagesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    public ImagesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private static MultipartFormDataContent BuildUpload(byte[] bytes, string fileName = "test.png", string? campaignId = null)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);
        if (campaignId is not null)
            content.Add(new StringContent(campaignId), "campaignId");
        return content;
    }

    [Fact]
    public async Task Upload_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/images", BuildUpload(PngBytes));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_a_valid_png_returns_201_with_a_usable_url()
    {
        var token = await RegisterGmAndGetTokenAsync("ImageGm1", "image1@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        body!.Url.Should().EndWith(".png");
        // nginx serves the images volume at the /images/ web path (docker-compose.yml's
        // images:/usr/share/nginx/html/images:ro mount) — a URL missing that prefix falls
        // through nginx's SPA fallback to index.html instead of the actual file.
        body.Url.Should().StartWith("/images/");
    }

    [Fact]
    public async Task Upload_real_png_bytes_named_with_an_html_extension_is_still_stored_as_png()
    {
        // The client-supplied filename must never determine the on-disk extension: real PNG
        // bytes named "payload.html" must be saved as .png (the format detected from the actual
        // bytes), not .html — otherwise nginx would serve them same-origin as text/html, a
        // stored-XSS vector against a page that keeps JWTs in localStorage.
        var token = await RegisterGmAndGetTokenAsync("ImageGmXss", "imagexss@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images")
        {
            Content = BuildUpload(PngBytes, "payload.html")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        body!.Url.Should().EndWith(".png");
        body.Url.Should().NotEndWith(".html");
    }

    [Fact]
    public async Task Upload_as_a_jogador_returns_201()
    {
        // Image upload is deliberately [Authorize]-only, not GM-restricted, so a Jogador can
        // later upload their own character portraits/diary images. Nothing previously pinned
        // that a Jogador actually succeeds here — only that an unauthenticated caller fails.
        var gmToken = await RegisterGmAndGetTokenAsync("ImageGmForJogador", "imagegmforjogador@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "ImageJogador1", "imagejogador1@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jogadorToken);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Upload_bytes_that_are_not_a_real_image_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ImageGm2", "image2@teste.com");
        var fakeBytes = "not an image"u8.ToArray();
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(fakeBytes, "fake.png") };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mine_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/images/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_returns_only_images_uploaded_by_the_caller_R0010()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("ImageGm3", "image3@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("ImageGm4", "image4@teste.com");
        var uploadA = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        uploadA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        await _client.SendAsync(uploadA);
        var uploadB = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        uploadB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await _client.SendAsync(uploadB);

        var mineMessage = new HttpRequestMessage(HttpMethod.Get, "/api/images/mine");
        mineMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(mineMessage);

        var body = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        body.Should().ContainSingle();
        body!.Single().Url.Should().StartWith("/images/");
    }

    [Fact]
    public async Task Mine_returns_the_callers_images_newest_first()
    {
        var token = await RegisterGmAndGetTokenAsync("ImageGm5", "image5@teste.com");

        var firstUpload = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        firstUpload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var firstResponse = await _client.SendAsync(firstUpload);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<ImageUploadResponse>();

        // A short delay guarantees the two uploads land at distinct CreatedAt instants, so
        // the "newest first" ordering is deterministically observable rather than depending
        // on incidental request latency between the two SendAsync calls.
        await Task.Delay(10);

        var secondUpload = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        secondUpload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var secondResponse = await _client.SendAsync(secondUpload);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<ImageUploadResponse>();

        var mineMessage = new HttpRequestMessage(HttpMethod.Get, "/api/images/mine");
        mineMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(mineMessage);

        var body = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        body.Should().HaveCount(2);
        body!.Select(i => i.Id).Should().Equal(secondBody!.Id, firstBody!.Id);
        body.Should().BeInDescendingOrder(i => i.CreatedAt);
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedWithIdAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        var meBody = await meResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Auth.MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAndAddMemberAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/campaigns")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", gmToken) },
            Content = JsonContent.Create(new RuinaRPG.Contracts.Campaigns.CreateCampaignRequest("Campanha Upload", ""))
        });
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.CampaignResponse>())!.Id;

        var memberMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/campaigns/{campaignId}/members")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", gmToken) },
            Content = JsonContent.Create(new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId))
        };
        await _client.SendAsync(memberMessage);
        return campaignId;
    }

    [Fact]
    public async Task Upload_by_a_jogador_with_a_campaignId_auto_attaches_it_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ImageAutoGm1", "imageautogm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedWithIdAsync(gmToken, "ImageAutoPlayer1", "imageautoplayer1@teste.com");
        var campaignId = await CreateCampaignAndAddMemberAsync(gmToken, playerId);
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes, campaignId: campaignId) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var imageId = (await response.Content.ReadFromJsonAsync<ImageUploadResponse>())!.Id;

        var availableMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-images");
        availableMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
        var availableResponse = await _client.SendAsync(availableMessage);
        var available = await availableResponse.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        available!.Should().ContainSingle(i => i.Id == imageId);
    }

    [Fact]
    public async Task Upload_by_a_gm_with_a_campaignId_does_not_auto_attach()
    {
        // GM uploads stay private-by-default via the existing GM-driven Anexos flow — a
        // campaignId on the upload itself is only a Jogador-side convenience.
        var gmToken = await RegisterGmAndGetTokenAsync("ImageAutoGm2", "imageautogm2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedWithIdAsync(gmToken, "ImageAutoPlayer2", "imageautoplayer2@teste.com");
        var campaignId = await CreateCampaignAndAddMemberAsync(gmToken, playerId);
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes, campaignId: campaignId) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);

        var response = await _client.SendAsync(message);
        var imageId = (await response.Content.ReadFromJsonAsync<ImageUploadResponse>())!.Id;

        var availableMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-images");
        availableMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var availableResponse = await _client.SendAsync(availableMessage);
        var available = await availableResponse.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        available!.Should().NotContain(i => i.Id == imageId);
    }

    [Fact]
    public async Task Upload_with_a_campaignId_for_a_campaign_the_jogador_is_not_a_member_of_still_succeeds_without_attaching()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ImageAutoGm3", "imageautogm3@teste.com");
        var (_, playerToken) = await RegisterJogadorLinkedWithIdAsync(gmToken, "ImageAutoPlayer3", "imageautoplayer3@teste.com");
        var campaignId = await CreateCampaignAndAddMemberAsync(gmToken, Guid.NewGuid().ToString()); // player never added
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes, campaignId: campaignId) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created); // upload itself never fails on this
        var imageId = (await response.Content.ReadFromJsonAsync<ImageUploadResponse>())!.Id;

        // GM is always an allowed caller per CampaignCatalogController's membership check, even
        // though the player who uploaded isn't a member of this campaign.
        var availableMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-images");
        availableMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var availableResponse = await _client.SendAsync(availableMessage);
        var available = await availableResponse.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        available!.Should().NotContain(i => i.Id == imageId);
    }
}
