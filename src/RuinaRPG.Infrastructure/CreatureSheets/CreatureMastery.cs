using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureMastery
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public required string Nome { get; set; }
    public Pericia Pericia { get; set; }
    public AtributoCriatura Atributo { get; set; }
    public int GastoMaestria { get; set; }
}
