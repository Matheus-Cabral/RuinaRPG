using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterArtifact : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Guid ArtifactItemId { get; set; }
}
