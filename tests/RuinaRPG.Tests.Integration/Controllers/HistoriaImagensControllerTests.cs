using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Aba Antecedentes (Personagem e NPC): a galeria de imagens da História (GET/PUT …/historia/imagens) e as
/// imagens dentro do texto (PUT …/historia só mantém &lt;img&gt; de imagens do app que quem salva pode usar).
/// </summary>
public class HistoriaImagensControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public HistoriaImagensControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"HImgGm{tag}", $"himggm{tag}@teste.com", "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorAsync(string gmToken, string tag)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"HImgJog{tag}", $"himgjog{tag}@teste.com", "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens!.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private sealed record Setup(string GmToken, string PlayerToken, string PlayerId, string CampaignId, string SheetId);

    /// <summary>GM + jogador membro da campanha + uma Ficha de Personagem do jogador.</summary>
    private async Task<Setup> CharacterSetupAsync(string tag)
    {
        var gmToken = await RegisterGmAsync(tag);
        var (playerId, playerToken) = await RegisterJogadorAsync(gmToken, tag);
        var campaign = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest($"Campanha HImg {tag}", "")));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheet = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return new Setup(gmToken, playerToken, playerId, campaignId, (await sheet.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id);
    }

    /// <summary>GM + jogador membro da campanha + um NPC concedido ao jogador (cópia de sourceNpcId, ou em branco).</summary>
    private async Task<Setup> GrantedNpcSetupAsync(string tag, Func<string, Task<string>>? prepareSource = null)
    {
        var gmToken = await RegisterGmAsync(tag);
        var (playerId, playerToken) = await RegisterJogadorAsync(gmToken, tag);
        var campaign = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest($"Campanha HImg {tag}", "")));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sourceId = prepareSource is null ? null : await prepareSource(gmToken);
        var grant = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, new GrantSheetRequest(playerId, "Npc", sourceId)));
        grant.StatusCode.Should().Be(HttpStatusCode.Created);
        return new Setup(gmToken, playerToken, playerId, campaignId, (await grant.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId);
    }

    private async Task<string> CreateNpcAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<(string Id, string Url)> UploadAsync(string token, string? campaignId = null)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        if (campaignId is not null)
            content.Add(new StringContent(campaignId), "campaignId");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        return (body!.Id, body.Url);
    }

    private Task<HttpResponseMessage> PutGalleryAsync(string basePath, string token, params string[] imageIds) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{basePath}/historia/imagens", token, new UpdateHistoriaImagensRequest(imageIds.ToList())));

    private async Task<List<HistoriaImagemResponse>> GetGalleryAsync(string basePath, string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"{basePath}/historia/imagens", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<HistoriaImagemResponse>>())!;
    }

    private Task<HttpResponseMessage> PutHistoriaAsync(string basePath, string token, string html) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{basePath}/historia", token, new UpdateHistoriaRequest(html)));

    private async Task<string?> GetCharacterHistoriaAsync(string sheetId, string token) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", token))).Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Historia;

    private async Task<string?> GetNpcHistoriaAsync(string sheetId, string token) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", token))).Content.ReadFromJsonAsync<NpcSheetResponse>())!.Historia;

    private static string Character(string sheetId) => $"/api/character-sheets/{sheetId}";
    private static string Npc(string sheetId) => $"/api/npc-sheets/{sheetId}";

    // ---------------------------------------------------------------- Personagem: galeria

    [Fact]
    public async Task Character_gallery_starts_empty()
    {
        var s = await CharacterSetupAsync("C0");

        (await GetGalleryAsync(Character(s.SheetId), s.PlayerToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Character_gallery_the_owner_attaches_images_and_both_owner_and_gm_see_them_in_order()
    {
        var s = await CharacterSetupAsync("C1");
        var a = await UploadAsync(s.PlayerToken);
        var b = await UploadAsync(s.PlayerToken);

        var put = await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, b.Id, a.Id);

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var returned = await put.Content.ReadFromJsonAsync<List<HistoriaImagemResponse>>();
        var expected = new[] { new HistoriaImagemResponse(b.Id, b.Url), new HistoriaImagemResponse(a.Id, a.Url) };
        returned.Should().Equal(expected);
        (await GetGalleryAsync(Character(s.SheetId), s.PlayerToken)).Should().Equal(expected);
        (await GetGalleryAsync(Character(s.SheetId), s.GmToken)).Should().Equal(expected);
    }

    [Fact]
    public async Task Character_gallery_put_replaces_the_list_so_it_reorders_and_removes()
    {
        var s = await CharacterSetupAsync("C2");
        var a = await UploadAsync(s.PlayerToken);
        var b = await UploadAsync(s.PlayerToken);
        var c = await UploadAsync(s.PlayerToken);
        await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, a.Id, b.Id, c.Id);

        var put = await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, c.Id, a.Id);

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetGalleryAsync(Character(s.SheetId), s.GmToken)).Select(i => i.ImageId).Should().Equal(c.Id, a.Id);

        (await PutGalleryAsync(Character(s.SheetId), s.PlayerToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetGalleryAsync(Character(s.SheetId), s.GmToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Character_gallery_a_repeated_id_is_stored_once()
    {
        var s = await CharacterSetupAsync("C3");
        var a = await UploadAsync(s.PlayerToken);

        var put = await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, a.Id, a.Id);

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetGalleryAsync(Character(s.SheetId), s.PlayerToken)).Should().ContainSingle();
    }

    [Fact]
    public async Task Character_gallery_an_image_uploaded_by_someone_else_returns_400_and_changes_nothing()
    {
        var s = await CharacterSetupAsync("C4");
        var other = await RegisterGmAsync("C4outro");
        var mine = await UploadAsync(s.PlayerToken);
        var foreign = await UploadAsync(other);
        await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, mine.Id);

        var put = await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, mine.Id, foreign.Id);

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await put.Content.ReadAsStringAsync()).Should().Contain("Imagem não encontrada.");
        (await GetGalleryAsync(Character(s.SheetId), s.PlayerToken)).Select(i => i.ImageId).Should().Equal(mine.Id);
    }

    [Theory]
    [InlineData("nao-e-um-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Character_gallery_a_malformed_or_unknown_id_returns_400(string id)
    {
        var s = await CharacterSetupAsync("C5" + id[..3]);

        (await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Character_gallery_more_than_the_maximum_returns_400()
    {
        var s = await CharacterSetupAsync("C6");
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid().ToString()).ToArray();

        var put = await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, ids);

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await put.Content.ReadAsStringAsync()).Should().Contain("no máximo 50 imagens");
    }

    [Fact]
    public async Task Character_gallery_the_gm_can_save_it_keeping_the_players_images_and_adding_his_own()
    {
        var s = await CharacterSetupAsync("C7");
        var daJogadora = await UploadAsync(s.PlayerToken);
        var doGm = await UploadAsync(s.GmToken);
        await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, daJogadora.Id);

        var put = await PutGalleryAsync(Character(s.SheetId), s.GmToken, daJogadora.Id, doGm.Id);

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetGalleryAsync(Character(s.SheetId), s.PlayerToken)).Select(i => i.ImageId).Should().Equal(daJogadora.Id, doGm.Id);
        // …and the player, saving later, keeps the GM's image without owning it.
        (await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, doGm.Id, daJogadora.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Character_gallery_the_gm_cannot_add_a_players_image_that_is_not_in_the_gallery()
    {
        var s = await CharacterSetupAsync("C8");
        var daJogadora = await UploadAsync(s.PlayerToken);

        (await PutGalleryAsync(Character(s.SheetId), s.GmToken, daJogadora.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Character_gallery_a_player_can_use_an_image_the_gm_made_public_in_the_campaign_but_not_a_private_one()
    {
        var s = await CharacterSetupAsync("C9");
        var publica = await UploadAsync(s.GmToken);
        var privada = await UploadAsync(s.GmToken);
        var attach = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{s.CampaignId}/attachments", s.GmToken, new AttachToCampaignRequest(null, null, null, null, publica.Id)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{s.CampaignId}/attachments/{attachmentId}/visibility", s.GmToken, true))).EnsureSuccessStatusCode();
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{s.CampaignId}/attachments", s.GmToken, new AttachToCampaignRequest(null, null, null, null, privada.Id)));

        (await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, publica.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PutGalleryAsync(Character(s.SheetId), s.PlayerToken, publica.Id, privada.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Character_gallery_is_closed_to_a_stranger_and_to_anonymous_callers()
    {
        var s = await CharacterSetupAsync("C10");
        var (_, strangerToken) = await RegisterJogadorAsync(s.GmToken, "C10x");
        var strangerImage = await UploadAsync(strangerToken);
        var otherGm = await RegisterGmAsync("C10outro");

        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"{Character(s.SheetId)}/historia/imagens", strangerToken))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"{Character(s.SheetId)}/historia/imagens", otherGm))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutGalleryAsync(Character(s.SheetId), strangerToken, strangerImage.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"{Character(s.SheetId)}/historia/imagens")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.PutAsJsonAsync($"{Character(s.SheetId)}/historia/imagens", new UpdateHistoriaImagensRequest([]))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await GetGalleryAsync(Character(s.SheetId), s.PlayerToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Character_gallery_on_a_nonexistent_sheet_returns_404()
    {
        var gmToken = await RegisterGmAsync("C11");

        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"{Character(Guid.NewGuid().ToString())}/historia/imagens", gmToken))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutGalleryAsync(Character(Guid.NewGuid().ToString()), gmToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- Personagem: imagens dentro do texto

    [Fact]
    public async Task Character_historia_keeps_an_app_image_of_the_caller_and_strips_external_ones()
    {
        var s = await CharacterSetupAsync("T1");
        var mine = await UploadAsync(s.PlayerToken);

        var put = await PutHistoriaAsync(Character(s.SheetId), s.PlayerToken,
            $"<p>Retrato:</p><p><img src=\"{mine.Url}\" alt=\"Lira\" width=\"300\" onerror=\"alert(1)\" srcset=\"https://mal.com/p.png 2x\"></p>" +
            "<p><img src=\"https://mal.com/pixel.gif\"><img src=\"data:image/png;base64,iVBORw0KGgo=\"><img src=\"//mal.com/p.png\"></p>");

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        foreach (var token in new[] { s.PlayerToken, s.GmToken })
        {
            var historia = await GetCharacterHistoriaAsync(s.SheetId, token);
            historia.Should().Contain($"<img src=\"{mine.Url}\" alt=\"Lira\" width=\"300\">")
                .And.NotContain("mal.com").And.NotContain("data:").And.NotContain("onerror").And.NotContain("srcset");
        }
    }

    [Fact]
    public async Task Character_historia_strips_an_app_image_the_caller_cannot_use()
    {
        var s = await CharacterSetupAsync("T2");
        var other = await RegisterGmAsync("T2outro");
        var foreign = await UploadAsync(other);
        var privadaDoGm = await UploadAsync(s.GmToken);

        var put = await PutHistoriaAsync(Character(s.SheetId), s.PlayerToken,
            $"<p>texto</p><img src=\"{foreign.Url}\"><img src=\"{privadaDoGm.Url}\"><img src=\"/images/{Guid.NewGuid()}.png\">");

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetCharacterHistoriaAsync(s.SheetId, s.PlayerToken)).Should().Contain("texto").And.NotContain("<img");
    }

    [Fact]
    public async Task Character_historia_saved_by_the_gm_keeps_the_players_inline_image_already_there()
    {
        var s = await CharacterSetupAsync("T3");
        var daJogadora = await UploadAsync(s.PlayerToken);
        var doGm = await UploadAsync(s.GmToken);
        await PutHistoriaAsync(Character(s.SheetId), s.PlayerToken, $"<p>antes</p><img src=\"{daJogadora.Url}\">");

        var put = await PutHistoriaAsync(Character(s.SheetId), s.GmToken, $"<p>depois</p><img src=\"{daJogadora.Url}\"><img src=\"{doGm.Url}\">");

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetCharacterHistoriaAsync(s.SheetId, s.PlayerToken)).Should().Contain("depois").And.Contain(daJogadora.Url).And.Contain(doGm.Url);
    }

    [Fact]
    public async Task Character_historia_a_url_written_as_text_does_not_unlock_the_image_on_a_later_save()
    {
        var s = await CharacterSetupAsync("T4");
        var other = await RegisterGmAsync("T4outro");
        var foreign = await UploadAsync(other);
        await PutHistoriaAsync(Character(s.SheetId), s.PlayerToken, $"<p>{foreign.Url}</p><p><a href=\"https://exemplo.com{foreign.Url}\">x</a></p>");

        await PutHistoriaAsync(Character(s.SheetId), s.PlayerToken, $"<p>texto</p><img src=\"{foreign.Url}\">");

        (await GetCharacterHistoriaAsync(s.SheetId, s.PlayerToken)).Should().NotContain("<img");
    }

    [Fact]
    public async Task Character_historia_a_player_upload_made_with_the_campaign_id_is_usable_inline()
    {
        var s = await CharacterSetupAsync("T5");
        var mine = await UploadAsync(s.PlayerToken, s.CampaignId);

        await PutHistoriaAsync(Character(s.SheetId), s.PlayerToken, $"<p><img src=\"{mine.Url}\"></p>");

        (await GetCharacterHistoriaAsync(s.SheetId, s.GmToken)).Should().Contain(mine.Url);
    }

    // ---------------------------------------------------------------- NPC

    [Fact]
    public async Task Npc_gallery_the_gm_attaches_reorders_and_removes()
    {
        var gmToken = await RegisterGmAsync("N1");
        var sheetId = await CreateNpcAsync(gmToken);
        var a = await UploadAsync(gmToken);
        var b = await UploadAsync(gmToken);

        (await GetGalleryAsync(Npc(sheetId), gmToken)).Should().BeEmpty();
        (await PutGalleryAsync(Npc(sheetId), gmToken, a.Id, b.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetGalleryAsync(Npc(sheetId), gmToken)).Should().Equal(new HistoriaImagemResponse(a.Id, a.Url), new HistoriaImagemResponse(b.Id, b.Url));
        (await PutGalleryAsync(Npc(sheetId), gmToken, b.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetGalleryAsync(Npc(sheetId), gmToken)).Select(i => i.ImageId).Should().Equal(b.Id);
    }

    [Fact]
    public async Task Npc_gallery_is_404_for_another_gm_and_for_a_player_it_was_not_granted_to()
    {
        var gmToken = await RegisterGmAsync("N2");
        var otherGm = await RegisterGmAsync("N2outro");
        var (_, playerToken) = await RegisterJogadorAsync(gmToken, "N2");
        var sheetId = await CreateNpcAsync(gmToken);
        var otherImage = await UploadAsync(otherGm);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"{Npc(sheetId)}/historia/imagens", otherGm))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutGalleryAsync(Npc(sheetId), otherGm, otherImage.Id)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"{Npc(sheetId)}/historia/imagens", playerToken))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutGalleryAsync(Npc(Guid.NewGuid().ToString()), gmToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetGalleryAsync(Npc(sheetId), gmToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Npc_gallery_a_foreign_or_malformed_image_returns_400()
    {
        var gmToken = await RegisterGmAsync("N3");
        var otherGm = await RegisterGmAsync("N3outro");
        var sheetId = await CreateNpcAsync(gmToken);
        var foreign = await UploadAsync(otherGm);

        (await PutGalleryAsync(Npc(sheetId), gmToken, foreign.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PutGalleryAsync(Npc(sheetId), gmToken, "xyz")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Granted_npc_gallery_the_player_and_the_gm_both_edit_and_see_it()
    {
        var s = await GrantedNpcSetupAsync("N4");
        var daJogadora = await UploadAsync(s.PlayerToken, s.CampaignId);
        var doGm = await UploadAsync(s.GmToken);

        (await PutGalleryAsync(Npc(s.SheetId), s.PlayerToken, daJogadora.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PutGalleryAsync(Npc(s.SheetId), s.GmToken, daJogadora.Id, doGm.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var expected = new[] { new HistoriaImagemResponse(daJogadora.Id, daJogadora.Url), new HistoriaImagemResponse(doGm.Id, doGm.Url) };
        (await GetGalleryAsync(Npc(s.SheetId), s.PlayerToken)).Should().Equal(expected);
        (await GetGalleryAsync(Npc(s.SheetId), s.GmToken)).Should().Equal(expected);
    }

    [Fact]
    public async Task Granted_npc_gallery_the_player_cannot_use_a_private_image_of_the_gm()
    {
        var s = await GrantedNpcSetupAsync("N5");
        var privada = await UploadAsync(s.GmToken);

        (await PutGalleryAsync(Npc(s.SheetId), s.PlayerToken, privada.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Npc_historia_keeps_the_gms_app_image_and_strips_external_and_foreign_ones()
    {
        var gmToken = await RegisterGmAsync("N6");
        var otherGm = await RegisterGmAsync("N6outro");
        var sheetId = await CreateNpcAsync(gmToken);
        var mine = await UploadAsync(gmToken);
        var foreign = await UploadAsync(otherGm);

        var put = await PutHistoriaAsync(Npc(sheetId), gmToken,
            $"<p>Guardiã</p><img src=\"{mine.Url}\"><img src=\"{foreign.Url}\"><img src=\"http://mal.com/p.png\"><img src=x onerror=alert(1)>");

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetNpcHistoriaAsync(sheetId, gmToken)).Should().Contain($"<img src=\"{mine.Url}\">")
            .And.NotContain(foreign.Url).And.NotContain("mal.com").And.NotContain("onerror");
    }

    [Fact]
    public async Task Granted_npc_historia_inline_images_of_player_and_gm_survive_each_others_saves()
    {
        var s = await GrantedNpcSetupAsync("N7");
        var daJogadora = await UploadAsync(s.PlayerToken, s.CampaignId);
        var doGm = await UploadAsync(s.GmToken);

        (await PutHistoriaAsync(Npc(s.SheetId), s.PlayerToken, $"<p>a</p><img src=\"{daJogadora.Url}\"><img src=\"{doGm.Url}\">")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetNpcHistoriaAsync(s.SheetId, s.GmToken)).Should().Contain(daJogadora.Url).And.NotContain(doGm.Url);

        await PutHistoriaAsync(Npc(s.SheetId), s.GmToken, $"<p>b</p><img src=\"{daJogadora.Url}\"><img src=\"{doGm.Url}\">");
        await PutHistoriaAsync(Npc(s.SheetId), s.PlayerToken, $"<p>c</p><img src=\"{daJogadora.Url}\"><img src=\"{doGm.Url}\">");

        (await GetNpcHistoriaAsync(s.SheetId, s.PlayerToken)).Should().Contain("c").And.Contain(daJogadora.Url).And.Contain(doGm.Url);
    }

    [Fact]
    public async Task Grant_from_an_existing_npc_copies_the_historia_gallery_and_keeps_its_inline_images()
    {
        string a = "", b = "", inlineUrl = "";
        var s = await GrantedNpcSetupAsync("N8", async gmToken =>
        {
            var sourceId = await CreateNpcAsync(gmToken);
            var first = await UploadAsync(gmToken);
            var second = await UploadAsync(gmToken);
            (a, b, inlineUrl) = (first.Id, second.Id, first.Url);
            await PutGalleryAsync(Npc(sourceId), gmToken, second.Id, first.Id);
            await PutHistoriaAsync(Npc(sourceId), gmToken, $"<p>origem</p><img src=\"{first.Url}\">");
            return sourceId;
        });

        (await GetGalleryAsync(Npc(s.SheetId), s.PlayerToken)).Select(i => i.ImageId).Should().Equal(b, a);
        (await GetNpcHistoriaAsync(s.SheetId, s.PlayerToken)).Should().Contain(inlineUrl);
        // The player re-saving the copied História keeps the GM's inline image (already part of the sheet).
        await PutHistoriaAsync(Npc(s.SheetId), s.PlayerToken, $"<p>minha</p><img src=\"{inlineUrl}\">");
        (await GetNpcHistoriaAsync(s.SheetId, s.GmToken)).Should().Contain("minha").And.Contain(inlineUrl);
    }
}
