using RuinaRPG.Domain.Encounters;

namespace RuinaRPG.Infrastructure.Encounters;

/// <summary>Uma linha do log do encontro (Gerenciador de Encontros R0010). Gravada só com o encontro em andamento.</summary>
public class EncounterLogEntry
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public long Sequencia { get; set; }        // identity column: stable order inside one instant
    public DateTime OccurredAt { get; set; }
    public int Turno { get; set; }
    public Guid? VezParticipantId { get; set; } // no FK: the participant may be removed later
    public string? VezNome { get; set; }
    public EncounterLogTipo Tipo { get; set; }
    public string? ParticipanteNome { get; set; }
    public string? Campo { get; set; }
    public string? ValorAntes { get; set; }
    public string? ValorDepois { get; set; }
    public string? Texto { get; set; }
}
