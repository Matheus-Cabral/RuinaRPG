using RuinaRPG.Domain.Runes;

namespace RuinaRPG.Client.Shared;

/// <summary>Rótulo exibido da Disciplina da Runa (nome do enum no contrato; null/desconhecido = "—").</summary>
public static class RuneDisciplinaRotulo
{
    public static string De(string? valor) =>
        Enum.TryParse<DisciplinaDeRuna>(valor, out var disciplina) && Enum.IsDefined(disciplina)
            ? DisciplinaDeRunaInfo.Rotulo(disciplina)
            : "—";
}
