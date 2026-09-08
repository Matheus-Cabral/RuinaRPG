using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterTrait
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid TraitId { get; set; }
    public Polaridade Polaridade { get; set; }
    public string? Especificacao { get; set; }
}
