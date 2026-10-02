namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Os dados que o GM pode escolher para dimensionar a sua Tabela de Arcas: a tabela tem uma linha por face.
/// </summary>
public static class DadoDeArca
{
    public static readonly IReadOnlyList<int> Faces = [6, 8, 10, 12, 20, 100];

    public const int Padrao = 20;

    public static bool EhValido(int faces) => Faces.Contains(faces);
}
