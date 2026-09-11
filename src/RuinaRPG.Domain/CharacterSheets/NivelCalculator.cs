using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Nível is now a pure function of Experiência Atual (1.b, "Para o próximo") instead of a
/// directly-editable field — GM overrides are no longer a thing for Ficha de Personagem. Each
/// XpPorNivel row's XpAbsoluto is the XP at which a character reaches that Nível — reaching it
/// exactly already counts (e.g. XP=50 is already Nível 2, not Nível 1); the "Nível 50" row is
/// the non-numeric "Lvl. Max" sentinel (no further threshold).
/// </summary>
public static class NivelCalculator
{
    public static int Compute(int experienciaAtual, IReadOnlyList<XpPorNivel> tabela)
    {
        var nivel = 1;
        foreach (var row in tabela.OrderBy(r => r.Nivel))
        {
            if (!int.TryParse(row.XpAbsoluto, out var threshold))
                continue; // "Lvl. Max" row (Nível 50) isn't a real numeric threshold to compare against

            if (experienciaAtual >= threshold)
                nivel = row.Nivel + 1;
        }

        return nivel;
    }

    /// <summary>XP still needed to reach the next Nível, or null once at the level cap (Nível 50).</summary>
    public static int? XpParaProximoNivel(int experienciaAtual, IReadOnlyList<XpPorNivel> tabela)
    {
        var nivelAtual = Compute(experienciaAtual, tabela);
        var currentRow = tabela.FirstOrDefault(r => r.Nivel == nivelAtual);
        if (currentRow is null || !int.TryParse(currentRow.XpAbsoluto, out var threshold))
            return null; // at Nível 50 ("Lvl. Max"), there's no next threshold

        return threshold - experienciaAtual;
    }

    /// <summary>
    /// Inverse of Compute: the minimum Experiência Atual that computes back to the given Nível.
    /// Used where Nível (not XP) is the field being edited directly — NPCs and Criaturas keep
    /// Nível directly editable (unlike Ficha de Personagem), so editing it needs a value to push
    /// into Experiência Atual to keep the two fields consistent with each other.
    /// </summary>
    public static int MinXpParaNivel(int nivel, IReadOnlyList<XpPorNivel> tabela)
    {
        if (nivel <= 1)
            return 0;

        // Nível N's own row holds the threshold to reach N+1, not the threshold to reach N — that
        // one is on the previous row (N-1), same "reaching it exactly already counts" rule as
        // Compute above.
        var previousRow = tabela.FirstOrDefault(r => r.Nivel == nivel - 1);
        if (previousRow is null || !int.TryParse(previousRow.XpAbsoluto, out var threshold))
            return 0; // no numeric previous threshold (shouldn't happen for a valid 1-50 Nível)

        return threshold;
    }
}
