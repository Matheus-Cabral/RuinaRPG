using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcArtifact : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Guid ArtifactItemId { get; set; }
}
