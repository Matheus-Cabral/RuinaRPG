using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Ficha de Personagem 1.b: os Pontos de Ignição Atual acompanham o Nível. Subir soma o que os
/// níveis ganhos concedem na Tabela de Níveis; descer subtrai o mesmo, sem passar de zero.
/// </summary>
public static class PontosDeIgnicaoPorNivel
{
    public static int Delta(int nivelAntigo, int nivelNovo, ProgressaoDeNivel tabela) =>
        tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivelNovo) - tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivelAntigo);

    public static int Aplicar(int atual, int delta) => Math.Max(0, atual + delta);

    /// <summary>O saldo com que uma ficha nova começa: tudo o que a tabela concede até o nível inicial.</summary>
    public static int Inicial(int nivel, ProgressaoDeNivel tabela) => tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivel);
}
