using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureArtifact : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Guid ArtifactItemId { get; set; }
}
