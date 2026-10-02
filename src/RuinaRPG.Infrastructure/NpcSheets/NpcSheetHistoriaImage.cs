namespace RuinaRPG.Infrastructure.NpcSheets;

/// <summary>Aba História: uma imagem da galeria da Ficha de NPC. Ordem = posição na galeria (0, 1, 2…).</summary>
public class NpcSheetHistoriaImage
{
    public Guid NpcSheetId { get; set; }
    public Guid ImageId { get; set; }
    public int Ordem { get; set; }
}
