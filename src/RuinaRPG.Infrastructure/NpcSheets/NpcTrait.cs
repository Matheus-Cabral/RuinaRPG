using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcTrait
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public Guid TraitId { get; set; }
    public Polaridade Polaridade { get; set; }
}
