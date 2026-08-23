using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcSkill
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public Pericia Pericia { get; set; }
    public int Gasto { get; set; }
}
