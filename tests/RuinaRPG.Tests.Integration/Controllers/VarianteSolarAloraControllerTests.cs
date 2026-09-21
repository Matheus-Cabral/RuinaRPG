using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

// Requisitos - Habilidades Raciais R0005: a variante solar de Alóra só existe nas fichas depois que o
// GM dá um nome a ela na página de Habilidades Raciais.
public class VarianteSolarAloraControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public VarianteSolarAloraControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> SetUpCharacterSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private static UpdateCharacterSheetRequest CharacterUpdate(string linhagem, string variante) => new(
        null, "Teste", linhagem, variante, "Campeao", "Duelista", null, null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, 0, null);

    private static UpdateNpcSheetRequest NpcUpdate(string linhagem, string variante) => new(
        null, "Teste", linhagem, variante, "Campeao", "Duelista", null, null,
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, null);

    private Task<HttpResponseMessage> SetNomeDaVarianteAsync(string gmToken, string? nome) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/AloraSolar/nome-da-variante", gmToken, new UpdateNomeDaVarianteRequest(nome)));

    private async Task<List<VarianteLiberadaResponse>> LiberadasAsync(string route, string sheetId, string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{route}/{sheetId}/variantes-liberadas", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<VarianteLiberadaResponse>>())!;
    }

    [Fact]
    public async Task List_includes_AloraSolar_with_empty_defaults_and_no_name()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm1", "solar1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        body!.Should().HaveCount(8);
        var solar = body!.Single(e => e.Variante == "AloraSolar");
        solar.Nome.Should().BeEmpty();
        solar.Descricao.Should().BeEmpty();
        solar.NomeDaVariante.Should().BeNull();
    }

    [Fact]
    public async Task Setting_the_name_persists_it_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm2", "solar2@teste.com");

        (await SetNomeDaVarianteAsync(gmToken, "Alóra Solar")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        body!.Single(e => e.Variante == "AloraSolar").NomeDaVariante.Should().Be("Alóra Solar");
    }

    [Fact]
    public async Task Setting_the_name_keeps_the_racial_Nome_and_Descricao_already_saved_and_vice_versa()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm3", "solar3@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/AloraSolar", gmToken, new UpdateRacialAbilityRequest("Racial (Brilho)", "Texto.")));

        await SetNomeDaVarianteAsync(gmToken, "Alóra Solar");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/AloraSolar", gmToken, new UpdateRacialAbilityRequest("Racial (Brilho 2)", "Texto 2.")));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        var solar = (await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>())!.Single(e => e.Variante == "AloraSolar");
        solar.Nome.Should().Be("Racial (Brilho 2)");
        solar.NomeDaVariante.Should().Be("Alóra Solar");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_name_clears_it(string? blank)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"SolarGm4{blank?.Length}", $"solar4{blank?.Length}@teste.com");
        await SetNomeDaVarianteAsync(gmToken, "Alóra Solar");

        (await SetNomeDaVarianteAsync(gmToken, blank)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        (await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>())!.Single(e => e.Variante == "AloraSolar").NomeDaVariante.Should().BeNull();
    }

    [Fact]
    public async Task Setting_a_name_for_any_other_Variante_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm5", "solar5@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Yavos/nome-da-variante", gmToken, new UpdateNomeDaVarianteRequest("Outro")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Setting_the_name_as_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm6", "solar6@teste.com");
        var (_, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SolarPlayer6", "solarplayer6@teste.com");

        var response = await SetNomeDaVarianteAsync(playerToken, "Alóra Solar");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Restoring_the_default_removes_the_name_too()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm7", "solar7@teste.com");
        await SetNomeDaVarianteAsync(gmToken, "Alóra Solar");

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/racial-abilities/AloraSolar", gmToken));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        (await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>())!.Single(e => e.Variante == "AloraSolar").NomeDaVariante.Should().BeNull();
    }

    [Fact]
    public async Task Character_sheet_rejects_the_solar_variant_until_the_GM_names_it_then_accepts_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm8", "solar8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SolarPlayer8", "solarplayer8@teste.com");
        var sheetId = await SetUpCharacterSheetAsync(gmToken, playerId);

        (await LiberadasAsync("character-sheets", sheetId, playerToken)).Should().BeEmpty();
        var before = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, CharacterUpdate("Econos", "AloraSolar")));
        before.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await SetNomeDaVarianteAsync(gmToken, "Alóra Solar");

        (await LiberadasAsync("character-sheets", sheetId, playerToken)).Should().ContainSingle(v => v.Variante == "AloraSolar" && v.Rotulo == "Alóra Solar");
        var after = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, CharacterUpdate("Econos", "AloraSolar")));
        after.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Character_sheet_already_using_the_variant_keeps_saving_after_the_GM_clears_the_name()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm9", "solar9@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SolarPlayer9", "solarplayer9@teste.com");
        var sheetId = await SetUpCharacterSheetAsync(gmToken, playerId);
        await SetNomeDaVarianteAsync(gmToken, "Alóra Solar");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, CharacterUpdate("Econos", "AloraSolar")));

        await SetNomeDaVarianteAsync(gmToken, null);

        // Só uma escolha NOVA é bloqueada — reenviar a mesma variante continua valendo.
        var resubmit = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, CharacterUpdate("Econos", "AloraSolar")));
        resubmit.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Character_sheet_with_the_solar_variant_has_no_pending_racial_trait_choice_while_the_GM_has_configured_no_options()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm10", "solar10@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SolarPlayer10", "solarplayer10@teste.com");
        var sheetId = await SetUpCharacterSheetAsync(gmToken, playerId);
        await SetNomeDaVarianteAsync(gmToken, "Alóra Solar");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, CharacterUpdate("Econos", "AloraSolar")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));

        (await response.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>())!.Pending.Should().BeFalse();
    }

    [Fact]
    public async Task Npc_sheet_rejects_the_solar_variant_until_the_GM_names_it_then_accepts_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm11", "solar11@teste.com");
        var sheetId = await CreateNpcSheetAsync(gmToken);

        (await LiberadasAsync("npc-sheets", sheetId, gmToken)).Should().BeEmpty();
        var before = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, NpcUpdate("Econos", "AloraSolar")));
        before.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await SetNomeDaVarianteAsync(gmToken, "Alóra Solar");

        (await LiberadasAsync("npc-sheets", sheetId, gmToken)).Should().ContainSingle(v => v.Variante == "AloraSolar" && v.Rotulo == "Alóra Solar");
        var after = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, NpcUpdate("Econos", "AloraSolar")));
        after.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Npc_sheet_already_using_the_variant_keeps_saving_after_the_GM_clears_the_name()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SolarGm12", "solar12@teste.com");
        var sheetId = await CreateNpcSheetAsync(gmToken);
        await SetNomeDaVarianteAsync(gmToken, "Alóra Solar");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, NpcUpdate("Econos", "AloraSolar")));

        await SetNomeDaVarianteAsync(gmToken, null);

        var resubmit = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, NpcUpdate("Econos", "AloraSolar")));
        resubmit.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Variantes_liberadas_of_another_gms_sheet_returns_404_or_403()
    {
        var gmOwner = await RegisterGmAndGetTokenAsync("SolarGm13", "solar13@teste.com");
        var gmOther = await RegisterGmAndGetTokenAsync("SolarGm13b", "solar13b@teste.com");
        var sheetId = await CreateNpcSheetAsync(gmOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/variantes-liberadas", gmOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
