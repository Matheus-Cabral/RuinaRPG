using RuinaRPG.Infrastructure.Fichas;

namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>Aba História: uma imagem da galeria da Ficha de Personagem. Ordem = posição na galeria (0, 1, 2…).</summary>
public class CharacterSheetHistoriaImage : IImagemDaHistoria
{
    public Guid SheetId { get; set; }
    public Guid ImageId { get; set; }
    public int Ordem { get; set; }
}
