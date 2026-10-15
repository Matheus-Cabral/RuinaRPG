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
public class EncountersController(RuinaRpgDbContext db, EncounterLog log, EncounterTurnos turnos, CondicoesDoEncontro condicoesDoEncontro) : ControllerBase
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
    [ConflitoDeConcorrenciaDoEncontro]
    public async Task<IActionResult> Start(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.Preparacao) is { } wrongPhase) return wrongPhase;

        var participants = await OrderedParticipantsAsync(encounterId);
        if (participants.Count == 0)
            return BadRequest("Adicione ao menos um participante antes de iniciar o encontro.");

        var (round, participantId) = TurnAdvanceCalculator.Start(participants.Select(p => p.Id).ToList());
        encounter!.Status = EncounterStatus.EmAndamento;
        var mudanca = await turnos.DarAVezAsync(encounter, participants.Single(p => p.Id == participantId), round, MotivoDaVez.Inicio);
        await condicoesDoEncontro.AoMudarAtivacaoAsync(participants.Select(p => p.Id));
        // Depois do corte da ativação: as linhas "ao aplicar" das Condições da preparação e as "a cada vez" do primeiro.
        await condicoesDoEncontro.AoMudarAVezAsync(encounter, mudanca);

        await db.SaveChangesAsync();
        await condicoesDoEncontro.NotificarAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/advance-turn")]
    [ConflitoDeConcorrenciaDoEncontro]
    public async Task<IActionResult> AdvanceTurn(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.EmAndamento) is { } wrongPhase) return wrongPhase;

        var participants = await OrderedParticipantsAsync(encounterId);
        if (participants.Count == 0)
            return BadRequest("Adicione ao menos um participante antes de passar a vez.");

        var orderedIds = participants.Select(p => p.Id).ToList();
        // Nobody on turn (the only participant was removed): the next is the first of the SAME Turno.
        // Advance would read a null vez as "the first already has it" and skip to the second.
        var (nextRound, nextParticipantId) = encounter!.CurrentParticipantId is null
            ? (encounter.CurrentRound, orderedIds[0])
            : TurnAdvanceCalculator.Advance(encounter.CurrentRound, encounter.CurrentParticipantId, orderedIds);
        var mudanca = await turnos.DarAVezAsync(encounter, participants.Single(p => p.Id == nextParticipantId), nextRound, MotivoDaVez.Proxima);
        await condicoesDoEncontro.AoMudarAVezAsync(encounter, mudanca);

        await db.SaveChangesAsync();
        await condicoesDoEncontro.NotificarAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/previous-turn")]
    [ConflitoDeConcorrenciaDoEncontro]
    public async Task<IActionResult> PreviousTurn(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.EmAndamento) is { } wrongPhase) return wrongPhase;

        var participants = await OrderedParticipantsAsync(encounterId);
        var previous = TurnAdvanceCalculator.Previous(encounter!.CurrentRound, encounter.CurrentParticipantId, participants.Select(p => p.Id).ToList());
        if (previous is null)
            return BadRequest("Não há vez anterior.");

        // Vez anterior não roda nem desfaz ajuste de Condição, e não traz de volta uma que expirou: sem gancho.
        await turnos.DarAVezAsync(encounter, participants.Single(p => p.Id == previous.Value.ParticipantId), previous.Value.Round, MotivoDaVez.Anterior);

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/turn/{participantId}")]
    [ConflitoDeConcorrenciaDoEncontro]
    public async Task<IActionResult> ChooseTurn(Guid encounterId, Guid participantId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.EmAndamento) is { } wrongPhase) return wrongPhase;

        var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == participantId && p.EncounterId == encounterId);
        if (participant is null)
            return NotFound();

        var mudanca = await turnos.DarAVezAsync(encounter!, participant, encounter!.CurrentRound, MotivoDaVez.Escolhida);
        await condicoesDoEncontro.AoMudarAVezAsync(encounter, mudanca);

        await db.SaveChangesAsync();
        await condicoesDoEncontro.NotificarAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/finish")]
    [ConflitoDeConcorrenciaDoEncontro]
    public async Task<IActionResult> Finish(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (RequirePhase(encounter!, EncounterStatus.EmAndamento) is { } wrongPhase) return wrongPhase;

        await log.RegistrarAsync(encounter!, EncounterLogTipo.Finalizado); // before the status leaves EmAndamento
        encounter!.Status = EncounterStatus.Finalizado;
        encounter.FinalizadoEm = DateTime.UtcNow;
        await condicoesDoEncontro.AoMudarAtivacaoAsync(await ParticipantIdsAsync(encounterId));

        await db.SaveChangesAsync();
        await condicoesDoEncontro.NotificarAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/reopen")]
    [ConflitoDeConcorrenciaDoEncontro]
    public async Task<IActionResult> Reopen(Guid encounterId)
    {
        var (error, encounter) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;
        if (encounter!.Status != EncounterStatus.Finalizado)
            return Conflict("O encontro não está finalizado.");

        encounter.Status = EncounterStatus.EmAndamento;
        encounter.FinalizadoEm = null;

        await log.RegistrarAsync(encounter, EncounterLogTipo.Reaberto);
        await condicoesDoEncontro.AoMudarAtivacaoAsync(await ParticipantIdsAsync(encounterId));

        await db.SaveChangesAsync();
        await condicoesDoEncontro.NotificarAsync();
        return NoContent();
    }

    [HttpGet("~/api/encounters/{encounterId}/log")]
    public async Task<ActionResult<List<EncounterLogEntryResponse>>> GetLog(Guid encounterId)
    {
        var (error, _) = await LoadOwnedAsync(encounterId);
        if (error is not null) return error;

        var entries = await db.EncounterLogEntries
            .Where(e => e.EncounterId == encounterId)
            .OrderBy(e => e.Ordem).ThenBy(e => e.Sequencia)
            .ToListAsync();
        return entries.Select(e => new EncounterLogEntryResponse(e.Sequencia, DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc), e.Turno,
            e.VezNome, e.Tipo.ToString(), e.ParticipanteNome, e.Campo, e.ValorAntes, e.ValorDepois, e.Texto, e.VezParticipantId?.ToString())).ToList();
    }

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

    private Task<List<Guid>> ParticipantIdsAsync(Guid encounterId) =>
        db.EncounterParticipants.Where(p => p.EncounterId == encounterId).Select(p => p.Id).ToListAsync();

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
