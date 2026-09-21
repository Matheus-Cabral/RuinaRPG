namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcRune
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Grau { get; set; }

    /// <summary>Imagem opcional (uma por Runa). FK para Images com SetNull.</summary>
    public Guid? ImageId { get; set; }

    /// <summary>Só rastreio de origem (sem FK): apagar a entrada do banco não afeta a Runa da ficha.</summary>
    public Guid? SourceBankEntryId { get; set; }
}
