using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureTrait
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public Guid TraitId { get; set; }
    public Polaridade Polaridade { get; set; }
    public string? Especificacao { get; set; }
}
