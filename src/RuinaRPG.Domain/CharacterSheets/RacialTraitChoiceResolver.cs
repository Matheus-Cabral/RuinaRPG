namespace RuinaRPG.Domain.CharacterSheets;

public sealed record RacialTraitResolutionResult(List<RacialTraitOption>? Grants, string? Error)
{
    public static RacialTraitResolutionResult Ok(List<RacialTraitOption> grants) => new(grants, null);
    public static RacialTraitResolutionResult Fail(string error) => new(null, error);
}

/// <summary>
/// Validates and resolves a player's/GM's racial-characteristic pick against a Variante's
/// RacialTraitSlots (default from RacialTraitLookup or GM-overridden) — pure, no I/O, so both
/// CharacterPossessionsController and NpcPossessionsController can share it.
/// </summary>
public static class RacialTraitChoiceResolver
{
    /// <summary>
    /// How many racial CharacterTrait/NpcTrait rows a fully-resolved Variante should have. A slot
    /// with no options grants nothing (Sinir/Laonir have no Obrigatória; a Variante the GM hasn't
    /// configured yet, like AloraSolar, may have no Gratuita either) — otherwise the sheet would
    /// wait forever on a choice that has nothing to pick from.
    /// </summary>
    public static int ExpectedGrantCount(RacialTraitSlots slots) =>
        (slots.Gratuita.Count > 0 ? 1 : 0) + (slots.Obrigatoria.Count > 0 ? 1 : 0);

    public static bool IsResolved(RacialTraitSlots slots, int existingCount) => existingCount >= ExpectedGrantCount(slots);

    public static RacialTraitResolutionResult Resolve(RacialTraitSlots slots, string gratuitaTraitNome, string? obrigatoriaTraitNome)
    {
        var grants = new List<RacialTraitOption>();

        if (slots.Gratuita.Count > 0)
        {
            var gratuita = slots.Gratuita.FirstOrDefault(o => o.TraitNome == gratuitaTraitNome);
            if (gratuita is null)
                return RacialTraitResolutionResult.Fail("A Característica Gratuita escolhida não é uma opção válida para esta Variante.");
            grants.Add(gratuita);
        }

        switch (slots.Obrigatoria.Count)
        {
            case 0:
                // No Obrigatória slot at all (Sinir/Laonir) — nothing to grant, any input is ignored.
                break;
            case 1:
                // No real choice (Alóra) — always grant the single fixed option regardless of input.
                grants.Add(slots.Obrigatoria[0]);
                break;
            default:
                var obrigatoria = obrigatoriaTraitNome is null ? null : slots.Obrigatoria.FirstOrDefault(o => o.TraitNome == obrigatoriaTraitNome);
                if (obrigatoria is null)
                    return RacialTraitResolutionResult.Fail("A Característica Obrigatória escolhida não é uma opção válida para esta Variante.");
                grants.Add(obrigatoria);
                break;
        }

        return RacialTraitResolutionResult.Ok(grants);
    }
}
