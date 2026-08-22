using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSkill
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Pericia Pericia { get; set; }
    public int Gasto { get; set; }
}
