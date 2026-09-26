namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Alma e Vida continuam no enum AfinidadeElemental (gravado como inteiro — remover um membro
/// deslocaria os valores salvos), mas são Caminhos, não uma Afinidade válida pra 1.a.
/// </summary>
public static class CaminhoSubElementoRules
{
    public static bool EhCaminho(AfinidadeElemental afinidade) => afinidade is AfinidadeElemental.Alma or AfinidadeElemental.Vida;
}
