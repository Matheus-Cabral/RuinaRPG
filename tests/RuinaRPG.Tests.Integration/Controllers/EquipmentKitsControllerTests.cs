using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EquipmentKitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EquipmentKitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Mirrors the exact established helpers in CharacterSheetsControllerTests.cs — copy them
    // verbatim (RegisterJogadorLinkedToAsync, CreateCampaignAsync) rather than reinventing a
    // shorter path, since Create_for_a_campaign_member requires the OwnerId to be a real
    // CampaignMember (CharacterSheetsController.Create enforces this).
    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meBody = await (await _client.SendAsync(me)).Content.ReadFromJsonAsync<MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private async Task<string> CreateCharacterSheetAsync(string gmToken, string playerId, string campaignId)
    {
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    // Copied verbatim from HistoricosControllerTests.cs — this codebase's established way to grant
    // the Rules Auditor role in a test (a direct DB write, not an API call — no grant endpoint exists,
    // real grants happen via `make grant-rules-auditor`).
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    private static CreateEquipmentKitRequest ValidCreate() => new(
        "Kit de Teste", "Descrição de teste", 10,
        [new EquipmentKitItemInput("Mochila", "ItemGeral", 1, null)],
        [new EquipmentKitChoiceSlotInput("Arma", "Arma", null, "F", 1, null, null, null)]);

    [Fact]
    public async Task List_is_open_to_any_authenticated_caller_and_returns_the_12_seeded_kits()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm1", "equipkits1@teste.com");
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/equipment-kits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitResponse>>();
        kits!.Should().HaveCountGreaterOrEqualTo(12);
        kits.Should().Contain(k => k.Nome == "Viajante");
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_GM_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm2", "equipkits2@teste.com");
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_rejects_an_item_of_Tipo_Armadura()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3", "equipkits3@teste.com");
        await GrantRulesAuditorAsync("equipkits3@teste.com");

        var invalid = ValidCreate() with { Items = [new EquipmentKitItemInput("Armadura de Couro", "Armadura", 1, null)] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_of_Tipo_other_than_Arma()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3b", "equipkits3b@teste.com");
        await GrantRulesAuditorAsync("equipkits3b@teste.com");

        // EquipmentKitGrantService.ResolveEligibleOptionsAsync/BuildPlanAsync only ever query the
        // Arma table regardless of a choice slot's declared Tipo — accepting a non-Arma slot Tipo
        // here would silently offer weapons as options and, on confirm, insert a row pointing at an
        // Arma's Id into the wrong sheet sub-table (e.g. CharacterShield), a corrupt row Postgres
        // never rejects because the FK targets the shared TPH Item base table.
        var invalid = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Escudo", "Escudo", null, "F", 1, null, null, null)] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_by_an_Auditor_persists_items_and_choice_slots()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm4", "equipkits4@teste.com");
        await GrantRulesAuditorAsync("equipkits4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<EquipmentKitResponse>();
        kit!.Items.Should().ContainSingle(i => i.Nome == "Mochila");
        kit.ChoiceSlots.Should().ContainSingle(s => s.Label == "Arma" && s.Tier == "F");
    }

    [Fact]
    public async Task Update_replaces_the_kit_s_items_and_choice_slots_wholesale()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm5", "equipkits5@teste.com");
        await GrantRulesAuditorAsync("equipkits5@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate())))
            .Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var updated = ValidCreate() with { Items = [new EquipmentKitItemInput("Corda", "ItemGeral", 2, null)], ChoiceSlots = [] };
        var putResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/equipment-kits/{created!.Id}", gmToken, updated));
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/equipment-kits", gmToken));
        var kit = (await listResponse.Content.ReadFromJsonAsync<List<EquipmentKitResponse>>())!.Single(k => k.Id == created.Id);
        kit.Items.Should().ContainSingle(i => i.Nome == "Corda" && i.Qtd == 2);
        kit.ChoiceSlots.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_a_kit_already_chosen_by_a_CharacterSheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm6", "equipkits6@teste.com");
        await GrantRulesAuditorAsync("equipkits6@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate())))
            .Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "EquipKitsPlayer6", "equipkitsplayer6@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha de Teste");
        var sheetId = await CreateCharacterSheetAsync(gmToken, playerId, campaignId);

        // Directly through the DB, to isolate this test from the full choose-flow this same plan builds in Task 9.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var s = await db.CharacterSheets.FindAsync(Guid.Parse(sheetId));
            s!.EquipmentKitId = Guid.Parse(created!.Id);
            await db.SaveChangesAsync();
        }

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/equipment-kits/{created.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
