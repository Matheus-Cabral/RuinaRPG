using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Services;

/// <summary>Monta as linhas do log do encontro; só grava com o encontro em andamento. Quem chama salva.</summary>
public class EncounterLog(RuinaRpgDbContext db)
{
    private int _ordem;

    /// <summary>No-op unless encounter.Status == EmAndamento. Snapshots Turno and whose turn it is from the encounter as it is NOW.</summary>
    public async Task RegistrarAsync(Encounter encounter, EncounterLogTipo tipo, string? participanteNome = null,
        string? campo = null, string? valorAntes = null, string? valorDepois = null, string? texto = null)
    {
        if (encounter.Status != EncounterStatus.EmAndamento)
            return;

        db.EncounterLogEntries.Add(new EncounterLogEntry
        {
            Id = ProximoId(),
            EncounterId = encounter.Id,
            OccurredAt = DateTime.UtcNow,
            Turno = encounter.CurrentRound,
            VezParticipantId = encounter.CurrentParticipantId,
            VezNome = await NomeDaVezAsync(encounter.CurrentParticipantId),
            Tipo = tipo,
            ParticipanteNome = participanteNome,
            Campo = campo,
            ValorAntes = valorAntes,
            ValorDepois = valorDepois,
            Texto = texto,
        });
    }

    // EF sorts the inserts of one table by primary key inside a save, and Sequencia is assigned in insert
    // order. A random Guid would scramble the rows of the same save, so the leading Guid field carries a
    // counter (Guid compares its first field first) and the rest stays random.
    private Guid ProximoId()
    {
        var aleatorio = Guid.NewGuid().ToByteArray();
        BitConverter.GetBytes(++_ordem).CopyTo(aleatorio, 0);
        return new Guid(aleatorio);
    }

    // The participant may have unsaved changes (tracked) or not be loaded at all.
    private async Task<string?> NomeDaVezAsync(Guid? participantId)
    {
        if (participantId is not { } id)
            return null;
        var tracked = db.EncounterParticipants.Local.FirstOrDefault(p => p.Id == id);
        if (tracked is not null)
            return tracked.Nome;
        return await db.EncounterParticipants.Where(p => p.Id == id).Select(p => p.Nome).FirstOrDefaultAsync();
    }
}
