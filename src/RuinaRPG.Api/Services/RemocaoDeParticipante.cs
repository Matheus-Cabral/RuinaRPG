using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Gerenciador de Encontros R0008: tira um participante do encontro — linha no log, passagem da vez se ele a tinha,
/// as Condições dele saem e a ficha dele volta aos totais sem elas. Único caminho de remoção: o GM removendo um
/// participante e a saída do jogador da campanha. Quem chama salva e, depois, chama
/// <see cref="CondicoesDoEncontro.NotificarAsync"/>. Várias remoções antes do mesmo salvamento funcionam: um participante
/// já removido nesta requisição não conta mais na ordem.
/// </summary>
public class RemocaoDeParticipante(RuinaRpgDbContext db, EncounterLog log, EncounterTurnos turnos, CondicoesDoEncontro condicoesDoEncontro)
{
    public async Task RemoverAsync(Encounter encounter, Guid participantId)
    {
        await db.EncounterParticipants.Where(p => p.EncounterId == encounter.Id).LoadAsync();
        var participants = db.ChangeTracker.Entries<EncounterParticipant>()
            .Where(e => e.Entity.EncounterId == encounter.Id && e.State is not (EntityState.Deleted or EntityState.Detached))
            .Select(e => e.Entity)
            .OrderByDescending(p => p.Iniciativa).ThenBy(p => p.Id)
            .ToList();
        var participant = participants.Single(p => p.Id == participantId);

        // Registered first, while the removed participant still has the turn: "X saiu" belongs under X's own vez and Turno.
        await log.RegistrarAsync(encounter, EncounterLogTipo.ParticipanteRemovido, participant.Nome);

        MudancaDeVez? mudanca = null;
        if (encounter.CurrentParticipantId == participantId)
        {
            // Order passed is the one BEFORE the removal.
            var next = TurnAdvanceCalculator.AfterRemoval(encounter.CurrentRound, participantId, participants.Select(p => p.Id).ToList());
            var receiver = next is { } handover ? participants.Single(p => p.Id == handover.ParticipantId) : null;
            mudanca = await turnos.DarAVezAsync(encounter, receiver, next?.Round ?? encounter.CurrentRound, MotivoDaVez.Remocao);
        }

        var conditions = await db.EncounterParticipantConditions.Where(c => c.EncounterParticipantId == participantId).ToListAsync();
        db.EncounterParticipantConditions.RemoveRange(conditions);
        // As Condições do catálogo saem com ele. Apagadas aqui (e não só pela cascata) para o log da ficha registrar cada uma.
        db.EncounterParticipantCondicoes.RemoveRange(await db.EncounterParticipantCondicoes.Where(c => c.EncounterParticipantId == participantId).ToListAsync());
        db.EncounterParticipants.Remove(participant);
        // Quem recebeu a vez: expiração (se entrou num Turno novo) e linhas "a cada vez" dele. O removido já não conta.
        if (mudanca is not null)
            await condicoesDoEncontro.AoMudarAVezAsync(encounter, mudanca);
        // A ficha do removido volta aos totais sem as Condições dele.
        await condicoesDoEncontro.AoMudarAtivacaoAsync([participantId]);
    }
}
