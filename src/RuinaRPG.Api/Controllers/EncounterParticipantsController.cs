using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/encounters/{encounterId}/participants")]
public class EncounterParticipantsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EncounterParticipantResponse>> Add(Guid encounterId, AddParticipantRequest request)
    {
        var (authError, encounter) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;

        var sourceCount = new[] { request.SourceCharacterSheetId, request.SourceNpcSheetId, request.SourceCreatureSheetId }.Count(id => id is not null);
        if (sourceCount != 1)
            return BadRequest("Informe exatamente uma origem: Ficha de Personagem, de NPC ou de Criatura.");

        var gmId = CurrentGmId();
        var participant = new EncounterParticipant { Id = Guid.NewGuid(), EncounterId = encounterId, Iniciativa = request.Iniciativa, AcoesRestantes = 3, Nome = "" };

        if (request.SourceCharacterSheetId is not null)
        {
            if (!Guid.TryParse(request.SourceCharacterSheetId, out var characterSheetId))
                return BadRequest("SourceCharacterSheetId inválido.");

            var sheet = await db.CharacterSheets.FindAsync(characterSheetId);
            // R0002: the CharacterSheet must belong to the encounter's own campaign. Wrong campaign is
            // treated identically to "doesn't exist" — no distinct message, same convention used elsewhere.
            if (sheet is null || sheet.CampaignId != encounter!.CampaignId) return BadRequest("Ficha de Personagem não encontrada.");
            participant.SourceCharacterSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            // PV/PF/PA stay null — always live-sourced for a CharacterSheet (owner's own, or a granted pet).
        }
        else if (request.SourceNpcSheetId is not null)
        {
            if (!Guid.TryParse(request.SourceNpcSheetId, out var npcSheetId))
                return BadRequest("SourceNpcSheetId inválido.");

            var sheet = await db.NpcSheets.FindAsync(npcSheetId);
            // R0002: the NpcSheet must belong to the calling GM.
            if (sheet is null || sheet.GmId != gmId) return BadRequest("Ficha de NPC não encontrada.");
            if (sheet.OwnerId is not null)
            {
                // Granted sheets are campaign-scoped via their grant-link CampaignAttachment row
                // (the same one CampaignGrantsController.Grant inserts) — a GM who owns several
                // campaigns must not be able to pull a companion granted in one of them into an
                // encounter in a different one, matching the CharacterSheet branch's own check above.
                var grantedToThisCampaign = await db.CampaignAttachments.AnyAsync(a => a.NpcSheetId == npcSheetId && a.CampaignId == encounter!.CampaignId);
                if (!grantedToThisCampaign) return BadRequest("Ficha de NPC não encontrada.");
            }
            participant.SourceNpcSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            if (sheet.OwnerId is null) // GM's own bestiary entry (R0002's 2nd case) — copy once, then independent.
            {
                participant.PVAtual = sheet.VitalidadeAtual;
                participant.PFAtual = sheet.FocoAtual;
                participant.PAAtual = sheet.AdrenalinaAtual;
            }
            // else: granted to a player — stays live-sourced, same as a CharacterSheet.
        }
        else
        {
            if (!Guid.TryParse(request.SourceCreatureSheetId, out var creatureSheetId))
                return BadRequest("SourceCreatureSheetId inválido.");

            var sheet = await db.CreatureSheets.FindAsync(creatureSheetId);
            // R0002: the CreatureSheet must belong to the calling GM.
            if (sheet is null || sheet.GmId != gmId) return BadRequest("Ficha de Criatura não encontrada.");
            if (sheet.OwnerId is not null)
            {
                // Same campaign-scoping rationale as the NpcSheet branch above.
                var grantedToThisCampaign = await db.CampaignAttachments.AnyAsync(a => a.CreatureSheetId == creatureSheetId && a.CampaignId == encounter!.CampaignId);
                if (!grantedToThisCampaign) return BadRequest("Ficha de Criatura não encontrada.");
            }
            participant.SourceCreatureSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            if (sheet.OwnerId is null)
            {
                participant.PVAtual = sheet.VitalidadeAtual;
                participant.PFAtual = sheet.FocoAtual;
                participant.PAAtual = sheet.AdrenalinaAtual;
            }
        }

        db.EncounterParticipants.Add(participant);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(participant, new List<string>()));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid encounterId, Guid id, UpdateParticipantRequest request)
    {
        var (authError, _) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;

        var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == id && p.EncounterId == encounterId);
        if (participant is null)
            return NotFound();

        if (request.AcoesRestantes is < 0 or > 3)
            return BadRequest("AcoesRestantes deve estar entre 0 e 3.");

        var isLive = participant.SourceCharacterSheetId is not null
            || (participant.SourceNpcSheetId is not null && (await db.NpcSheets.FindAsync(participant.SourceNpcSheetId.Value))?.OwnerId is not null)
            || (participant.SourceCreatureSheetId is not null && (await db.CreatureSheets.FindAsync(participant.SourceCreatureSheetId.Value))?.OwnerId is not null);
        if (isLive && (request.PV is not null || request.PF is not null || request.PA is not null))
            return BadRequest("PV/PF/PA de um participante vindo de Ficha de Personagem são somente leitura aqui — edite a ficha diretamente.");

        participant.Iniciativa = request.Iniciativa;
        participant.AcoesRestantes = request.AcoesRestantes;
        if (!isLive)
        {
            participant.PVAtual = request.PV;
            participant.PFAtual = request.PF;
            participant.PAAtual = request.PA;
        }

        var existingConditions = await db.EncounterParticipantConditions.Where(c => c.EncounterParticipantId == id).ToListAsync();
        db.EncounterParticipantConditions.RemoveRange(existingConditions);
        foreach (var texto in request.Condicoes)
            db.EncounterParticipantConditions.Add(new EncounterParticipantCondition { Id = Guid.NewGuid(), EncounterParticipantId = id, Texto = texto });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<EncounterParticipantResponse>>> List(Guid encounterId)
    {
        var (authError, _) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;

        var participants = await db.EncounterParticipants
            .Where(p => p.EncounterId == encounterId)
            .OrderByDescending(p => p.Iniciativa).ThenBy(p => p.Id)
            .ToListAsync();

        var participantIds = participants.Select(p => p.Id).ToList();
        var conditionsByParticipantId = (await db.EncounterParticipantConditions
                .Where(c => participantIds.Contains(c.EncounterParticipantId))
                .ToListAsync())
            .GroupBy(c => c.EncounterParticipantId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Texto).ToList());

        var responses = new List<EncounterParticipantResponse>();
        foreach (var participant in participants)
            responses.Add(await ToResponseAsync(participant, conditionsByParticipantId.GetValueOrDefault(participant.Id, new List<string>())));
        return responses;
    }

    private async Task<EncounterParticipantResponse> ToResponseAsync(EncounterParticipant p, List<string> condicoes)
    {
        if (p.SourceCharacterSheetId is not null)
        {
            var sheet = await db.CharacterSheets.FindAsync(p.SourceCharacterSheetId.Value);
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa, sheet?.VitalidadeAtual, sheet?.FocoAtual, sheet?.AdrenalinaAtual, p.AcoesRestantes, IsLiveSourced: true, condicoes);
        }
        if (p.SourceNpcSheetId is not null)
        {
            var sheet = await db.NpcSheets.FindAsync(p.SourceNpcSheetId.Value);
            var isLive = sheet?.OwnerId is not null;
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa,
                isLive ? sheet?.VitalidadeAtual : p.PVAtual, isLive ? sheet?.FocoAtual : p.PFAtual, isLive ? sheet?.AdrenalinaAtual : p.PAAtual,
                p.AcoesRestantes, isLive, condicoes);
        }
        if (p.SourceCreatureSheetId is not null)
        {
            var sheet = await db.CreatureSheets.FindAsync(p.SourceCreatureSheetId.Value);
            var isLive = sheet?.OwnerId is not null;
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa,
                isLive ? sheet?.VitalidadeAtual : p.PVAtual, isLive ? sheet?.FocoAtual : p.PFAtual, isLive ? sheet?.AdrenalinaAtual : p.PAAtual,
                p.AcoesRestantes, isLive, condicoes);
        }
        // Source sheet was deleted (SetNull cascaded) — Nome snapshot still displays, PV/PF/PA frozen at whatever the columns last held.
        return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa, p.PVAtual, p.PFAtual, p.PAAtual, p.AcoesRestantes, IsLiveSourced: false, condicoes);
    }

    private async Task<(ActionResult? Error, Encounter? Encounter)> CheckEncounterOwnershipAsync(Guid encounterId)
    {
        var encounter = await db.Encounters.FindAsync(encounterId);
        if (encounter is null)
            return (NotFound(), null);

        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == CurrentGmId());
        return isOwner ? (null, encounter) : (NotFound(), null);
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
