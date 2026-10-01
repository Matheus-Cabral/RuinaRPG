namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>Perícias que entram em fórmulas (Iniciativa, Esquiva/Reflexos, Fortitude — ver Formulas.md): não podem ser removidas na Auditoria.</summary>
public static class PericiasDeSistema
{
    public const int Fortitude = 17;
    public const int Prontidao = 32;
    public const int Reflexos = 33;

    public static IReadOnlyList<int> Todas { get; } = [Fortitude, Prontidao, Reflexos];

    public static bool IsProtegida(int id) => Todas.Contains(id);
}
