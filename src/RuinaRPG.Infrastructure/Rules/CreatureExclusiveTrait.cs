using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// Same shape as Trait, but a genuinely separate table — only a Ficha de Criatura can pick from
/// this catalog (see CreaturePossessionsController.AddTrait). No IsCustomized: unlike Trait,
/// nothing seeds this table from a markdown document, every row is created manually by the Rules
/// Auditor, so there is nothing for a future seeder to "not overwrite".
/// </summary>
public class CreatureExclusiveTrait
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Custo { get; set; }
    public Polaridade Polaridade { get; set; }
    public bool RequerEspecificacao { get; set; }

    // Soft delete, same reasoning as Trait.IsDeleted: a CreatureTrait row may already reference
    // this catalog entry — hard-deleting it would break that read. Filtered explicitly per query
    // (no EF global filter), matching Trait's own convention.
    public bool IsDeleted { get; set; }

    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
