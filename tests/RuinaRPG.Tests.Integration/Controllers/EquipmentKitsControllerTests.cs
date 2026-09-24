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
        [new EquipmentKitChoiceSlotInput("Arma", "Arma", null, "F", 1, null, null, null, null)]);

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
    public async Task Create_rejects_a_choice_slot_of_Tipo_ItemGeral()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3b", "equipkits3b@teste.com");
        await GrantRulesAuditorAsync("equipkits3b@teste.com");

        // EquipmentKitGrantService now dispatches choice slots to the matching Arma/Armadura/
        // Escudo/Artefato subtype, but ItemGeral has no such subtype to resolve options from, so
        // it's the one ItemTipo value that stays rejected here.
        var invalid = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Item Geral", "ItemGeral", null, "F", 1, null, null, null, null)] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_whose_Tipo_is_a_numeric_string()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3c", "equipkits3c@teste.com");
        await GrantRulesAuditorAsync("equipkits3c@teste.com");

        // Enum.TryParse is lenient about numeric strings ("99") — the controller must reject them
        // via an exact-name (ordinal, case-sensitive) check rather than accepting any int cast to
        // a valid-looking ItemTipo.
        var invalid = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Numérico", "99", null, "F", 1, null, null, null, null)] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_whose_ArmorSlot_is_a_numeric_string()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3d", "equipkits3d@teste.com");
        await GrantRulesAuditorAsync("equipkits3d@teste.com");

        var invalid = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, "99")] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_accepts_a_choice_slot_of_Tipo_Armadura_with_an_ArmorSlot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm1", "equipkitsarmor1@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor1@teste.com");

        var request = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, "Superior")] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<EquipmentKitResponse>();
        kit!.ChoiceSlots.Should().ContainSingle(s => s.Tipo == "Armadura" && s.ArmorSlot == "Superior");
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_of_Tipo_Armadura_without_an_ArmorSlot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm2", "equipkitsarmor2@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor2@teste.com");

        var request = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, null)] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_ArmorSlot_on_a_choice_slot_whose_Tipo_is_not_Armadura()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm3", "equipkitsarmor3@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor3@teste.com");

        var request = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Arma", "Arma", null, null, 1, null, null, null, "Superior")] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_accepts_choice_slots_of_Tipo_Escudo_and_Artefato()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm4", "equipkitsarmor4@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor4@teste.com");

        var request = ValidCreate() with
        {
            ChoiceSlots =
            [
                new EquipmentKitChoiceSlotInput("Escudo", "Escudo", null, null, 1, null, null, null, null),
                new EquipmentKitChoiceSlotInput("Artefato", "Artefato", null, null, 1, null, null, null, null),
            ]
        };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
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
