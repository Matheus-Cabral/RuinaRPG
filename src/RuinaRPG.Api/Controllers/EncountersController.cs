using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/campaigns/{campaignId}/encounters")]
public class EncountersController(RuinaRpgDbContext db, EncounterLog log) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EncounterResponse>> Create(Guid campaignId, CreateEncounterRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var encounter = new Encounter { Id = Guid.NewGuid(), CampaignId = campaignId, Nome = request.Nome };
        db.Encounters.Add(encounter);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(encounter));
    }

    [HttpGet]
    public async Task<ActionResult<List<EncounterResponse>>> List(Guid campaignId)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var encounters = await db.Encounters.Where(e => e.CampaignId == campaignId).ToListAsync();
        var responses = new List<EncounterResponse>();
        foreach (var encounter in encounters)
            responses.Add(await ToResponseAsync(encounter));
        return responses;
    }

    [HttpPut("~/api/encounters/{encounterId}")]
    public async Task<IActionResult> Update(Guid encounterId, UpdateEncounterRequest request)
    {
        var gmId = CurrentGmId();
        var encounter = await db.Encounters.FindAsync(encounterId);
        if (encounter is null)
            return NotFound();
        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == gmId);
        if (!isOwner)
            return NotFound();

        encounter.Nome = request.Nome;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/start")]
    public async Task<IActionResult> Start(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.Preparacao) is { } wrongPhase) return wrongPhase;

        var participants = await OrderedParticipantsAsync(encounterId);
        if (participants.Count == 0)
            return BadRequest("Adicione ao menos um participante antes de iniciar o encontro.");

        var (round, participantId) = TurnAdvanceCalculator.Start(participants.Select(p => p.Id).ToList());
        encounter!.CurrentRound = round;
        encounter.CurrentParticipantId = participantId;
        encounter.Status = EncounterStatus.EmAndamento;
        participants.Single(p => p.Id == participantId).AcoesRestantes = 3;

        await log.RegistrarAsync(encounter, EncounterLogTipo.Iniciado);
        await log.RegistrarAsync(encounter, EncounterLogTipo.VezPassada, NomeDe(participants, participantId));

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/advance-turn")]
    public async Task<IActionResult> AdvanceTurn(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.EmAndamento) is { } wrongPhase) return wrongPhase;

        var participants = await OrderedParticipantsAsync(encounterId);
        if (participants.Count == 0)
            return BadRequest("Adicione ao menos um participante antes de avançar o turno.");

        var orderedIds = participants.Select(p => p.Id).ToList();
        // Nobody on turn (the only participant was removed): the next is the first of the SAME Turno.
        // Advance would read a null vez as "the first already has it" and skip to the second.
        var (nextRound, nextParticipantId) = encounter!.CurrentParticipantId is null
            ? (encounter.CurrentRound, orderedIds[0])
            : TurnAdvanceCalculator.Advance(encounter.CurrentRound, encounter.CurrentParticipantId, orderedIds);
        var wrapped = nextRound > encounter.CurrentRound;
        encounter.CurrentRound = nextRound;
        encounter.CurrentParticipantId = nextParticipantId;

        participants.Single(p => p.Id == nextParticipantId).AcoesRestantes = 3;

        if (wrapped)
            await log.RegistrarAsync(encounter, EncounterLogTipo.NovoTurno);
        await log.RegistrarAsync(encounter, EncounterLogTipo.VezPassada, NomeDe(participants, nextParticipantId));

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/previous-turn")]
    public async Task<IActionResult> PreviousTurn(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.EmAndamento) is { } wrongPhase) return wrongPhase;

        var participants = await OrderedParticipantsAsync(encounterId);
        var previous = TurnAdvanceCalculator.Previous(encounter!.CurrentRound, encounter.CurrentParticipantId, participants.Select(p => p.Id).ToList());
        if (previous is null)
            return BadRequest("Não há vez anterior.");

        encounter.CurrentRound = previous.Value.Round;
        encounter.CurrentParticipantId = previous.Value.ParticipantId;

        await log.RegistrarAsync(encounter, EncounterLogTipo.VezVoltou, NomeDe(participants, previous.Value.ParticipantId));

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/turn/{participantId}")]
    public async Task<IActionResult> ChooseTurn(Guid encounterId, Guid participantId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.EmAndamento) is { } wrongPhase) return wrongPhase;

        var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == participantId && p.EncounterId == encounterId);
        if (participant is null)
            return NotFound();

        encounter!.CurrentParticipantId = participant.Id;
        participant.AcoesRestantes = 3;

        await log.RegistrarAsync(encounter, EncounterLogTipo.VezEscolhida, participant.Nome);

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/finish")]
    public async Task<IActionResult> Finish(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.EmAndamento) is { } wrongPhase) return wrongPhase;

        await log.RegistrarAsync(encounter!, EncounterLogTipo.Finalizado); // before the status leaves EmAndamento
        encounter!.Status = EncounterStatus.Finalizado;
        encounter.FinalizadoEm = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/reopen")]
    public async Task<IActionResult> Reopen(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (encounter!.Status != EncounterStatus.Finalizado)
            return Conflict("O encontro não está finalizado.");

        encounter.Status = EncounterStatus.EmAndamento;
        encounter.FinalizadoEm = null;

        await log.RegistrarAsync(encounter, EncounterLogTipo.Reaberto);

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("~/api/encounters/{encounterId}/log")]
    public async Task<ActionResult<List<EncounterLogEntryResponse>>> GetLog(Guid encounterId)
    {
        var (error, _) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;

        var entries = await db.EncounterLogEntries
            .Where(e => e.EncounterId == encounterId)
            .OrderBy(e => e.Sequencia)
            .ToListAsync();
        return entries.Select(e => new EncounterLogEntryResponse(e.Sequencia, DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc), e.Turno,
            e.VezNome, e.Tipo.ToString(), e.ParticipanteNome, e.Campo, e.ValorAntes, e.ValorDepois, e.Texto)).ToList();
    }

    private static string? NomeDe(List<EncounterParticipant> participants, Guid id) => participants.Single(p => p.Id == id).Nome;

    private async Task<(ActionResult? Error, Encounter? Encounter)> LoadOwnedAsync(Guid encounterId)
    {
        var encounter = await db.Encounters.FindAsync(encounterId);
        if (encounter is null)
            return (NotFound(), null);
        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == CurrentGmId());
        return isOwner ? (null, encounter) : (NotFound(), null);
    }

    private ActionResult? RequirePhase(Encounter encounter, params EncounterStatus[] allowed)
    {
        if (allowed.Contains(encounter.Status))
            return null;
        return encounter.Status switch
        {
            EncounterStatus.Finalizado => Conflict("Encontro finalizado."),
            EncounterStatus.EmAndamento => Conflict("O encontro já foi iniciado."),
            _ => Conflict("O encontro não está em andamento."),
        };
    }

    private Task<List<EncounterParticipant>> OrderedParticipantsAsync(Guid encounterId) =>
        db.EncounterParticipants
            .Where(p => p.EncounterId == encounterId)
            .OrderByDescending(p => p.Iniciativa).ThenBy(p => p.Id)
            .ToListAsync();

    private async Task<EncounterResponse> ToResponseAsync(Encounter e)
    {
        var orderedIds = await db.EncounterParticipants
            .Where(p => p.EncounterId == e.Id)
            .OrderByDescending(p => p.Iniciativa).ThenBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync();
        var index = TurnAdvanceCalculator.IndexOf(e.CurrentParticipantId, orderedIds);
        return new EncounterResponse(e.Id.ToString(), e.Nome, e.CurrentRound, index,
            e.Status.ToString(),
            e.FinalizadoEm is { } fim ? DateTime.SpecifyKind(fim, DateTimeKind.Utc) : null,
            e.CurrentParticipantId?.ToString());
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
