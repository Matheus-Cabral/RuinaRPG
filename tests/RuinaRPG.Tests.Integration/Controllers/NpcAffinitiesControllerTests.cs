using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcAffinitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcAffinitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
    public async Task Add_a_valid_combination_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm1", "npcaff1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Elemento == "Fogo" && a.ElementoValor == 3 && a.SubElemento == "Vida" && a.SubElementoValor == 2
            && a.CaminhoNome == "Caminho da Fênix" && a.Experiencia == 10);
    }

    [Fact]
    public async Task Add_an_invalid_Elemento_SubElemento_combination_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm2", "npcaff2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Ar", 0, "Ferro", 0, "Caminho Inválido", 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm3", "npcaff3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Terra", 1, "Vida", 1, "Caminho da Terra", 5)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();
        body!.Should().NotContain(a => a.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAffGmOwner4", "npcaffowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAffGmOther4", "npcaffother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmTokenOther,
            new AddNpcAffinityRequest("Terra", 1, "Vida", 1, "Caminho da Terra", 5)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAffGmOwner5", "npcaffowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAffGmOther5", "npcaffother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_with_every_field_null_returns_201_and_a_blank_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm6", "npcaff6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<NpcAffinityResponse>();
        added!.Elemento.Should().BeNull();
        added.SubElemento.Should().BeNull();
    }

    [Fact]
    public async Task Update_an_existing_affinity_returns_200_and_the_list_reflects_the_change()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm7", "npcaff7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Terra", 5, "Aprimorar", 1, "Caminho da Terra", 8)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();
        updated!.Elemento.Should().Be("Terra");
        updated.SubElemento.Should().Be("Aprimorar");

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Id == added.Id && a.Elemento == "Terra" && a.SubElemento == "Aprimorar");
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAffGmOwner8", "npcaffowner8@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAffGmOther8", "npcaffother8@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmTokenOwner,
            new AddNpcAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmTokenOther,
            new UpdateNpcAffinityRequest("Terra", 1, "Vida", 1, "Outro", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
