using RuinaRPG.Domain.Runes;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Conversão da Disciplina da Runa entre o fio (nome exato do enum) e o enum. null/"" devolve true com
/// disciplina nula — quem chama decide se a ausência é permitida (não é ao criar/editar).
/// </summary>
internal static class RuneDisciplina
{
    public const string RequiredMessage = "Disciplina é obrigatória.";
    public const string UnknownMessage = "Disciplina de Runa desconhecida.";

    public static bool TryParse(string? value, out DisciplinaDeRuna? disciplina)
    {
        disciplina = null;
        if (string.IsNullOrEmpty(value))
            return true;

        // Nome exato: Enum.TryParse aceitaria "adicao" (ignoreCase) e números como "1".
        foreach (var d in DisciplinaDeRunaInfo.Todas)
            if (value == d.ToString()) { disciplina = d; return true; }
        return false;
    }

    public static string? Format(DisciplinaDeRuna? disciplina) => disciplina?.ToString();
}
