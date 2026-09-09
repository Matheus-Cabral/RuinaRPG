using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RacialAbilitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RacialAbilitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    [Fact]
    public async Task ListRacialAbilities_returns_all_7_Variantes_with_defaults_when_no_overrides_exist()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm1", "racial1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        body!.Should().HaveCount(7);
        body!.Should().OnlyContain(e => e.IsDefault);
        body!.Single(e => e.Variante == "Sinir").Nome.Should().Be("Racial (Arca)");
        body!.Single(e => e.Variante == "Sinir").Descricao.Should().Be("Role 1d18 na tabela de Arcas.");
        body!.Single(e => e.Variante == "Alora").Nome.Should().Be("Racial (Amplificador Místico)");
    }

    [Fact]
    public async Task UpdateRacialAbility_persists_the_override_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm2", "racial2@teste.com");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Alora", gmToken,
            new UpdateRacialAbilityRequest("Racial (Custom)", "Um texto novo definido pelo GM.")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        var alora = body!.Single(e => e.Variante == "Alora");
        alora.Nome.Should().Be("Racial (Custom)");
        alora.Descricao.Should().Be("Um texto novo definido pelo GM.");
        alora.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRacialAbility_called_twice_updates_the_same_row_instead_of_duplicating()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm3", "racial3@teste.com");

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Yavos", gmToken, new UpdateRacialAbilityRequest("V1", "D1")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Yavos", gmToken, new UpdateRacialAbilityRequest("V2", "D2")));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        body!.Should().ContainSingle(e => e.Variante == "Yavos");
        body!.Single(e => e.Variante == "Yavos").Nome.Should().Be("V2");
    }

    [Fact]
    public async Task DeleteRacialAbilityOverride_reverts_to_the_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm4", "racial4@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Koroanos", gmToken, new UpdateRacialAbilityRequest("Custom", "Custom desc")));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/racial-abilities/Koroanos", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        var koroanos = body!.Single(e => e.Variante == "Koroanos");
        koroanos.Nome.Should().Be("Racial (Sobre Voo)");
        koroanos.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteRacialAbilityOverride_on_a_Variante_with_no_override_is_a_no_op_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm5", "racial5@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/racial-abilities/PhylacTai", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateRacialAbility_with_an_unknown_Variante_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm6", "racial6@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/NaoExiste", gmToken, new UpdateRacialAbilityRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRacialAbility_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm7", "racial7@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialPlayer7", "racialplayer7@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Alora", playerToken, new UpdateRacialAbilityRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListRacialAbilities_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm8", "racial8@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialPlayer8", "racialplayer8@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListArcas_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm12", "racial12@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialPlayer12", "racialplayer12@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListArcas_returns_18_entries_with_null_fields_when_unset()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm9", "racial9@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        body!.Should().HaveCount(18);
        body!.Select(a => a.Roll).Should().BeEquivalentTo(Enumerable.Range(1, 18));
        body!.Should().OnlyContain(a => a.Nome == null && a.Descricao == null);
    }

    [Fact]
    public async Task UpdateArca_persists_the_entry_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm10", "racial10@teste.com");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/7", gmToken,
            new UpdateArcaEntryRequest("A Chama Eterna", "Concede resistência ao fogo por 1 cena.")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        var entry = body!.Single(a => a.Roll == 7);
        entry.Nome.Should().Be("A Chama Eterna");
        entry.Descricao.Should().Be("Concede resistência ao fogo por 1 cena.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    public async Task UpdateArca_with_a_roll_outside_1_to_18_returns_400(int invalidRoll)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"RacialGmRoll{invalidRoll}", $"racialroll{invalidRoll}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/{invalidRoll}", gmToken, new UpdateArcaEntryRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateArca_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm11", "racial11@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialPlayer11", "racialplayer11@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/1", playerToken, new UpdateArcaEntryRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
