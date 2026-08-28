using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureAttribute
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public AtributoCriatura Atributo { get; set; }
    public int Gasto { get; set; }
    public int Bonus { get; set; }
    public bool TemMaestria { get; set; }
}
