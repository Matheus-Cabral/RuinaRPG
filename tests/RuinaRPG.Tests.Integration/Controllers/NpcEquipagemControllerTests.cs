using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcEquipagemControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcEquipagemControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Copied verbatim from EquipmentKitsControllerTests.cs — the established direct-DB way to grant
    // the Rules Auditor role in a test (no grant endpoint exists).
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    // Copied verbatim from CharacterPossessionsControllerTests.cs's helper.
    private async Task<string> CreateArtefatoItemAsync(string gmToken, string nome, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    // This repo's existing Npc-sheet-creation helper (see NpcRacialTraitsControllerTests.cs) — a
    // fresh, GM-owned, not-attached-to-any-campaign NPC sheet.
    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    // Creates a campaign, adds a player member, and grants that player a brand-new NPC sheet
    // (CampaignGrantsController, Tipo="Npc", no SourceSheetId) — the only way an NpcSheet ends up
    // attached to a campaign, since NpcSheet itself carries no CampaignId column.
    private async Task<(string CampaignId, string SheetId)> CreateCampaignAndGrantedNpcSheetAsync(string gmToken)
    {
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, $"EquipagemNpcPlayer{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@teste.com");
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha Npc", "")));
        var campaign = await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>();
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaign!.Id}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var grantResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/grants", gmToken,
            new GrantSheetRequest(playerId, "Npc", null)));
        var grant = await grantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>();
        return (campaign.Id, grant!.SheetId);
    }

    [Fact]
    public async Task ListKits_returns_every_seeded_kit_with_choice_slot_options_resolved_against_the_GM_s_catalog()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm1", "equipagemnpc1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        kits!.Should().HaveCountGreaterOrEqualTo(12);
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");
        patrulheiro.ChoiceSlots.Should().ContainSingle();
        patrulheiro.ChoiceSlots.Single().Options.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Choose_a_kit_with_no_choice_slots_places_every_fixed_item_and_adds_Ciclos()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm2", "equipagemnpc2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var viajante = kits!.Single(k => k.Nome == "Viajante");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(viajante.Id, [])));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        sheet!.EquipmentKitId.Should().Be(viajante.Id);
        sheet.Ciclos.Should().Be(25);

        var inventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/inventory", gmToken));
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<List<NpcInventoryItemResponse>>();
        inventory!.Should().Contain(i => i.Nome == "Mochila");
        inventory.Should().Contain(i => i.Nome == "Ração de Viagem" && i.Qtd == 2);
    }

    [Fact]
    public async Task Choose_a_kit_with_a_choice_slot_places_the_selected_weapon()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm3", "equipagemnpc3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");
        var chosenWeapon = patrulheiro.ChoiceSlots.Single().Options.First();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(patrulheiro.Id, [new ChoiceSlotSelectionRequest(patrulheiro.ChoiceSlots.Single().SlotId, chosenWeapon.ItemId)])));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var weaponsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/weapons", gmToken));
        var weapons = await weaponsResponse.Content.ReadFromJsonAsync<List<NpcWeaponResponse>>();
        weapons!.Should().ContainSingle(w => w.Nome == chosenWeapon.Nome);

        var shieldsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/shields", gmToken));
        var shields = await shieldsResponse.Content.ReadFromJsonAsync<List<NpcShieldResponse>>();
        shields!.Should().ContainSingle(s => s.Nome == "Tampa de Madeira");
    }

    [Fact]
    public async Task Choose_Cacador_with_the_Arco_alternative_also_grants_10_Flechas_de_Madeira()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm4", "equipagemnpc4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var meResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = (await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        Guid testBowId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var bow = new RuinaRPG.Infrastructure.Items.Arma
            {
                Id = Guid.NewGuid(), GmId = Guid.Parse(gmId), Nome = "Arco de Teste F Npc", Subcategoria = "Arcos",
                Tier = RuinaRPG.Domain.Items.Tier.F, Peso = 1, Preco = 0,
            };
            db.Add(bow);
            await db.SaveChangesAsync();
            testBowId = bow.Id;
        }

        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var cacador = kits!.Single(k => k.Nome == "Caçador");
        var slot = cacador.ChoiceSlots.Single();
        var chosen = slot.Options.Single(o => o.ItemId == testBowId.ToString());

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(cacador.Id, [new ChoiceSlotSelectionRequest(slot.SlotId, chosen.ItemId)])));

        var inventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/inventory", gmToken));
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<List<NpcInventoryItemResponse>>();
        inventory!.Should().Contain(i => i.Nome == "Flecha de Madeira" && i.Qtd == 10);
    }

    [Fact]
    public async Task Choose_a_second_time_on_an_already_chosen_sheet_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm5", "equipagemnpc5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var negociante = kits!.Single(k => k.Nome == "Negociante");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        var second = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Choose_with_ChoiceSelections_omitted_from_the_request_body_returns_400_not_500()
    {
        // Mirrors CharacterEquipagemControllerTests's identical test — ChoiceSelections deserializes
        // to null from a hand-crafted JSON body missing the field; BuildPlanAsync must not NRE.
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm8", "equipagemnpc8@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        request.Content = JsonContent.Create(new { KitId = patrulheiro.Id });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Choose_a_kit_that_would_push_an_Artefato_TipoDeAlvo_past_3_returns_400_and_grants_nothing()
    {
        // Mirrors CharacterEquipagemControllerTests's identical test — NpcPossessionsController.AddArtifact's
        // own "limite de 3 por TipoDeAlvo" cap must hold for the Equipagem grant path too.
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm9", "equipagemnpc9@teste.com");
        await GrantRulesAuditorAsync("equipagemnpc9@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var artifact1 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem Npc 1", "Atributo", "Vigor", 1);
        var artifact2 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem Npc 2", "Atributo", "Forca", 1);
        var artifact3 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem Npc 3", "Atributo", "Agilidade", 1);
        var artifact4Nome = "Anel Equipagem Npc 4";
        await CreateArtefatoItemAsync(gmToken, artifact4Nome, "Atributo", "Astucia", 1);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifact1)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifact2)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifact3)));

        var kitRequest = new CreateEquipmentKitRequest("Kit Npc com Artefato", "Kit de teste com Artefato", 0,
            [new EquipmentKitItemInput(artifact4Nome, "Artefato", 1, null)], []);
        var kitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, kitRequest));
        var kit = await kitResponse.Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var chooseResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(kit!.Id, [])));

        chooseResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        sheet!.EquipmentKitId.Should().BeNull();

        var artifactsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/artifacts", gmToken));
        var artifacts = await artifactsResponse.Content.ReadFromJsonAsync<List<NpcArtifactResponse>>();
        artifacts!.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListKits_and_Choose_by_a_different_GM_return_404()
    {
        // Same GrantedSheetAuthorization.CanEdit gate as every other Npc sheet sub-controller,
        // returning NotFound rather than Forbid (see PendingRacialTraitChoice_by_a_different_gm_returns_404
        // in NpcRacialTraitsControllerTests.cs) — avoids confirming the sheet exists to a stranger.
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("EquipagemNpcGmOwner10", "equipagemnpcowner10@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("EquipagemNpcGmOther10", "equipagemnpcother10@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmTokenOther));
        listResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var chooseResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmTokenOther,
            new ChooseEquipmentKitRequest(Guid.NewGuid().ToString(), [])));
        chooseResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Choose_auto_attaches_every_granted_item_to_the_sheet_s_campaign_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm6", "equipagemnpc6@teste.com");
        var (campaignId, sheetId) = await CreateCampaignAndGrantedNpcSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var viajante = kits!.Single(k => k.Nome == "Viajante");

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(viajante.Id, [])));

        var meResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = Guid.Parse((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        // Scoped by GmId — see the identical note in CharacterEquipagemControllerTests's mirror of
        // this test: ItemGeral "Mochila" is per-GM, and this class's Facts share one database.
        var mochila = await db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>().SingleAsync(i => i.Nome == "Mochila" && i.GmId == gmId);
        var attachment = await db.CampaignAttachments.SingleAsync(a => a.CampaignId == Guid.Parse(campaignId) && a.ItemId == mochila.Id);
        attachment.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Choose_on_a_GM_owned_Npc_not_attached_to_any_campaign_skips_the_auto_attach_step_without_erroring()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm7", "equipagemnpc7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // this repo's existing Npc-sheet-creation helper — not attached to any campaign
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var negociante = kits!.Single(k => k.Nome == "Negociante");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
