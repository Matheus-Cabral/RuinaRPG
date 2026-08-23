using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSkill
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public Pericia Pericia { get; set; }
    public int Gasto { get; set; }
}
