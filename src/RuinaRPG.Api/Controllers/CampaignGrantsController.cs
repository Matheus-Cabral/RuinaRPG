using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/campaigns/{campaignId}/grants")]
public class CampaignGrantsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Grant(Guid campaignId, GrantSheetRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var playerId = Guid.Parse(request.PlayerId);
        var isMember = await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == playerId);
        if (!isMember)
            return BadRequest("O jogador informado não é membro desta campanha.");

        if (request.Tipo == "Npc")
        {
            NpcSheet? newSheet;
            if (request.SourceSheetId is null)
            {
                newSheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gmId, OwnerId = playerId };
                SeedBlankNpcChildren(newSheet.Id);
            }
            else
            {
                newSheet = await DeepCopyNpcAsync(Guid.Parse(request.SourceSheetId), gmId, playerId);
            }
            if (newSheet is null)
                return BadRequest("Ficha de NPC de origem não encontrada.");
            db.NpcSheets.Add(newSheet);
            db.CampaignAttachments.Add(new CampaignAttachment
            {
                Id = Guid.NewGuid(), CampaignId = campaignId, NpcSheetId = newSheet.Id,
                IsPublic = false, NpcNomePublico = false, NpcImagemPublica = false, CreatureNomePublico = false, CreatureImagemPublica = false
            });
            await db.SaveChangesAsync();
            return Created(string.Empty, new GrantSheetResponse(newSheet.Id.ToString(), "Npc"));
        }
        if (request.Tipo == "Creature")
        {
            CreatureSheet? newSheet;
            if (request.SourceSheetId is null)
            {
                newSheet = new CreatureSheet { Id = Guid.NewGuid(), GmId = gmId, OwnerId = playerId };
                SeedBlankCreatureChildren(newSheet.Id);
            }
            else
            {
                newSheet = await DeepCopyCreatureAsync(Guid.Parse(request.SourceSheetId), gmId, playerId);
            }
            if (newSheet is null)
                return BadRequest("Ficha de Criatura de origem não encontrada.");
            db.CreatureSheets.Add(newSheet);
            db.CampaignAttachments.Add(new CampaignAttachment
            {
                Id = Guid.NewGuid(), CampaignId = campaignId, CreatureSheetId = newSheet.Id,
                IsPublic = false, NpcNomePublico = false, NpcImagemPublica = false, CreatureNomePublico = false, CreatureImagemPublica = false
            });
            await db.SaveChangesAsync();
            return Created(string.Empty, new GrantSheetResponse(newSheet.Id.ToString(), "Creature"));
        }

        return BadRequest("Tipo deve ser 'Npc' ou 'Creature'.");
    }

    /// <summary>
    /// Mirrors NpcSheetsController.Create's child-row seeding exactly (one row per Atributo,
    /// per Pericia, per ArmorSlotType) — a blank grant is otherwise indistinguishable from a
    /// sheet created directly, and downstream reads (e.g. NpcSheetsController.Get) assume every
    /// Atributo/Pericia has exactly one row.
    /// </summary>
    private void SeedBlankNpcChildren(Guid npcSheetId)
    {
        foreach (var atributo in Enum.GetValues<Atributo>())
            db.NpcAttributes.Add(new NpcAttribute { Id = Guid.NewGuid(), NpcSheetId = npcSheetId, Atributo = atributo });
        foreach (var pericia in Enum.GetValues<Pericia>())
            db.NpcSkills.Add(new NpcSkill { Id = Guid.NewGuid(), NpcSheetId = npcSheetId, Pericia = pericia });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.NpcArmorSlots.Add(new NpcArmorSlot { Id = Guid.NewGuid(), NpcSheetId = npcSheetId, Slot = slot });
    }

    /// <summary>
    /// Mirrors CreatureSheetsController.Create's child-row seeding exactly, including the
    /// shorter CreatureSkillAllowList (R0005's fixed shorter list) instead of every Pericia.
    /// </summary>
    private void SeedBlankCreatureChildren(Guid creatureSheetId)
    {
        foreach (var atributo in Enum.GetValues<AtributoCriatura>())
            db.CreatureAttributes.Add(new CreatureAttribute { Id = Guid.NewGuid(), CreatureSheetId = creatureSheetId, Atributo = atributo });
        foreach (var pericia in CreatureSkillAllowList.AllowedPericias)
            db.CreatureSkills.Add(new CreatureSkill { Id = Guid.NewGuid(), CreatureSheetId = creatureSheetId, Pericia = pericia });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.CreatureArmorSlots.Add(new CreatureArmorSlot { Id = Guid.NewGuid(), CreatureSheetId = creatureSheetId, Slot = slot });
    }

    private async Task<NpcSheet?> DeepCopyNpcAsync(Guid sourceId, Guid gmId, Guid ownerId)
    {
        var source = await db.NpcSheets.FindAsync(sourceId);
        if (source is null)
            return null;

        var copy = new NpcSheet
        {
            Id = Guid.NewGuid(), GmId = gmId, OwnerId = ownerId, ImageId = source.ImageId, Nome = source.Nome,
            Linhagem = source.Linhagem, Variante = source.Variante, Vocacao = source.Vocacao, SubVocacao = source.SubVocacao,
            Afinidade = source.Afinidade, Propriedade = source.Propriedade, Nivel = source.Nivel, Circulo = source.Circulo,
            Grau = source.Grau, PossuiCoracaoDeMana = source.PossuiCoracaoDeMana, ExperienciaAtual = source.ExperienciaAtual,
            EAPAtual = source.EAPAtual, NucleosRankF = source.NucleosRankF, NucleosRankE = source.NucleosRankE,
            NucleosRankD = source.NucleosRankD, NucleosRankC = source.NucleosRankC, NucleosRankB = source.NucleosRankB,
            NucleosRankA = source.NucleosRankA, NucleosRankS = source.NucleosRankS,
            PontosDeIgnicaoAtual = source.PontosDeIgnicaoAtual, PontosDeIgnicaoTotal = source.PontosDeIgnicaoTotal,
            VitalidadeAtual = source.VitalidadeAtual, FocoAtual = source.FocoAtual, AdrenalinaAtual = source.AdrenalinaAtual,
            EstresseAtual = source.EstresseAtual, Cobertura = source.Cobertura, Ciclos = source.Ciclos
        };

        // Deep-copy every child table — mechanical, one loop per table, same shape each time.
        foreach (var a in await db.NpcAttributes.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcAttributes.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Atributo = a.Atributo, Gasto = a.Gasto, Bonus = a.Bonus, TemMaestria = a.TemMaestria });
        foreach (var s in await db.NpcSkills.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcSkills.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Pericia = s.Pericia, Gasto = s.Gasto });
        foreach (var a in await db.NpcAffinities.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcAffinities.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Elemento = a.Elemento, SubElemento = a.SubElemento, CaminhoNome = a.CaminhoNome, Experiencia = a.Experiencia });
        foreach (var r in await db.NpcRunes.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcRunes.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Nome = r.Nome, Descricao = r.Descricao, Grau = r.Grau });
        foreach (var m in await db.NpcMasteries.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcMasteries.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Nome = m.Nome, Pericia = m.Pericia, Atributo = m.Atributo, GastoMaestria = m.GastoMaestria });
        foreach (var w in await db.NpcWeapons.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcWeapons.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, ItemId = w.ItemId, IsEquipped = w.IsEquipped, DurabilidadeAtual = w.DurabilidadeAtual });
        foreach (var slot in await db.NpcArmorSlots.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcArmorSlots.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Slot = slot.Slot, ItemId = slot.ItemId, DurabilidadeAtual = slot.DurabilidadeAtual });
        foreach (var sh in await db.NpcShields.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcShields.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, ItemId = sh.ItemId, IsEquipped = sh.IsEquipped, DurabilidadeAtual = sh.DurabilidadeAtual });
        foreach (var inv in await db.NpcInventoryItems.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcInventoryItems.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, ItemId = inv.ItemId, Qtd = inv.Qtd });
        foreach (var art in await db.NpcArtifacts.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcArtifacts.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, ArtifactItemId = art.ArtifactItemId });
        foreach (var sa in await db.NpcSpellAbilities.Where(x => x.NpcSheetId == sourceId).ToListAsync())
        {
            var saCopy = new NpcSpellAbility
            {
                Id = Guid.NewGuid(), NpcSheetId = copy.Id, SourceBankEntryId = sa.SourceBankEntryId, Nome = sa.Nome,
                Tipo = sa.Tipo, Grau = sa.Grau, GastoEmPI = sa.GastoEmPI, Custo = sa.Custo, Descricao = sa.Descricao
            };
            db.NpcSpellAbilities.Add(saCopy);
            foreach (var eff in await db.NpcSpellAbilityEffects.Where(x => x.NpcSpellAbilityId == sa.Id).ToListAsync())
                db.NpcSpellAbilityEffects.Add(new() { Id = Guid.NewGuid(), NpcSpellAbilityId = saCopy.Id, EfeitoNome = eff.EfeitoNome, Quantidade = eff.Quantidade, CustoPI = eff.CustoPI });
        }
        foreach (var aff in await db.NpcAffections.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcAffections.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Nome = aff.Nome, Favorabilidade = aff.Favorabilidade });
        foreach (var t in await db.NpcTraits.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcTraits.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, TraitId = t.TraitId, Polaridade = t.Polaridade });

        return copy;
    }

    private async Task<CreatureSheet?> DeepCopyCreatureAsync(Guid sourceId, Guid gmId, Guid ownerId)
    {
        var source = await db.CreatureSheets.FindAsync(sourceId);
        if (source is null)
            return null;

        var copy = new CreatureSheet
        {
            Id = Guid.NewGuid(), GmId = gmId, OwnerId = ownerId, ImageId = source.ImageId, Nome = source.Nome,
            Raca = source.Raca, Arquetipo = source.Arquetipo, SubArquetipo = source.SubArquetipo, Afinidade = source.Afinidade,
            Rank = source.Rank, Nivel = source.Nivel, ExperienciaAtual = source.ExperienciaAtual,
            PontosDeIgnicao = source.PontosDeIgnicao, VitalidadeAtual = source.VitalidadeAtual, FocoAtual = source.FocoAtual,
            AdrenalinaAtual = source.AdrenalinaAtual, Cobertura = source.Cobertura
        };

        foreach (var a in await db.CreatureAttributes.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureAttributes.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, Atributo = a.Atributo, Gasto = a.Gasto, Bonus = a.Bonus, TemMaestria = a.TemMaestria });
        foreach (var s in await db.CreatureSkills.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureSkills.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, Pericia = s.Pericia, Gasto = s.Gasto });
        foreach (var m in await db.CreatureMasteries.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureMasteries.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, Nome = m.Nome, Pericia = m.Pericia, Atributo = m.Atributo, GastoMaestria = m.GastoMaestria });
        foreach (var w in await db.CreatureWeapons.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureWeapons.Add(new()
            {
                Id = Guid.NewGuid(), CreatureSheetId = copy.Id, ItemId = w.ItemId, IsEquipped = w.IsEquipped, DurabilidadeAtual = w.DurabilidadeAtual,
                ManualNome = w.ManualNome, ManualTipoDeDano = w.ManualTipoDeDano, ManualDados = w.ManualDados, ManualDano = w.ManualDano
            });
        foreach (var slot in await db.CreatureArmorSlots.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureArmorSlots.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, Slot = slot.Slot, ItemId = slot.ItemId, DurabilidadeAtual = slot.DurabilidadeAtual });
        foreach (var sh in await db.CreatureShields.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureShields.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, ItemId = sh.ItemId, IsEquipped = sh.IsEquipped, DurabilidadeAtual = sh.DurabilidadeAtual });
        foreach (var sp in await db.CreatureSpoils.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureSpoils.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, ItemId = sp.ItemId, Qtd = sp.Qtd, DT = sp.DT });
        foreach (var art in await db.CreatureArtifacts.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureArtifacts.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, ArtifactItemId = art.ArtifactItemId });
        foreach (var sa in await db.CreatureSpellAbilities.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
        {
            var saCopy = new CreatureSpellAbility
            {
                Id = Guid.NewGuid(), CreatureSheetId = copy.Id, SourceBankEntryId = sa.SourceBankEntryId, Nome = sa.Nome,
                Tipo = sa.Tipo, Grau = sa.Grau, GastoEmPI = sa.GastoEmPI, Custo = sa.Custo, Descricao = sa.Descricao
            };
            db.CreatureSpellAbilities.Add(saCopy);
            foreach (var eff in await db.CreatureSpellAbilityEffects.Where(x => x.CreatureSpellAbilityId == sa.Id).ToListAsync())
                db.CreatureSpellAbilityEffects.Add(new() { Id = Guid.NewGuid(), CreatureSpellAbilityId = saCopy.Id, EfeitoNome = eff.EfeitoNome, Quantidade = eff.Quantidade, CustoPI = eff.CustoPI });
        }
        foreach (var aff in await db.CreatureAffections.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureAffections.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, Nome = aff.Nome, Favorabilidade = aff.Favorabilidade });
        foreach (var t in await db.CreatureTraits.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureTraits.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, TraitId = t.TraitId, Polaridade = t.Polaridade });

        return copy;
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
