using System.Globalization;
using RuinaRPG.Domain.Runes;

namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>Como um valor de coluna aparece numa linha do log de ficha.</summary>
public static class SheetLogValues
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// bool → "Sim"/"Não"; null → "—"; decimal sem zeros à direita (vírgula); Disciplina da Runa → seu rótulo;
    /// demais enums → o nome do valor (os rótulos de Atributo/Rank etc. vivem no Client); outros → ToString().
    /// </summary>
    public static string Render(object? valor) => valor switch
    {
        null => "—",
        bool b => b ? "Sim" : "Não",
        DisciplinaDeRuna d => DisciplinaDeRunaInfo.Rotulo(d),
        decimal m => m.ToString("0.############################", PtBr),
        double d => d.ToString("0.################", PtBr),
        float f => f.ToString("0.#######", PtBr),
        IFormattable f => f.ToString(null, PtBr),
        _ => valor.ToString() ?? "—"
    };
}
