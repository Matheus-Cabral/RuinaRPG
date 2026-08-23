using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcMasteriesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcMasteriesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    [Fact]
    public async Task Add_a_valid_mastery_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMasteryGm1", "npcmastery1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/masteries", gmToken,
            new AddNpcMasteryRequest("Maestria em Pontaria", "Pontaria", "Destreza", 3)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/masteries", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Nome == "Maestria em Pontaria" && m.Pericia == "Pontaria" && m.Atributo == "Destreza" && m.GastoMaestria == 3);
    }

    [Fact]
    public async Task List_returns_the_masteries_on_the_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMasteryGm2", "npcmastery2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/masteries", gmToken,
            new AddNpcMasteryRequest("Maestria em Furtividade", "Furtividade", "Agilidade", 2)));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/masteries", gmToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Nome == "Maestria em Furtividade" && m.Pericia == "Furtividade" && m.Atributo == "Agilidade" && m.GastoMaestria == 2);
    }

    [Fact]
    public async Task Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMasteryGm3", "npcmastery3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/masteries", gmToken,
            new AddNpcMasteryRequest("Maestria em Investigação", "Investigacao", "Astucia", 1)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcMasteryResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/masteries/{added!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/masteries", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcMasteryResponse>>();
        body!.Should().NotContain(m => m.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcMasteryGmOwner4", "npcmasteryowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcMasteryGmOther4", "npcmasteryother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/masteries", gmTokenOther,
            new AddNpcMasteryRequest("Maestria em Atletismo", "Atletismo", "Vigor", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_computes_Total_from_the_matching_skill_and_attribute_rows()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMasteryGm5", "npcmastery5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Bruto[Pontaria] = Modificador(9) = 3
        var skillUpdate = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Pontaria", gmToken,
            new UpdateNpcSkillRequest(9, null)));
        skillUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // AtributoTotal[Destreza] = 4 (Gasto 4, Bonus 0, sem maestria)
        var attributeUpdate = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Destreza", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));
        attributeUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/masteries", gmToken,
            new AddNpcMasteryRequest("Maestria em Pontaria", "Pontaria", "Destreza", 5)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<NpcMasteryResponse>();

        // Total = GastoMaestria (5) + Bruto[Pontaria] (3) + AtributoTotal[Destreza] (4) = 12
        added!.Total.Should().Be(12);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/masteries", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Id == added.Id && m.Total == 12);
    }

    [Fact]
    public async Task Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list()
    {
        // A numeric string satisfies Enum.TryParse but isn't a real Pericia/Atributo value - without
        // an Enum.IsDefined check, the row would be saved before ComputeTotalAsync runs and then every
        // later GET .../masteries would 500 forever trying to look up a skill/attribute row that
        // doesn't exist for that undefined enum value.
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMasteryGm6", "npcmastery6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/masteries", gmToken,
            new AddNpcMasteryRequest("Maestria Inválida", "999", "Destreza", 3)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/masteries", gmToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcMasteryResponse>>();
        body!.Should().BeEmpty();
    }
}
