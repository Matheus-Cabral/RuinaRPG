using System.Text.Json;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Resolves the effective RacialTraitSlots for a Variante — the GM's RacialTraitOverride when one
/// exists, otherwise RacialTraitLookup's hardcoded default. Shared by
/// CharacterPossessionsController and NpcPossessionsController (both need the same "which options
/// can be picked right now" answer to check pending state and validate a resolve request).
/// </summary>
internal static class RacialTraitOverrideResolver
{
    public static RacialTraitSlots Resolve(RacialTraitOverride? over, Variante variante)
    {
        if (over is null)
            return RacialTraitLookup.For(variante);

        return new RacialTraitSlots(
            Gratuita: DeserializeOptions(over.GratuitaOptionsJson),
            Obrigatoria: DeserializeOptions(over.ObrigatoriaOptionsJson));
    }

    private static List<RacialTraitOption> DeserializeOptions(string json) =>
        JsonSerializer.Deserialize<List<RacialTraitOption>>(json) ?? [];
}
