using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcAffinity
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public Elemento Elemento { get; set; }
    public SubElemento SubElemento { get; set; }
    public required string CaminhoNome { get; set; }
    public int Experiencia { get; set; }
}
