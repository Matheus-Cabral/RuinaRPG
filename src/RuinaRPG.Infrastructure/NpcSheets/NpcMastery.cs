using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcMastery
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public required string Nome { get; set; }
    public Pericia Pericia { get; set; }
    public Atributo Atributo { get; set; }
    public int GastoMaestria { get; set; }
}
