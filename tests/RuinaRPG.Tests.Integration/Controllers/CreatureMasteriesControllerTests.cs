using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureMasteriesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureMasteriesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    [Fact]
    public async Task Add_a_valid_mastery_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMasteryGm1", "creaturemastery1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/masteries", gmToken,
            new AddCreatureMasteryRequest("Maestria em Pontaria", "Pontaria", "Destreza", 3)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/masteries", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Nome == "Maestria em Pontaria" && m.Pericia == "Pontaria" && m.Atributo == "Destreza" && m.GastoMaestria == 3);
    }

    [Fact]
    public async Task List_returns_the_masteries_on_the_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMasteryGm2", "creaturemastery2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/masteries", gmToken,
            new AddCreatureMasteryRequest("Maestria em Furtividade", "Furtividade", "Agilidade", 2)));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/masteries", gmToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Nome == "Maestria em Furtividade" && m.Pericia == "Furtividade" && m.Atributo == "Agilidade" && m.GastoMaestria == 2);
    }

    [Fact]
    public async Task Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMasteryGm3", "creaturemastery3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/masteries", gmToken,
            new AddCreatureMasteryRequest("Maestria em Investigação", "Investigacao", "Astucia", 1)));
        var added = await addResponse.Content.ReadFromJsonAsync<CreatureMasteryResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/masteries/{added!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/masteries", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureMasteryResponse>>();
        body!.Should().NotContain(m => m.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureMasteryGmOwner4", "creaturemasteryowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureMasteryGmOther4", "creaturemasteryother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/masteries", gmTokenOther,
            new AddCreatureMasteryRequest("Maestria em Atletismo", "Atletismo", "Vigor", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_computes_Total_from_the_matching_skill_and_attribute_rows()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMasteryGm5", "creaturemastery5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Bruto[Pontaria] = Modificador(9) = 3
        var skillUpdate = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Pontaria", gmToken,
            new UpdateCreatureSkillRequest(9, null)));
        skillUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // AtributoTotal[Destreza] = 4 (Gasto 4, Bonus 0, sem maestria)
        var attributeUpdate = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Destreza", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));
        attributeUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/masteries", gmToken,
            new AddCreatureMasteryRequest("Maestria em Pontaria", "Pontaria", "Destreza", 5)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<CreatureMasteryResponse>();

        // Total = GastoMaestria (5) + Bruto[Pontaria] (3) + AtributoTotal[Destreza] (4) = 12
        added!.Total.Should().Be(12);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/masteries", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Id == added.Id && m.Total == 12);
    }

    [Fact]
    public async Task Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMasteryGm6", "creaturemastery6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/masteries", gmToken,
            new AddCreatureMasteryRequest("Maestria Inválida", "999", "Destreza", 3)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/masteries", gmToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureMasteryResponse>>();
        body!.Should().BeEmpty();
    }

    // Additional Criatura-only guard: Alquimia is a real Pericia enum value but not in the R0005
    // 20-skill allow-list, so no CreatureSkill row exists for it — without this check, Add would
    // 500 trying to SingleAsync a skill row that was never seeded.
    [Fact]
    public async Task Add_with_a_disallowed_Pericia_returns_400_and_does_not_poison_the_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMasteryGm7", "creaturemastery7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/masteries", gmToken,
            new AddCreatureMasteryRequest("Maestria em Alquimia", "Alquimia", "Destreza", 3)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/masteries", gmToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureMasteryResponse>>();
        body!.Should().BeEmpty();
    }
}
