using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class TraitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public TraitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
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

    private async Task<string> GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return email;
    }

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    // Trivial, valid-but-empty markdown: TraitSeedParser.Parse("") produces zero TraitSeeds, so
    // SeedAsync becomes a pure "does the existingByKey lookup throw" probe against whatever the
    // table already holds — exactly what the two regression tests below need, with no dependency
    // on TraitSeederTests' own Markdown constant (private to that class).
    private const string TrivialMarkdown = "";

    // Mirrors DeleteTrait_that_is_already_in_use_on_a_sheet_returns_409, but the reference lives on
    // a Creature sheet instead of a Character sheet — the regression this guards against (Critical
    // #2) is specifically that CreatureTraits was omitted from the Delete in-use check.
    [Fact]
    public async Task DeleteTrait_that_is_already_in_use_on_a_creature_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm8", "traitcrudgm8@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm8@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Em Uso Criatura", "Teste.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<TraitResponse>();

        var sheetId = await CreateCreatureSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(created!.Id, null)));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/traits/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Regression test for Critical #1, path 1: soft-delete then recreate under the exact same
    // (Nome, Custo, Polaridade) key used to slip past Create's duplicate check (which only excluded
    // !IsDeleted rows), leaving two rows sharing that key — which then crashed TraitSeeder.SeedAsync
    // on the very next seed (ToDictionary throws on a duplicate key).
    [Fact]
    public async Task CreateTrait_duplicating_a_soft_deleted_trait_s_key_returns_400_and_does_not_break_the_seeder()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm9", "traitcrudgm9@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm9@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Recriada", "Original.", 4, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<TraitResponse>();
        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/traits/{created!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var recreateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Recriada", "Outra descrição.", 4, "Positiva", false)));
        recreateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var act = async () => await TraitSeeder.SeedAsync(db, TrivialMarkdown);
        await act.Should().NotThrowAsync();
    }

    // Regression test for Critical #1, path 2: Update had NO uniqueness check at all, so renaming
    // trait B onto trait A's exact (Nome, Custo, Polaridade) collided two live rows onto the same
    // key — same TraitSeeder.SeedAsync crash on the next seed.
    [Fact]
    public async Task UpdateTrait_colliding_onto_another_trait_s_key_returns_400_and_does_not_break_the_seeder()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm10", "traitcrudgm10@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm10@teste.com");
        var createAResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Trait A", "Original A.", 2, "Positiva", false)));
        var createdA = await createAResponse.Content.ReadFromJsonAsync<TraitResponse>();
        var createBResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Trait B", "Original B.", 3, "Positiva", false)));
        var createdB = await createBResponse.Content.ReadFromJsonAsync<TraitResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/traits/{createdB!.Id}", gmToken,
            new UpdateTraitRequest(createdA!.Nome, "Renomeada para colidir.", createdA.Custo, createdA.Polaridade, false)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var act = async () => await TraitSeeder.SeedAsync(db, TrivialMarkdown);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/traits");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_with_no_filter_returns_a_real_Id_for_a_known_trait()
    {
        // TraitId is what AddCharacterTraitRequest (and the Npc/Creature equivalents) actually
        // needs — this is the one endpoint that lets a caller resolve it, unlike
        // CompendioController's search, which only ever returns Nome/Descricao text.
        var token = await RegisterGmAndGetTokenAsync("TraitGm1", "trait1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<TraitResponse>>();
        var alfabetizado = body!.Should().ContainSingle(t => t.Nome == "Alfabetizado").Subject;
        Guid.TryParse(alfabetizado.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task List_can_filter_by_partial_Nome_case_insensitively()
    {
        var token = await RegisterGmAndGetTokenAsync("TraitGm2", "trait2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits?nome=alfabet", token));

        var body = await response.Content.ReadFromJsonAsync<List<TraitResponse>>();
        body!.Should().ContainSingle(t => t.Nome == "Alfabetizado");
    }

    [Fact]
    public async Task CreateTrait_by_a_Rules_Auditor_persists_it_as_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm1", "traitcrudgm1@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Sortudo", "Ganha sorte extra.", 2, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<TraitResponse>();
        body!.Nome.Should().Be("Sortudo");
        body.IsCustomized.Should().BeTrue();

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<TraitResponse>>();
        list!.Should().Contain(t => t.Nome == "Sortudo");
    }

    [Fact]
    public async Task CreateTrait_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm2", "traitcrudgm2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Sortudo", "Ganha sorte extra.", 2, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTrait_with_a_Custo_sign_mismatched_to_Polaridade_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm3", "traitcrudgm3@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Custo Errado", "Teste.", -2, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTrait_duplicating_an_existing_Nome_Custo_Polaridade_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm4", "traitcrudgm4@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm4@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Duplicada", "Original.", 3, "Positiva", false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Duplicada", "Outra descrição.", 3, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTrait_by_a_Rules_Auditor_persists_the_change_and_marks_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm5", "traitcrudgm5@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm5@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Para Editar", "Antes.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<TraitResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/traits/{created!.Id}", gmToken,
            new UpdateTraitRequest("Para Editar", "Depois.", 5, "Positiva", false)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<TraitResponse>>();
        var edited = list!.Single(t => t.Id == created.Id);
        edited.Descricao.Should().Be("Depois.");
        edited.Custo.Should().Be(5);
    }

    [Fact]
    public async Task DeleteTrait_soft_deletes_and_it_no_longer_appears_in_List()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm6", "traitcrudgm6@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm6@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Para Excluir", "Teste.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<TraitResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/traits/{created!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<TraitResponse>>();
        list!.Should().NotContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task DeleteTrait_that_is_already_in_use_on_a_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm7", "traitcrudgm7@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm7@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Em Uso", "Teste.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<TraitResponse>();

        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "TraitCrudPlayer7", "traitcrudplayer7@teste.com");
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha Trait", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken, new AddCharacterTraitRequest(created!.Id, null)));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/traits/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
