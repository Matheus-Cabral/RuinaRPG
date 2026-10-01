using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/campaigns/{campaignId}/grants")]
public class CampaignGrantsController(RuinaRpgDbContext db, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Grant(Guid campaignId, GrantSheetRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        if (!Guid.TryParse(request.PlayerId, out var playerId))
            return BadRequest("PlayerId inválido.");
        var isMember = await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == playerId);
        if (!isMember)
            return BadRequest("O jogador informado não é membro desta campanha.");

        Guid? sourceSheetId = null;
        if (request.SourceSheetId is not null)
        {
            if (!Guid.TryParse(request.SourceSheetId, out var parsedSourceSheetId))
                return BadRequest("SourceSheetId inválido.");
            sourceSheetId = parsedSourceSheetId;
        }

        if (request.Tipo == "Npc")
        {
            NpcSheet? newSheet;
            if (sourceSheetId is null)
            {
                newSheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gmId, OwnerId = playerId };
                await SeedBlankNpcChildrenAsync(newSheet.Id);
            }
            else
            {
                newSheet = await DeepCopyNpcAsync(sourceSheetId.Value, gmId, playerId, campaignId);
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
            if (sourceSheetId is null)
            {
                newSheet = new CreatureSheet { Id = Guid.NewGuid(), GmId = gmId, OwnerId = playerId };
                await SeedBlankCreatureChildrenAsync(newSheet.Id);
            }
            else
            {
                newSheet = await DeepCopyCreatureAsync(sourceSheetId.Value, gmId, playerId, campaignId);
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
    /// Épico 4 item 5: a GM had no way to see who already has what — granted sheets are hidden
    /// from the Anexos tab by design (removing them there would break the player's companion
    /// link), so this is the only place a GM can audit existing grants for a campaign.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<GrantSummaryResponse>>> List(Guid campaignId)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var results = new List<GrantSummaryResponse>();

        var grantedNpcs = await db.NpcSheets.Where(n => n.GmId == gmId && n.OwnerId != null
            && db.CampaignAttachments.Any(a => a.CampaignId == campaignId && a.NpcSheetId == n.Id)).ToListAsync();
        foreach (var npc in grantedNpcs)
        {
            var owner = await db.Users.FindAsync(npc.OwnerId!.Value);
            results.Add(new GrantSummaryResponse(npc.Id.ToString(), "Npc", npc.Nome, npc.OwnerId.Value.ToString(), owner?.Nickname ?? ""));
        }

        var grantedCreatures = await db.CreatureSheets.Where(c => c.GmId == gmId && c.OwnerId != null
            && db.CampaignAttachments.Any(a => a.CampaignId == campaignId && a.CreatureSheetId == c.Id)).ToListAsync();
        foreach (var creature in grantedCreatures)
        {
            var owner = await db.Users.FindAsync(creature.OwnerId!.Value);
            results.Add(new GrantSummaryResponse(creature.Id.ToString(), "Creature", creature.Nome, creature.OwnerId.Value.ToString(), owner?.Nickname ?? ""));
        }

        return results;
    }

    /// <summary>
    /// Mirrors NpcSheetsController.Create's child-row seeding exactly (one row per Atributo,
    /// per Pericia, per ArmorSlotType) — a blank grant is otherwise indistinguishable from a
    /// sheet created directly, and downstream reads (e.g. NpcSheetsController.Get) assume every
    /// Atributo/Pericia has exactly one row.
    /// </summary>
    private async Task SeedBlankNpcChildrenAsync(Guid npcSheetId)
    {
        foreach (var atributo in Enum.GetValues<Atributo>())
            db.NpcAttributes.Add(new NpcAttribute { Id = Guid.NewGuid(), NpcSheetId = npcSheetId, Atributo = atributo });
        foreach (var pericia in (await pericias.TodasAsync()).Where(p => !p.IsDeleted))
            db.NpcSkills.Add(new NpcSkill { Id = Guid.NewGuid(), NpcSheetId = npcSheetId, PericiaId = pericia.Id, AtributoEscolhido = pericia.AtributoSugerido });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.NpcArmorSlots.Add(new NpcArmorSlot { Id = Guid.NewGuid(), NpcSheetId = npcSheetId, Slot = slot });
    }

    /// <summary>
    /// Mirrors CreatureSheetsController.Create's child-row seeding exactly, including the
    /// shorter list of Perícias flagged DisponivelParaCriaturas (R0005) instead of every Perícia.
    /// </summary>
    private async Task SeedBlankCreatureChildrenAsync(Guid creatureSheetId)
    {
        foreach (var atributo in Enum.GetValues<AtributoCriatura>())
            db.CreatureAttributes.Add(new CreatureAttribute { Id = Guid.NewGuid(), CreatureSheetId = creatureSheetId, Atributo = atributo });
        foreach (var pericia in (await pericias.TodasAsync()).Where(p => !p.IsDeleted && p.DisponivelParaCriaturas))
            db.CreatureSkills.Add(new CreatureSkill { Id = Guid.NewGuid(), CreatureSheetId = creatureSheetId, PericiaId = pericia.Id, AtributoEscolhido = Enum.TryParse<AtributoCriatura>(pericia.AtributoSugerido?.ToString(), out var sugerido) ? sugerido : null });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.CreatureArmorSlots.Add(new CreatureArmorSlot { Id = Guid.NewGuid(), CreatureSheetId = creatureSheetId, Slot = slot });
    }

    /// <summary>
    /// Requisitos - Campanha R0010: cada Magia/Habilidade de uma ficha concedida fica anexada à campanha
    /// como pública, para o jogador vê-la e escolhê-la. Reusa a entrada do banco a que a ficha já aponta;
    /// uma magia antiga, sem esse vínculo, reusa uma entrada idêntica do banco do GM, e só na falta dela é
    /// que uma entrada nova é criada. Devolve a entrada usada, que a cópia da ficha passa a apontar.
    /// </summary>
    private async Task<Guid> PublicarMagiaNaCampanhaAsync(Guid campaignId, Guid gmId, Guid? sourceBankEntryId,
        string nome, SpellAbilityTipo tipo, int grau, int gastoEmPI, int custo, string descricao,
        IEnumerable<(string EfeitoNome, int? Quantidade, int CustoPI)> efeitos, bool deCriatura,
        CategoriaDePassiva? categoria, RequisitosDePassiva? requisitos)
    {
        // Local primeiro: duas magias legadas iguais na mesma concessão reusam a entrada criada para a primeira.
        var entrada = sourceBankEntryId is { } id
            ? db.SpellAbilityBankEntries.Local.FirstOrDefault(e => e.Id == id && e.GmId == gmId)
                ?? await db.SpellAbilityBankEntries.FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId)
            : null;
        entrada ??= db.SpellAbilityBankEntries.Local.FirstOrDefault(e => e.GmId == gmId && e.Nome == nome && e.Tipo == tipo && e.Grau == grau && e.Descricao == descricao)
            ?? await db.SpellAbilityBankEntries.FirstOrDefaultAsync(e => e.GmId == gmId && e.Nome == nome && e.Tipo == tipo && e.Grau == grau && e.Descricao == descricao);
        if (entrada is null)
        {
            entrada = new SpellAbilityBankEntry
            {
                Id = Guid.NewGuid(), GmId = gmId, Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao, DeCriatura = deCriatura,
                // Uma Passiva recriada (a original saiu do banco) mantém Categoria e Requisitos (Banco R0009).
                Categoria = categoria, Requisitos = requisitos
            };
            entrada.Efeitos = efeitos.Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = entrada.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
            db.SpellAbilityBankEntries.Add(entrada);
        }

        var anexo = db.CampaignAttachments.Local.FirstOrDefault(a => a.CampaignId == campaignId && a.SpellAbilityBankEntryId == entrada.Id)
            ?? await db.CampaignAttachments.FirstOrDefaultAsync(a => a.CampaignId == campaignId && a.SpellAbilityBankEntryId == entrada.Id);
        if (anexo is null)
            db.CampaignAttachments.Add(new CampaignAttachment { Id = Guid.NewGuid(), CampaignId = campaignId, SpellAbilityBankEntryId = entrada.Id, IsPublic = true });
        else
            anexo.IsPublic = true;

        return entrada.Id;
    }

    private async Task<NpcSheet?> DeepCopyNpcAsync(Guid sourceId, Guid gmId, Guid ownerId, Guid campaignId)
    {
        // Scoped to the calling GM's own registry (R0010: "ficha já cadastrada no Bestiário/NPCs
        // do GM") — "doesn't exist" and "exists but belongs to another GM" both fall through to
        // the same null/400, so neither case is distinguishable to the caller.
        var source = await db.NpcSheets.FirstOrDefaultAsync(s => s.Id == sourceId && s.GmId == gmId);
        if (source is null)
            return null;

        var copy = new NpcSheet
        {
            Id = Guid.NewGuid(), GmId = gmId, OwnerId = ownerId, ImageId = source.ImageId, Nome = source.Nome,
            Linhagem = source.Linhagem, Variante = source.Variante, Vocacao = source.Vocacao, SubVocacao = source.SubVocacao,
            Afinidade = source.Afinidade, Propriedade = source.Propriedade, Nivel = source.Nivel, Circulo = source.Circulo,
            Grau = source.Grau, PossuiCoracaoDeMana = source.PossuiCoracaoDeMana, AfinidadeAdicional = source.AfinidadeAdicional, ExperienciaAtual = source.ExperienciaAtual,
            EAPAtual = source.EAPAtual, NucleosRankF = source.NucleosRankF, NucleosRankE = source.NucleosRankE,
            NucleosRankD = source.NucleosRankD, NucleosRankC = source.NucleosRankC, NucleosRankB = source.NucleosRankB,
            NucleosRankA = source.NucleosRankA, NucleosRankS = source.NucleosRankS,
            PontosDeIgnicaoAtual = source.PontosDeIgnicaoAtual, PontosDeIgnicaoTotal = source.PontosDeIgnicaoTotal,
            VitalidadeAtual = source.VitalidadeAtual, FocoAtual = source.FocoAtual, AdrenalinaAtual = source.AdrenalinaAtual,
            EstresseAtual = source.EstresseAtual, Cobertura = source.Cobertura, Ciclos = source.Ciclos, Historia = source.Historia
        };

        // Deep-copy every child table — mechanical, one loop per table, same shape each time.
        foreach (var a in await db.NpcAttributes.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcAttributes.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Atributo = a.Atributo, Gasto = a.Gasto, Bonus = a.Bonus, TemMaestria = a.TemMaestria });
        foreach (var s in await db.NpcSkills.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcSkills.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, PericiaId = s.PericiaId, Gasto = s.Gasto });
        foreach (var a in await db.NpcAffinities.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcAffinities.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Elemento = a.Elemento, ElementoValor = a.ElementoValor, SubElemento = a.SubElemento, SubElementoValor = a.SubElementoValor, SegundaEssencia = a.SegundaEssencia, SegundaEssenciaValor = a.SegundaEssenciaValor, Experiencia = a.Experiencia });
        foreach (var r in await db.NpcRunes.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcRunes.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Nome = r.Nome, Descricao = r.Descricao, Grau = r.Grau, ImageId = r.ImageId });
        foreach (var m in await db.NpcMasteries.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcMasteries.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Nome = m.Nome, PericiaId = m.PericiaId, Atributo = m.Atributo, GastoMaestria = m.GastoMaestria });
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
            var efeitos = await db.NpcSpellAbilityEffects.Where(x => x.NpcSpellAbilityId == sa.Id).ToListAsync();
            var entradaDoBanco = await PublicarMagiaNaCampanhaAsync(campaignId, gmId, sa.SourceBankEntryId, sa.Nome, sa.Tipo, sa.Grau, sa.GastoEmPI, sa.Custo, sa.Descricao,
                efeitos.Select(e => (e.EfeitoNome, e.Quantidade, e.CustoPI)), deCriatura: false, sa.Categoria, sa.Requisitos);
            var saCopy = new NpcSpellAbility
            {
                Id = Guid.NewGuid(), NpcSheetId = copy.Id, SourceBankEntryId = entradaDoBanco, Nome = sa.Nome,
                Tipo = sa.Tipo, Grau = sa.Grau, GastoEmPI = sa.GastoEmPI, Custo = sa.Custo, Descricao = sa.Descricao,
                Categoria = sa.Categoria, Requisitos = sa.Requisitos
            };
            db.NpcSpellAbilities.Add(saCopy);
            foreach (var eff in efeitos)
                db.NpcSpellAbilityEffects.Add(new() { Id = Guid.NewGuid(), NpcSpellAbilityId = saCopy.Id, EfeitoNome = eff.EfeitoNome, Quantidade = eff.Quantidade, CustoPI = eff.CustoPI });
        }
        foreach (var aff in await db.NpcAffections.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcAffections.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Nome = aff.Nome, Favorabilidade = aff.Favorabilidade });
        foreach (var t in await db.NpcTraits.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcTraits.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, TraitId = t.TraitId, Polaridade = t.Polaridade });

        return copy;
    }

    private async Task<CreatureSheet?> DeepCopyCreatureAsync(Guid sourceId, Guid gmId, Guid ownerId, Guid campaignId)
    {
        // Scoped to the calling GM's own registry — same rationale as DeepCopyNpcAsync above.
        var source = await db.CreatureSheets.FirstOrDefaultAsync(s => s.Id == sourceId && s.GmId == gmId);
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
            db.CreatureSkills.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, PericiaId = s.PericiaId, Gasto = s.Gasto });
        foreach (var m in await db.CreatureMasteries.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureMasteries.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, Nome = m.Nome, PericiaId = m.PericiaId, Atributo = m.Atributo, GastoMaestria = m.GastoMaestria });
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
            var efeitos = await db.CreatureSpellAbilityEffects.Where(x => x.CreatureSpellAbilityId == sa.Id).ToListAsync();
            var entradaDoBanco = await PublicarMagiaNaCampanhaAsync(campaignId, gmId, sa.SourceBankEntryId, sa.Nome, sa.Tipo, sa.Grau, sa.GastoEmPI, sa.Custo, sa.Descricao,
                efeitos.Select(e => (e.EfeitoNome, e.Quantidade, e.CustoPI)), deCriatura: true, sa.Categoria, sa.Requisitos);
            var saCopy = new CreatureSpellAbility
            {
                Id = Guid.NewGuid(), CreatureSheetId = copy.Id, SourceBankEntryId = entradaDoBanco, Nome = sa.Nome,
                Tipo = sa.Tipo, Grau = sa.Grau, GastoEmPI = sa.GastoEmPI, Custo = sa.Custo, Descricao = sa.Descricao,
                Categoria = sa.Categoria, Requisitos = sa.Requisitos
            };
            db.CreatureSpellAbilities.Add(saCopy);
            foreach (var eff in efeitos)
                db.CreatureSpellAbilityEffects.Add(new() { Id = Guid.NewGuid(), CreatureSpellAbilityId = saCopy.Id, EfeitoNome = eff.EfeitoNome, Quantidade = eff.Quantidade, CustoPI = eff.CustoPI });
        }
        foreach (var aff in await db.CreatureAffections.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureAffections.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, Nome = aff.Nome, Favorabilidade = aff.Favorabilidade });
        foreach (var t in await db.CreatureTraits.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureTraits.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, TraitId = t.TraitId, CreatureExclusiveTraitId = t.CreatureExclusiveTraitId, Polaridade = t.Polaridade });

        return copy;
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
