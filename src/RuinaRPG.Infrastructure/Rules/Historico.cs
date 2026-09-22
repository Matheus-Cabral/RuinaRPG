using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.Rules;

public class Historico
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public Pericia PericiaMaisSeis { get; set; }
    public Pericia PericiaMaisTres { get; set; }

    // Set by HistoricosController's Create/Update — once true, HistoricoSeeder never touches this
    // row again, so a manual edit always wins over whatever Historico.md says. Mirrors Trait.
    public bool IsCustomized { get; set; }

    // Soft delete: hidden from every read path (an explicit "!IsDeleted" filter per query), but the
    // row itself stays so HistoricoSeeder still recognizes it as "already present" and never
    // resurrects it. Mirrors Trait.
    public bool IsDeleted { get; set; }

    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
