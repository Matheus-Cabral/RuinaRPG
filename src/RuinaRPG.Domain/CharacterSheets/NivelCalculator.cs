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
}
