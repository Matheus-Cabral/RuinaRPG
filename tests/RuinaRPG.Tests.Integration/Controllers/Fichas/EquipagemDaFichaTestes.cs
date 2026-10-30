using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;

// Respostas e pedidos de arsenal, posses e ficha têm o mesmo JSON no Personagem e no NPC nos campos lidos aqui.
using AdicionarArtefato = RuinaRPG.Contracts.Fichas.AddArtifactRequest;
using ArmaLidaDaEquipagem = RuinaRPG.Contracts.Fichas.WeaponResponse;
using ArtefatoLidoDaEquipagem = RuinaRPG.Contracts.Fichas.ArtifactResponse;
using EscudoLidoDaEquipagem = RuinaRPG.Contracts.Fichas.ShieldResponse;
using ItemLidoDaEquipagem = RuinaRPG.Contracts.Fichas.InventoryItemResponse;
using SlotLidoDaEquipagem = RuinaRPG.Contracts.Fichas.ArmorSlotResponse;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// Equipagem inicial (<c>/equipagem</c>), que só Personagem e NPC têm. O Mestre escolhe o kit: no
/// Personagem, na ficha do Jogador da campanha; no NPC, no seu próprio NPC.
/// </summary>
public abstract class EquipagemDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    [Fact]
    public async Task ListKits_returns_every_seeded_kit_with_choice_slot_options_resolved_against_the_GM_s_catalog()
    {
        var f = await NovaFichaAsync();
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/equipagem/kits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        kits!.Should().HaveCountGreaterOrEqualTo(12);
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");
        patrulheiro.ChoiceSlots.Should().ContainSingle();
        patrulheiro.ChoiceSlots.Single().Options.Should().NotBeEmpty(); // Varinha de Carvalho and every default Tier-F weapon are seeded per-GM already
    }

    [Fact]
    public async Task Choose_a_kit_with_no_choice_slots_places_every_fixed_item_and_adds_Ciclos()
    {
        var f = await NovaFichaAsync();
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);
        var kits = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var viajante = kits!.Single(k => k.Nome == "Viajante");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(viajante.Id, [])));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}", gmToken));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<FichaLida>();
        sheet!.EquipmentKitId.Should().Be(viajante.Id);
        sheet.Ciclos.Should().Be(25);

        var inventoryResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/inventory", gmToken));
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<List<ItemLidoDaEquipagem>>();
        inventory!.Should().Contain(i => i.Nome == "Mochila");
        inventory.Should().Contain(i => i.Nome == "Ração de Viagem" && i.Qtd == 2);
    }

    [Fact]
    public async Task Choose_a_kit_with_a_choice_slot_places_the_selected_weapon()
    {
        var f = await NovaFichaAsync();
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);
        var kits = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");
        var chosenWeapon = patrulheiro.ChoiceSlots.Single().Options.First();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(patrulheiro.Id, [new ChoiceSlotSelectionRequest(patrulheiro.ChoiceSlots.Single().SlotId, chosenWeapon.ItemId)])));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var weaponsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/weapons", gmToken));
        var weapons = await weaponsResponse.Content.ReadFromJsonAsync<List<ArmaLidaDaEquipagem>>();
        weapons!.Should().ContainSingle(w => w.Nome == chosenWeapon.Nome);

        var shieldsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/shields", gmToken));
        var shields = await shieldsResponse.Content.ReadFromJsonAsync<List<EscudoLidoDaEquipagem>>();
        shields!.Should().ContainSingle(s => s.Nome == "Tampa de Madeira");
    }

    [Fact]
    public async Task Choose_Cacador_with_the_Arco_alternative_also_grants_10_Flechas_de_Madeira()
    {
        var f = await NovaFichaAsync();
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);

        // DefaultCatalogItems.cs's Tier-F rows for "Arcos"/"Fundas e Baladeiras" are empty (its lowest
        // bow is Arco Curto at Tier E) — this test can't rely on incidental default-catalog data for
        // Caçador's choice slot, so it seeds its own Tier-F "Arcos" weapon directly, the same way a GM
        // would need to before this kit's choice slot has any real option.
        var meResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = (await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        Guid testBowId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var bow = new RuinaRPG.Infrastructure.Items.Arma
            {
                Id = Guid.NewGuid(), GmId = Guid.Parse(gmId), Nome = "Arco de Teste F", Subcategoria = "Arcos",
                Rank = RuinaRPG.Domain.Items.RankDeItem.F, Peso = 1, Preco = 0,
            };
            db.Add(bow);
            await db.SaveChangesAsync();
            testBowId = bow.Id;
        }

        var kits = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var cacador = kits!.Single(k => k.Nome == "Caçador");
        var slot = cacador.ChoiceSlots.Single();
        var chosen = slot.Options.Single(o => o.ItemId == testBowId.ToString());

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(cacador.Id, [new ChoiceSlotSelectionRequest(slot.SlotId, chosen.ItemId)])));

        var inventoryResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/inventory", gmToken));
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<List<ItemLidoDaEquipagem>>();
        inventory!.Should().Contain(i => i.Nome == "Flecha de Madeira" && i.Qtd == 10);

        // Durabilidade por Rank: a Rank-F kit weapon lands with atual = max = 20 (Tabela).
        var weaponsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/weapons", gmToken));
        var weapons = await weaponsResponse.Content.ReadFromJsonAsync<List<ArmaLidaDaEquipagem>>();
        var bowOnSheet = weapons!.Single(w => w.ItemId == testBowId.ToString());
        bowOnSheet.DurabilidadeAtual.Should().Be(20);
        bowOnSheet.DurabilidadeMaxima.Should().Be(20);
    }

    [Fact]
    public async Task Choose_a_second_time_on_an_already_chosen_sheet_returns_400()
    {
        var f = await NovaFichaAsync();
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);
        var kits = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var negociante = kits!.Single(k => k.Nome == "Negociante");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        var second = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Choose_with_ChoiceSelections_omitted_from_the_request_body_returns_400_not_500()
    {
        // ChooseEquipmentKitRequest.ChoiceSelections is a non-nullable List<...> in the C# record,
        // but a hand-crafted JSON body omitting the field deserializes it to null. Without a
        // null-coalescing guard, BuildPlanAsync's foreach over choiceSlots would NRE (500) the first
        // time it tries selections.FirstOrDefault(...) for a kit that actually has a choice slot
        // (Patrulheiro) — it should fail cleanly with 400 instead.
        var f = await NovaFichaAsync();
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);
        var kits = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        request.Content = JsonContent.Create(new { KitId = patrulheiro.Id });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Choose_a_kit_that_would_push_an_Artefato_TipoDeAlvo_past_3_returns_400_and_grants_nothing()
    {
        // Mirrors the Possessions controller's AddArtifact's own "limite de 3 por TipoDeAlvo
        // validado na aplicação" cap (Requisitos - Modelo de Dados) — no path in the Equipagem
        // feature enforced this before this fix, even though nothing stops an Auditor from
        // authoring a kit with an Artefato fixed item via the Auditoria page's Tipo dropdown.
        var f = await NovaFichaAsync();
        await GrantRulesAuditorAsync(f.GmEmail);
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);

        var artifact1 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem 1", "Atributo", "Vigor", 1);
        var artifact2 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem 2", "Atributo", "Forca", 1);
        var artifact3 = await CreateArtefatoItemAsync(gmToken, "Anel Equipagem 3", "Atributo", "Agilidade", 1);
        var artifact4Nome = "Anel Equipagem 4";
        await CreateArtefatoItemAsync(gmToken, artifact4Nome, "Atributo", "Astucia", 1);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/artifacts", gmToken, new AdicionarArtefato(artifact1)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/artifacts", gmToken, new AdicionarArtefato(artifact2)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/artifacts", gmToken, new AdicionarArtefato(artifact3)));

        var artifact4FixoResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kit-fixed-items", gmToken,
            new CreateItemRequest("Artefato", artifact4Nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, "Atributo", "Astucia", 1, null)));
        artifact4FixoResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var artifact4Fixo = (await artifact4FixoResponse.Content.ReadFromJsonAsync<EquipmentKitFixedItemResponse>())!;

        var kitRequest = new CreateEquipmentKitRequest("Kit com Artefato", "Kit de teste com Artefato", 0,
            [new EquipmentKitItemInput(artifact4Fixo.Id, 1)], []);
        var kitResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, kitRequest));
        var kit = await kitResponse.Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var chooseResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(kit!.Id, [])));

        chooseResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}", gmToken));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<FichaLida>();
        sheet!.EquipmentKitId.Should().BeNull(); // rejected before SaveChangesAsync — nothing partially applied

        var artifactsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/artifacts", gmToken));
        var artifacts = await artifactsResponse.Content.ReadFromJsonAsync<List<ArtefatoLidoDaEquipagem>>();
        artifacts!.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListKits_and_Choose_by_someone_without_access_return_the_denied_status()
    {
        // Same CanEdit gate as every other sheet sub-controller — a caller who cannot edit the sheet
        // cannot list kits or choose one (403 no Personagem; 404 no NPC, sem confirmar que a ficha existe).
        var f = await NovaFichaAsync();
        var sheetId = f.SheetId;
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/equipagem/kits", otherToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var chooseResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", otherToken,
            new ChooseEquipmentKitRequest(Guid.NewGuid().ToString(), [])));
        chooseResponse.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task Choose_auto_attaches_every_granted_item_to_the_sheet_s_campaign_as_public()
    {
        // No NPC, só um NPC concedido a um Jogador fica numa campanha (a ficha de NPC não tem CampaignId).
        var f = await FichaDeJogadorAsync();
        var (gmToken, sheetId, campaignId) = (f.GmToken, f.SheetId, f.CampaignId!);
        var kits = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var viajante = kits!.Single(k => k.Nome == "Viajante");

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(viajante.Id, [])));

        var meResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = Guid.Parse((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        // Scoped by GmId — ItemGeral "Mochila" is per-GM (Equipagem's fixed-item resolution creates
        // one independently for every GM that applies a kit containing it), so an unscoped Nome-only
        // lookup would collide with another test in this class that also grants a "Mochila" (e.g.
        // Choose_a_kit_with_no_choice_slots_places_every_fixed_item_and_adds_Ciclos above), since all
        // Facts in this class share one Postgres database.
        var mochila = await db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>().SingleAsync(i => i.Nome == "Mochila" && i.GmId == gmId);
        var attachment = await db.CampaignAttachments.SingleAsync(a => a.CampaignId == Guid.Parse(campaignId) && a.ItemId == mochila.Id);
        attachment.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Choose_a_kit_with_an_Armadura_choice_slot_updates_the_target_armor_slot()
    {
        var f = await NovaFichaAsync();
        await GrantRulesAuditorAsync(f.GmEmail);
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);

        var meResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = (await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        Guid armaduraId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var armadura = new RuinaRPG.Infrastructure.Items.Armadura
            {
                Id = Guid.NewGuid(), GmId = Guid.Parse(gmId), Nome = "Armadura de Teste F", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro",
                Peso = 1, Preco = 0, // no Rank → no durability (Durabilidade por Rank)
            };
            db.Add(armadura);
            await db.SaveChangesAsync();
            armaduraId = armadura.Id;
        }

        var kitResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken,
            new CreateEquipmentKitRequest("Kit com Armadura", "D", 0, [],
                [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", ["Couro"], null, 1, null, null, null, "Superior")])));
        var kit = await kitResponse.Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var chooseResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(kit!.Id, [new ChoiceSlotSelectionRequest(kit.ChoiceSlots.Single().Id, armaduraId.ToString())])));
        chooseResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var slotsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/armor-slots", gmToken));
        var slots = await slotsResponse.Content.ReadFromJsonAsync<List<SlotLidoDaEquipagem>>();
        var superior = slots!.Single(s => s.Slot == "Superior");
        superior.Nome.Should().Be("Armadura de Teste F");
        superior.DurabilidadeAtual.Should().Be(0);
        superior.DurabilidadeMaxima.Should().BeNull();
    }

    [Fact]
    public async Task Choose_a_kit_that_would_CREATE_an_Artefato_past_the_cap_of_3_returns_400_and_persists_nothing()
    {
        // O Artefato fixo não está no catálogo do GM: a aplicação o criaria. O teto de 3 por TipoDeAlvo
        // tem de valer também para o item que ainda só existe no change tracker.
        var f = await NovaFichaAsync();
        await GrantRulesAuditorAsync(f.GmEmail);
        var (gmToken, sheetId) = (f.GmToken, f.SheetId);

        foreach (var (nome, alvo) in new[] { ("Anel Character Teto 1", "Vigor"), ("Anel Character Teto 2", "Forca"), ("Anel Character Teto 3", "Agilidade") })
        {
            var id = await CreateArtefatoItemAsync(gmToken, nome, "Atributo", alvo, 1);
            await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/artifacts", gmToken, new AdicionarArtefato(id)));
        }

        var fixoNome = "Anel Character Teto Novo " + Guid.NewGuid().ToString("N");
        var fixoResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kit-fixed-items", gmToken,
            new CreateItemRequest("Artefato", fixoNome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, "Atributo", "Astucia", 1, null)));
        fixoResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var fixo = (await fixoResponse.Content.ReadFromJsonAsync<EquipmentKitFixedItemResponse>())!;
        var kit = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken,
            new CreateEquipmentKitRequest("Kit Teto Criando " + Guid.NewGuid().ToString("N"), "D", 0, [new EquipmentKitItemInput(fixo.Id, 1)], []))))
            .Content.ReadFromJsonAsync<EquipmentKitResponse>())!;

        var choose = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(kit.Id, [])));

        choose.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await choose.Content.ReadAsStringAsync()).Should().Contain("Limite de 3 Artefatos do tipo Atributo já atingido.");
        var artifacts = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}/artifacts", gmToken))).Content.ReadFromJsonAsync<List<ArtefatoLidoDaEquipagem>>();
        artifacts!.Should().HaveCount(3);
        var gmId = Guid.Parse((await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken))).Content.ReadFromJsonAsync<MeResponse>())!.Id);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        (await db.Items.CountAsync(i => i.GmId == gmId && i.Nome == fixoNome)).Should().Be(0);
    }
}

/// <summary>Personagem: o Mestre escolhe o kit na ficha do Jogador da campanha.</summary>
public sealed class EquipagemDaFichaTestesPersonagem(ApiFixture fixture) : EquipagemDaFichaTestes<TipoDeTestePersonagem>(fixture);

public sealed class EquipagemDaFichaTestesNpc(ApiFixture fixture) : EquipagemDaFichaTestes<TipoDeTesteNpc>(fixture)
{
    [Fact]
    public async Task Choose_on_a_GM_owned_Npc_not_attached_to_any_campaign_skips_the_auto_attach_step_without_erroring()
    {
        var f = await NovaFichaAsync(); // NPC do Mestre, fora de qualquer campanha
        var kits = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "equipagem/kits"), f.GmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var negociante = kits!.Single(k => k.Nome == "Negociante");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "equipagem/choose"), f.GmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
