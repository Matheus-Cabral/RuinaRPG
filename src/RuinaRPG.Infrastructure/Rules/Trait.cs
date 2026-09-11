using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Rules;

public class Trait
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Custo { get; set; }
    public Polaridade Polaridade { get; set; }
    public bool RequerEspecificacao { get; set; }

    // Set by TraitsController's Create/Update — once true, TraitSeeder never touches this row
    // again (see TraitSeeder.SeedAsync's updated doc comment), so a manual edit always wins over
    // whatever Características.md says.
    public bool IsCustomized { get; set; }

    // Soft delete: hidden from every read path (an explicit "!IsDeleted" filter per query, not an
    // EF global filter — see TraitsController), but the row itself stays so TraitSeeder still
    // recognizes it as "already present" and never resurrects it.
    public bool IsDeleted { get; set; }

    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
