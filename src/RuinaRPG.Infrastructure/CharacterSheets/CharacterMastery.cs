using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterMastery
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public required string Nome { get; set; }
    public Pericia Pericia { get; set; }
    public Atributo Atributo { get; set; }
    public int GastoMaestria { get; set; }
}
