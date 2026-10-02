namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>Aba História: uma imagem da galeria da Ficha de Personagem. Ordem = posição na galeria (0, 1, 2…).</summary>
public class CharacterSheetHistoriaImage
{
    public Guid CharacterSheetId { get; set; }
    public Guid ImageId { get; set; }
    public int Ordem { get; set; }
}
