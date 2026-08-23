using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcAttribute
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public Atributo Atributo { get; set; }
    public int Gasto { get; set; }
    public int Bonus { get; set; }
    public bool TemMaestria { get; set; }
}
