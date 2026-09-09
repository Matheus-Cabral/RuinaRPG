using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class RacialAbilityOverride
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public Variante Variante { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
}
