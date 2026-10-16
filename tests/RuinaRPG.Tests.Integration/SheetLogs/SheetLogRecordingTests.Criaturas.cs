using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Integration.SheetLogs;

/// <summary>Fichas de Criatura no log e o nome que identifica cada linha filha (ItemNome).</summary>
public partial class SheetLogRecordingTests
{
    private Task<List<SheetLogEntry>> LogDaCriaturaAsync(Guid sheetId) => LogAsync(l => l.CreatureSheetId == sheetId);

    private async Task<List<SheetLogEntry>> NovasDaCriaturaAsync(Guid sheetId, Func<Task> acao)
    {
        var antes = (await LogDaCriaturaAsync(sheetId)).Select(l => l.Id).ToHashSet();
        await acao();
        return (await LogDaCriaturaAsync(sheetId)).Where(l => !antes.Contains(l.Id)).ToList();
    }

    private async Task<Guid> ConcederCriaturaAsync(Cenario c)
    {
        var response = await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{c.CampaignId}/grants", c.GmToken, new GrantSheetRequest(c.PlayerId.ToString(), "Creature", null));
        return Guid.Parse((await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId);
    }

    /// <summary>Salva a ficha de Criatura como está, trocando só o Nome.</summary>
    private async Task RenomearCriaturaAsync(string token, Guid sheetId, string nome)
    {
        var r = (await (await SendOkAsync(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", token)).Content.ReadFromJsonAsync<CreatureSheetResponse>())!;
        await SendOkAsync(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", token, new UpdateCreatureSheetRequest(
            r.ImageId, nome, r.Raca, r.Arquetipo, r.SubArquetipo, r.Afinidade, r.Rank, r.Nivel, r.ExperienciaAtual, r.PontosDeIgnicao,
            r.VitalidadeAtual, r.FocoAtual, r.AdrenalinaAtual, r.Cobertura));
    }

    private async Task<string> CriarItemAsync(string gmToken, CreateItemRequest request) =>
        (await (await SendOkAsync(HttpMethod.Post, "/api/items", gmToken, request)).Content.ReadFromJsonAsync<ItemResponse>())!.Id;

    private Task<string> CriarArmaAsync(string gmToken, string nome) => CriarItemAsync(gmToken,
        new CreateItemRequest("Arma", nome, 2m, 50, null, "Espadas", null, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
            null, null, null, null, null, null, null, null, null));

    private Task<string> CriarArmaduraAsync(string gmToken, string nome) => CriarItemAsync(gmToken,
        new CreateItemRequest("Armadura", nome, 3m, 30, null, null, null, null, null, null, null, null, null, null, "Medio", 5, 3, 2, null, null, null, null, null));

    private Task<string> CriarItemGeralAsync(string gmToken, string nome) => CriarItemAsync(gmToken,
        new CreateItemRequest("ItemGeral", nome, 1m, 5, null, "Diversos", "Um item qualquer", null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null));

    [Fact]
    public async Task A_granted_creature_is_logged_under_its_own_sheet_for_the_player_and_for_the_gm()
    {
        var c = await CenarioAsync("Criatura");
        var sheetId = await ConcederCriaturaAsync(c);

        var doJogador = await NovasDaCriaturaAsync(sheetId, () => RenomearCriaturaAsync(c.PlayerToken, sheetId, "Lobo Sombrio"));
        var doGm = await NovasDaCriaturaAsync(sheetId, () => RenomearCriaturaAsync(c.GmToken, sheetId, "Lobo da Ruína"));

        var linha = doJogador.Should().ContainSingle().Subject;
        linha.CreatureSheetId.Should().Be(sheetId);
        linha.CharacterSheetId.Should().BeNull();
        linha.NpcSheetId.Should().BeNull();
        linha.Acao.Should().Be(SheetLogAcao.Alterou);
        linha.Secao.Should().Be(Secao<CreatureSheet>());
        linha.Campo.Should().Be(Rotulo<CreatureSheet>("Nome"));
        linha.ValorAntes.Should().Be("—");
        linha.ValorDepois.Should().Be("Lobo Sombrio");
        linha.ActorUserId.Should().Be(c.PlayerId);
        linha.ActorNickname.Should().Be(c.PlayerNickname);
        linha.ActorIsGm.Should().BeFalse();
        var linhaDoGm = doGm.Should().ContainSingle().Subject;
        linhaDoGm.ValorAntes.Should().Be("Lobo Sombrio");
        linhaDoGm.ValorDepois.Should().Be("Lobo da Ruína");
        linhaDoGm.ActorUserId.Should().Be(c.GmId);
        linhaDoGm.ActorIsGm.Should().BeTrue();
        // A concessão em si é uma linha só, do GM.
        var criou = (await LogDaCriaturaAsync(sheetId)).Where(l => l.Acao == SheetLogAcao.Criou).Should().ContainSingle().Subject;
        criou.ActorIsGm.Should().BeTrue();
        (await LogDaCriaturaAsync(sheetId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task A_creature_that_is_not_granted_is_not_logged()
    {
        var c = await CenarioAsync("CriaturaDoGm");
        var response = await SendOkAsync(HttpMethod.Post, "/api/creature-sheets", c.GmToken);
        var sheetId = Guid.Parse((await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id);

        await RenomearCriaturaAsync(c.GmToken, sheetId, "Besta do Bestiário");
        await SendOkAsync(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", c.GmToken, new AddCreatureWeaponRequest(null, "Garras", "Cortante", "1D6", 1));

        (await LogDaCriaturaAsync(sheetId)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_creature_weapon_is_named_by_its_catalog_item_or_by_its_manual_name()
    {
        var c = await CenarioAsync("CriaturaArma");
        var sheetId = await ConcederCriaturaAsync(c);
        var armaId = await CriarArmaAsync(c.GmToken, "Lança de Osso");
        string manualId = null!;

        var doCatalogo = await NovasDaCriaturaAsync(sheetId, () =>
            SendOkAsync(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", c.PlayerToken, new AddCreatureWeaponRequest(armaId, null, null, null, null)));
        var manual = await NovasDaCriaturaAsync(sheetId, async () =>
        {
            var response = await SendOkAsync(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/weapons", c.PlayerToken, new AddCreatureWeaponRequest(null, "Mordida", "Perfurante", "1D8", 2));
            manualId = (await response.Content.ReadFromJsonAsync<CreatureWeaponResponse>())!.Id;
        });
        var aoEquipar = await NovasDaCriaturaAsync(sheetId, () =>
            SendOkAsync(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/weapons/{manualId}", c.PlayerToken, true));

        var adicionouDoCatalogo = doCatalogo.Should().ContainSingle().Subject;
        adicionouDoCatalogo.Acao.Should().Be(SheetLogAcao.Adicionou);
        adicionouDoCatalogo.Secao.Should().Be(Secao<CreatureWeapon>());
        adicionouDoCatalogo.ItemNome.Should().Be("Lança de Osso");
        var adicionouManual = manual.Should().ContainSingle().Subject;
        adicionouManual.Acao.Should().Be(SheetLogAcao.Adicionou);
        adicionouManual.ItemNome.Should().Be("Mordida");
        var equipou = aoEquipar.Should().ContainSingle().Subject;
        equipou.Acao.Should().Be(SheetLogAcao.Alterou);
        equipou.ItemNome.Should().Be("Mordida");
        equipou.Campo.Should().Be(Rotulo<CreatureWeapon>("IsEquipped"));
        equipou.ValorAntes.Should().Be("Não");
        equipou.ValorDepois.Should().Be("Sim");
    }

    [Fact]
    public async Task A_creature_exclusive_characteristic_is_named_by_the_characteristic()
    {
        var c = await CenarioAsync("CriaturaTraco");
        var sheetId = await ConcederCriaturaAsync(c);
        var exclusiva = new CreatureExclusiveTrait
        {
            Id = Guid.NewGuid(), Nome = "Couraça Viva do Log", Descricao = "Pele endurecida.", Custo = 2, Polaridade = Polaridade.Positiva, RequerEspecificacao = false,
        };
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.Set<CreatureExclusiveTrait>().Add(exclusiva);
            await db.SaveChangesAsync();
        }

        var novas = await NovasDaCriaturaAsync(sheetId, () =>
            SendOkAsync(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", c.PlayerToken, new AddCreatureTraitRequest(exclusiva.Id.ToString(), null)));

        var linha = novas.Should().ContainSingle().Subject;
        linha.Acao.Should().Be(SheetLogAcao.Adicionou);
        linha.Secao.Should().Be(Secao<CreatureTrait>());
        linha.ItemNome.Should().Be("Couraça Viva do Log");
    }

    [Fact]
    public async Task An_armor_slot_is_named_by_the_slot_and_its_item_is_a_field_rendered_by_name()
    {
        var c = await CenarioAsync("Armadura");
        var armaduraId = await CriarArmaduraAsync(c.GmToken, "Elmo de Ferro");
        var url = $"/api/character-sheets/{c.SheetId}/armor-slots/Capacete";

        var aoEquipar = await NovasAsync(c.SheetId, () => SendOkAsync(HttpMethod.Put, url, c.PlayerToken, new UpdateCharacterArmorSlotRequest(armaduraId)));
        // Ao tirar, o item de antes está preenchido: a linha continua sendo o slot, não o item.
        var aoTirar = await NovasAsync(c.SheetId, () => SendOkAsync(HttpMethod.Put, url, c.PlayerToken, new UpdateCharacterArmorSlotRequest(null)));

        aoEquipar.Concat(aoTirar).Should().OnlyContain(l => l.Acao == SheetLogAcao.Alterou
            && l.Secao == Secao<CharacterArmorSlot>() && l.ItemNome == SheetLogValues.Render(ArmorSlotType.Capacete));
        var equipou = aoEquipar.Single(l => l.Campo == Rotulo<CharacterArmorSlot>("ItemId"));
        equipou.ValorAntes.Should().Be("—");
        equipou.ValorDepois.Should().Be("Elmo de Ferro");
        aoEquipar.Select(l => l.Campo).Should().BeSubsetOf(new[] { Rotulo<CharacterArmorSlot>("ItemId"), Rotulo<CharacterArmorSlot>("DurabilidadeAtual") });
        var tirou = aoTirar.Single(l => l.Campo == Rotulo<CharacterArmorSlot>("ItemId"));
        tirou.ValorAntes.Should().Be("Elmo de Ferro");
        tirou.ValorDepois.Should().Be("—");
    }

    [Fact]
    public async Task A_skill_row_is_named_by_its_pericia()
    {
        var c = await CenarioAsync("Pericia");
        PericiaDefinicao pericia;
        await using (var scope = Factory.Services.CreateAsyncScope())
            pericia = await scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>().Pericias.AsNoTracking().SingleAsync(p => p.Chave == "Atletismo");

        var novas = await NovasAsync(c.SheetId, () =>
            SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/skills/Atletismo", c.PlayerToken,
                new UpdateCharacterSkillRequest(1, pericia.AtributoSugerido?.ToString())));

        var linha = novas.Should().ContainSingle().Subject;
        linha.Acao.Should().Be(SheetLogAcao.Alterou);
        linha.Secao.Should().Be(Secao<CharacterSkill>());
        linha.ItemNome.Should().Be(pericia.Nome);
        linha.Campo.Should().Be(Rotulo<CharacterSkill>("Gasto"));
        linha.ValorAntes.Should().Be("0");
        linha.ValorDepois.Should().Be("1");
    }

    [Fact]
    public async Task An_inventory_row_is_named_by_its_item()
    {
        var c = await CenarioAsync("Inventario");
        var itemId = await CriarItemGeralAsync(c.GmToken, "Corda de Cânhamo");

        var novas = await NovasAsync(c.SheetId, () =>
            SendOkAsync(HttpMethod.Post, $"/api/character-sheets/{c.SheetId}/inventory", c.PlayerToken, new AddCharacterInventoryItemRequest(itemId, 2)));

        var linha = novas.Should().ContainSingle().Subject;
        linha.Acao.Should().Be(SheetLogAcao.Adicionou);
        linha.Secao.Should().Be(Secao<CharacterInventoryItem>());
        linha.ItemNome.Should().Be("Corda de Cânhamo");
    }
}
