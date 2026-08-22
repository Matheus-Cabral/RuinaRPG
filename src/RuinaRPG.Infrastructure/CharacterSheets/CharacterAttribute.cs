using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterAttribute
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Atributo Atributo { get; set; }
    public int Gasto { get; set; }
    public int Bonus { get; set; }
    public bool TemMaestria { get; set; }
}
