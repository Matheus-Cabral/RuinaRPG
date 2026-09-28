using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcSpellAbility
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public Guid? SourceBankEntryId { get; set; }
    public required string Nome { get; set; }
    public SpellAbilityTipo Tipo { get; set; }
    public int Grau { get; set; }
    public int GastoEmPI { get; set; }
    public int Custo { get; set; }
    public required string Descricao { get; set; }
    public List<NpcSpellAbilityEffect> Efeitos { get; set; } = [];

    /// <summary>Só em Passivas (Tipo = Passiva); nulo nos demais tipos.</summary>
    public CategoriaDePassiva? Categoria { get; set; }

    /// <summary>Só em Passivas; gravado como jsonb. Nulo = sem requisitos.</summary>
    public RequisitosDePassiva? Requisitos { get; set; }
}
