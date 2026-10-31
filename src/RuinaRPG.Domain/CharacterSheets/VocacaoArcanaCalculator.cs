namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Vocações mágicas e marciais (Ficha de Personagem R0007): as mágicas somam o total do Círculo ao máximo de pontos de
/// Afinidade (RegrasDeAfinidade.Maximo); as marciais só têm a Afinidade Adicional.
/// </summary>
public static class VocacaoArcanaCalculator
{
    public static bool EhMagica(Vocacao? vocacao) => vocacao is Vocacao.Feiticeiro or Vocacao.Adepto or Vocacao.Bruxo;
}
