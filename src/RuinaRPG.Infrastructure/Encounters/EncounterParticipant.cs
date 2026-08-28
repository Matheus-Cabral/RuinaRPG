namespace RuinaRPG.Infrastructure.Encounters;

public class EncounterParticipant
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public Guid? SourceCharacterSheetId { get; set; }
    public Guid? SourceNpcSheetId { get; set; }
    public Guid? SourceCreatureSheetId { get; set; }
    public required string Nome { get; set; }
    public int Iniciativa { get; set; }
    public int? PVAtual { get; set; }
    public int? PFAtual { get; set; }
    public int? PAAtual { get; set; }
    public int AcoesRestantes { get; set; }
}
