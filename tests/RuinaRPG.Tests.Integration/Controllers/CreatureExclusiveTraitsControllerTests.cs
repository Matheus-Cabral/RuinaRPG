using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureExclusiveTraitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureExclusiveTraitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Copied verbatim from TraitsControllerTests.cs — same mechanism, a new local copy per this
    // repo's own established convention (every controller test file owns its helpers, none share
    // a base class for this).
    private async Task<string> GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return email;
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

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    [Fact]
    public async Task List_is_open_to_any_authenticated_GM_even_without_the_Auditor_role()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm1", "creatureexclusive1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-exclusive-traits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm2", "creatureexclusive2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Regeneração Bestial", "Recupera Vitalidade.", 3, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_by_the_designated_Rules_Auditor_succeeds_and_the_row_is_then_listed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm3", "creatureexclusive3@teste.com");
        await GrantRulesAuditorAsync("creatureexclusive3@teste.com");

        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Regeneração Bestial", "Recupera Vitalidade.", 3, "Positiva", false)));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-exclusive-traits", gmToken));
        var traits = await listResponse.Content.ReadFromJsonAsync<List<CreatureExclusiveTraitResponse>>();
        traits!.Should().ContainSingle(t => t.Nome == "Regeneração Bestial" && t.Custo == 3);
    }

    [Fact]
    public async Task Create_with_a_Custo_sign_inconsistent_with_Polaridade_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm4", "creatureexclusive4@teste.com");
        await GrantRulesAuditorAsync("creatureexclusive4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Inconsistente", "Descrição.", -1, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_by_the_Auditor_changes_the_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm5", "creatureexclusive5@teste.com");
        await GrantRulesAuditorAsync("creatureexclusive5@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Original", "Descrição.", 2, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<CreatureExclusiveTraitResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-exclusive-traits/{created!.Id}", gmToken,
            new UpdateCreatureExclusiveTraitRequest("Renomeado", "Nova descrição.", 4, "Positiva", true)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-exclusive-traits", gmToken));
        var traits = await listResponse.Content.ReadFromJsonAsync<List<CreatureExclusiveTraitResponse>>();
        traits!.Should().ContainSingle(t => t.Id == created.Id && t.Nome == "Renomeado" && t.Custo == 4 && t.RequerEspecificacao);
    }

    [Fact]
    public async Task Delete_by_the_Auditor_removes_the_row_from_the_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm6", "creatureexclusive6@teste.com");
        await GrantRulesAuditorAsync("creatureexclusive6@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Para Excluir", "Descrição.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<CreatureExclusiveTraitResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-exclusive-traits/{created!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-exclusive-traits", gmToken));
        var traits = await listResponse.Content.ReadFromJsonAsync<List<CreatureExclusiveTraitResponse>>();
        traits!.Should().NotContain(t => t.Id == created.Id);
    }

    // Mirrors TraitsControllerTests.DeleteTrait_that_is_already_in_use_on_a_creature_sheet_returns_409,
    // but here the in-use check is against the *other* catalog's own Delete — only a CreatureTrait
    // row can reference a CreatureExclusiveTrait, so this exercises
    // CreatureExclusiveTraitsController.Delete's own inUse guard directly, deferred from Task 2.
    [Fact]
    public async Task Delete_that_is_already_in_use_on_a_creature_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm7", "creatureexclusive7@teste.com");
        await GrantRulesAuditorAsync("creatureexclusive7@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Em Uso", "Teste.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<CreatureExclusiveTraitResponse>();

        var sheetId = await CreateCreatureSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
            new AddCreatureTraitRequest(created!.Id, null)));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-exclusive-traits/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Mirrors TraitsControllerTests.CreateTrait_duplicating_an_existing_Nome_Custo_Polaridade_returns_400,
    // deferred from Task 2 since it needed nothing from Task 3 — added now alongside the rest of
    // this pass regardless.
    [Fact]
    public async Task Create_duplicating_an_existing_Nome_Custo_Polaridade_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm8", "creatureexclusive8@teste.com");
        await GrantRulesAuditorAsync("creatureexclusive8@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Duplicada", "Original.", 3, "Positiva", false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Duplicada", "Outra descrição.", 3, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Mirrors Update's own uniqueness check the same way TraitsControllerTests covers it via
    // UpdateTrait_colliding_onto_another_trait_s_key_returns_400_and_does_not_break_the_seeder,
    // minus the seeder assertion (this catalog has no seeder — see CreatureExclusiveTrait's own
    // doc comment).
    [Fact]
    public async Task Update_colliding_onto_another_row_s_Nome_Custo_Polaridade_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureExclusiveGm9", "creatureexclusive9@teste.com");
        await GrantRulesAuditorAsync("creatureexclusive9@teste.com");
        var createAResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Trait A", "Original A.", 2, "Positiva", false)));
        var createdA = await createAResponse.Content.ReadFromJsonAsync<CreatureExclusiveTraitResponse>();
        var createBResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-exclusive-traits", gmToken,
            new CreateCreatureExclusiveTraitRequest("Trait B", "Original B.", 3, "Positiva", false)));
        var createdB = await createBResponse.Content.ReadFromJsonAsync<CreatureExclusiveTraitResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-exclusive-traits/{createdB!.Id}", gmToken,
            new UpdateCreatureExclusiveTraitRequest(createdA!.Nome, "Renomeada para colidir.", createdA.Custo, createdA.Polaridade, false)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
