using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSpellAbility
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid? SourceBankEntryId { get; set; }
    public required string Nome { get; set; }
    public SpellAbilityTipo Tipo { get; set; }
    public int Grau { get; set; }
    public int GastoEmPI { get; set; }
    public int Custo { get; set; }
    public required string Descricao { get; set; }
    public List<CharacterSpellAbilityEffect> Efeitos { get; set; } = [];
}
