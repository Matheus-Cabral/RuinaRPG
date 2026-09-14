using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

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
    // synthetic ones — mirrors NpcAffectionsAndTraitsControllerTests. Restricted to
    // !RequerEspecificacao so callers that don't care about that field can add the trait with a bare
    // TraitId, no Especificacao required.
    private async Task<(string PositiveTraitId, string NegativeTraitId)> GetSeededTraitIdsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var positive = await db.Traits.FirstAsync(t => t.Polaridade == RuinaRPG.Domain.Enums.Polaridade.Positiva && !t.RequerEspecificacao && t.Custo <= 3);
        var negative = await db.Traits.FirstAsync(t => t.Polaridade == RuinaRPG.Domain.Enums.Polaridade.Negativa && !t.RequerEspecificacao && t.Custo >= -3);
        return (positive.Id.ToString(), negative.Id.ToString());
    }

    // "Alergia" is a real, stable single-tier Negativa (-1 ponto) that RequerEspecificacao — used
    // directly by name, mirroring TraitSeedParserTests' own convention of referencing it verbatim.
    private async Task<Trait> GetTraitAsync(string nome)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return await db.Traits.SingleAsync(t => t.Nome == nome);
    }

    private async Task<string> CreateExclusiveTraitDirectlyAsync(string nome, string descricao, int custo, RuinaRPG.Domain.Enums.Polaridade polaridade, bool requerEspecificacao)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var trait = new RuinaRPG.Infrastructure.Rules.CreatureExclusiveTrait
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Descricao = descricao,
            Custo = custo,
            Polaridade = polaridade,
            RequerEspecificacao = requerEspecificacao,
        };
        db.Set<RuinaRPG.Infrastructure.Rules.CreatureExclusiveTrait>().Add(trait);
        await db.SaveChangesAsync();
        return trait.Id.ToString();
    }

    // The two highest-cost traits on a side (excluding ones needing a specification, to isolate the
    // budget check from the specification check) — real Características.md data, at least 5 points
    // apart when combined, safely exceeding the level-1 budget of 5.
    private async Task<List<(string Id, int Custo)>> GetTopCostTraitsAsync(RuinaRPG.Domain.Enums.Polaridade polaridade, int take)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        // Each candidate's own |Custo| must stay within the level-1 budget (5) on its own — the test
        // using this helper adds them one at a time and expects the FIRST to succeed, so a trait that
        // alone already exceeds the budget (e.g. a -10 magnitude Negativa) would be a false failure.
        var ordered = polaridade == RuinaRPG.Domain.Enums.Polaridade.Positiva
            ? await db.Traits.Where(t => t.Polaridade == polaridade && !t.RequerEspecificacao && t.Custo <= 5).OrderByDescending(t => t.Custo).Take(take).ToListAsync()
            : await db.Traits.Where(t => t.Polaridade == polaridade && !t.RequerEspecificacao && t.Custo >= -5).OrderBy(t => t.Custo).Take(take).ToListAsync();
        return ordered.Select(t => (t.Id.ToString(), t.Custo)).ToList();
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
            new AddCreatureTraitRequest(positiveTraitId, null)));
        addPositiveResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var addNegativeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(negativeTraitId, null)));
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
            new AddCreatureTraitRequest(Guid.NewGuid().ToString(), null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTrait_rejects_a_Positiva_that_would_push_the_side_total_past_the_budget()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureAffGm5", "creatureafftraitgm5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var top = await GetTopCostTraitsAsync(RuinaRPG.Domain.Enums.Polaridade.Positiva, 2);
        top.Sum(t => t.Custo).Should().BeGreaterThan(5); // level-1 creation budget

        var first = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(top[0].Id, null)));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(top[1].Id, null)));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTrait_rejects_a_RequerEspecificacao_trait_added_without_an_Especificacao_and_persists_it_when_provided()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureAffGm6", "creatureafftraitgm6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var alergia = await GetTraitAsync("Alergia");

        var rejected = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(alergia.Id.ToString(), null)));
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(alergia.Id.ToString(), "Poeira")));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<CreatureTraitResponse>();
        added!.Especificacao.Should().Be("Poeira");
    }

    [Fact]
    public async Task AddTrait_resolves_a_creature_exclusive_characteristic_and_it_lands_in_ListTraits()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureTraitExclusiveGm1", "creaturetraitexclusive1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var exclusiveId = await CreateExclusiveTraitDirectlyAsync("Regeneração Bestial", "Recupera Vitalidade.", 3, RuinaRPG.Domain.Enums.Polaridade.Positiva, false);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(exclusiveId, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreatureTraitResponse>();
        created!.Nome.Should().Be("Regeneração Bestial");
        created.Custo.Should().Be(3);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/traits", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<CreatureTraitsListResponse>();
        list!.Positivas.Should().ContainSingle(t => t.Nome == "Regeneração Bestial");
        list.TotalPositivas.Should().Be(3);
    }

    [Fact]
    public async Task AddTrait_sums_a_normal_and_a_creature_exclusive_characteristic_into_the_same_Positivas_budget()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureTraitExclusiveGm2", "creaturetraitexclusive2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // "Alfabetizado" (Características.md: "1 ponto") — same by-name reference style this file
        // already uses for "Alergia" in GetTraitAsync. Asserted below rather than trusted blindly, so
        // this test fails loudly (not silently under-tests) if that game-data value ever changes.
        var alfabetizado = await GetTraitAsync("Alfabetizado");
        alfabetizado.Custo.Should().Be(1);
        var exclusiveId = await CreateExclusiveTraitDirectlyAsync("Regeneração Bestial", "Recupera Vitalidade.", 3, RuinaRPG.Domain.Enums.Polaridade.Positiva, false);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken, new AddCreatureTraitRequest(alfabetizado.Id.ToString(), null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken, new AddCreatureTraitRequest(exclusiveId, null)));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/traits", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<CreatureTraitsListResponse>();
        list!.Positivas.Should().HaveCount(2);
        list.TotalPositivas.Should().Be(4); // 1 (Alfabetizado) + 3 (Regeneração Bestial)
    }

    [Fact]
    public async Task AddTrait_rejects_a_creature_exclusive_pick_that_requires_Especificacao_without_one()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureTraitExclusiveGm3", "creaturetraitexclusive3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var exclusiveId = await CreateExclusiveTraitDirectlyAsync("Fúria Sazonal", "Muda de comportamento numa estação.", 1, RuinaRPG.Domain.Enums.Polaridade.Positiva, true);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(exclusiveId, null)));

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
        var addTrait = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmTokenOwner, new AddCreatureTraitRequest(positiveTraitId, null)));
        var traitId = (await addTrait.Content.ReadFromJsonAsync<CreatureTraitResponse>())!.Id;

        var addAffectionResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/affections", gmTokenOther, new AddCreatureAffectionRequest("Rival", -2)));
        addAffectionResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listAffectionsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/affections", gmTokenOther));
        listAffectionsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteAffectionResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/affections/{affectionId}", gmTokenOther));
        deleteAffectionResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var addTraitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmTokenOther, new AddCreatureTraitRequest(positiveTraitId, null)));
        addTraitResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listTraitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/traits", gmTokenOther));
        listTraitsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteTraitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/traits/{traitId}", gmTokenOther));
        deleteTraitResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
