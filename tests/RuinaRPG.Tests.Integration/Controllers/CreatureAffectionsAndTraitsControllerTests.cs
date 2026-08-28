using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureAffectionsAndTraitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureAffectionsAndTraitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Traits are seeded at app startup by TraitSeeder from Características.md. Pull one positive
    // and one negative real Trait straight out of the running app's database rather than inserting
    // synthetic ones — mirrors NpcAffectionsAndTraitsControllerTests.
    private async Task<(string PositiveTraitId, string NegativeTraitId)> GetSeededTraitIdsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var positive = await db.Traits.FirstAsync(t => t.Polaridade == RuinaRPG.Domain.Enums.Polaridade.Positiva);
        var negative = await db.Traits.FirstAsync(t => t.Polaridade == RuinaRPG.Domain.Enums.Polaridade.Negativa);
        return (positive.Id.ToString(), negative.Id.ToString());
    }

    [Fact]
    public async Task AddAffection_then_ListAffections_returns_it_then_DeleteAffection_removes_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureAffGm1", "creatureafftraitgm1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/affections", gmToken,
            new AddCreatureAffectionRequest("Amigo de infância", 5)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<CreatureAffectionResponse>();
        added!.Nome.Should().Be("Amigo de infância");
        added.Favorabilidade.Should().Be(5);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/affections", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<CreatureAffectionResponse>>();
        list!.Should().ContainSingle(a => a.Id == added.Id);

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/affections/{added.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterDelete = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/affections", gmToken));
        var listAfter = await listAfterDelete.Content.ReadFromJsonAsync<List<CreatureAffectionResponse>>();
        listAfter!.Should().NotContain(a => a.Id == added.Id);
    }

    [Fact]
    public async Task AddTrait_lands_in_the_correct_bucket_with_correct_totals_and_DeleteTrait_removes_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureAffGm2", "creatureafftraitgm2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var (positiveTraitId, negativeTraitId) = await GetSeededTraitIdsAsync();

        var addPositiveResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(positiveTraitId)));
        addPositiveResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var addNegativeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(negativeTraitId)));
        addNegativeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var addedNegative = await addNegativeResponse.Content.ReadFromJsonAsync<CreatureTraitResponse>();

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/traits", gmToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<CreatureTraitsListResponse>();
        list!.Positivas.Should().ContainSingle(t => t.TraitId == positiveTraitId);
        list.Negativas.Should().ContainSingle(t => t.TraitId == negativeTraitId);
        list.TotalPositivas.Should().Be(list.Positivas.Sum(t => t.Custo));
        list.TotalNegativas.Should().Be(list.Negativas.Sum(t => t.Custo));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/traits/{addedNegative!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterDelete = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/traits", gmToken));
        var listAfter = await listAfterDelete.Content.ReadFromJsonAsync<CreatureTraitsListResponse>();
        listAfter!.Negativas.Should().NotContain(t => t.Id == addedNegative.Id);
    }

    [Fact]
    public async Task AddTrait_with_a_nonexistent_TraitId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureAffGm3", "creatureafftraitgm3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(Guid.NewGuid().ToString())));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Affection_and_trait_actions_by_a_different_gm_return_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureAffGmOwner4", "creatureafftraitgmowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureAffGmOther4", "creatureafftraitgmother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);
        var (positiveTraitId, _) = await GetSeededTraitIdsAsync();

        var addAffection = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/affections", gmTokenOwner, new AddCreatureAffectionRequest("Rival", -2)));
        var affectionId = (await addAffection.Content.ReadFromJsonAsync<CreatureAffectionResponse>())!.Id;
        var addTrait = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmTokenOwner, new AddCreatureTraitRequest(positiveTraitId)));
        var traitId = (await addTrait.Content.ReadFromJsonAsync<CreatureTraitResponse>())!.Id;

        var addAffectionResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/affections", gmTokenOther, new AddCreatureAffectionRequest("Rival", -2)));
        addAffectionResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listAffectionsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/affections", gmTokenOther));
        listAffectionsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteAffectionResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/affections/{affectionId}", gmTokenOther));
        deleteAffectionResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var addTraitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmTokenOther, new AddCreatureTraitRequest(positiveTraitId)));
        addTraitResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listTraitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/traits", gmTokenOther));
        listTraitsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteTraitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/traits/{traitId}", gmTokenOther));
        deleteTraitResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
