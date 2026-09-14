using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureTrait
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }

    // Exactly one of these two is set per row — which catalog this grant's characteristic came
    // from. Two nullable FKs instead of one non-nullable column, because a single column can't
    // carry a real foreign key to two different tables at once (the same pattern already used by
    // EncounterParticipant.SourceCharacterSheetId/SourceNpcSheetId/SourceCreatureSheetId for three
    // possible sources).
    public Guid? TraitId { get; set; }
    public Guid? CreatureExclusiveTraitId { get; set; }

    public Polaridade Polaridade { get; set; }
    public string? Especificacao { get; set; }
}
