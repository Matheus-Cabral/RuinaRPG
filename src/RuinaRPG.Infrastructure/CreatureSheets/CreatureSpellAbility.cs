using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSpellAbility
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public Guid? SourceBankEntryId { get; set; }
    public required string Nome { get; set; }
    public SpellAbilityTipo Tipo { get; set; }
    public int Grau { get; set; }
    public int GastoEmPI { get; set; }
    public int Custo { get; set; }
    public required string Descricao { get; set; }
    public List<CreatureSpellAbilityEffect> Efeitos { get; set; } = [];
}
