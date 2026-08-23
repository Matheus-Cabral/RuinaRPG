using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.SpellsAndAbilities;

public class SpellAbilityBankEntry
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public SpellAbilityTipo Tipo { get; set; }
    public int Grau { get; set; }
    public int GastoEmPI { get; set; }
    public int Custo { get; set; }
    public required string Descricao { get; set; }
    public List<SpellAbilityBankEffect> Efeitos { get; set; } = [];
}
