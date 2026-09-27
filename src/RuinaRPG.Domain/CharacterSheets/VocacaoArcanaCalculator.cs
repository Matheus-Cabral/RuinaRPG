using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Pontos de Vocação Arcana distribuídos nas Afinidades (Ficha de Personagem 2.c).
/// </summary>
public static class VocacaoArcanaCalculator
{
    public static bool EhMagica(Vocacao? vocacao) => vocacao is Vocacao.Feiticeiro or Vocacao.Adepto or Vocacao.Bruxo;

    /// <summary>
    /// Cada Essência (Elemento ou Caminho) conta uma vez só, pelo maior Valor em que aparece em
    /// qualquer linha, seja como Essência 1 ou 2; os Valores de Sub-Elemento somam todos.
    /// </summary>
    public static int Gasto(IEnumerable<LinhaDeAfinidade> linhas)
    {
        var maiorPorEssencia = new Dictionary<EssenciaBasica, int>();
        var subElementos = 0;

        foreach (var linha in linhas)
        {
            // Os 4 primeiros membros de EssenciaBasica coincidem com Elemento.
            if (linha.Elemento is { } elemento)
                Registrar(maiorPorEssencia, (EssenciaBasica)(int)elemento, linha.ElementoValor ?? 0);
            if (linha.SegundaEssencia is { } segunda)
                Registrar(maiorPorEssencia, segunda, linha.SegundaEssenciaValor ?? 0);
            subElementos += linha.SubElementoValor ?? 0;
        }

        return maiorPorEssencia.Values.Sum() + subElementos;
    }

    /// <summary>
    /// Vocações mágicas partem da coluna Afinidade da Tabela de Círculo e Grau por EAP no Círculo
    /// atual; as marciais (e a Vocação ainda não escolhida) só têm a Afinidade Adicional.
    /// </summary>
    public static int Maxima(Vocacao? vocacao, int circulo, int afinidadeAdicional, IReadOnlyList<CirculoGrauPorEap> tabela)
    {
        var baseDaTabela = EhMagica(vocacao)
            ? tabela.Where(r => r.CirculoOuGrau == circulo).Select(r => r.AfinidadeAbsoluta).FirstOrDefault()
            : 0;
        return baseDaTabela + afinidadeAdicional;
    }

    private static void Registrar(Dictionary<EssenciaBasica, int> maiorPorEssencia, EssenciaBasica essencia, int valor) =>
        maiorPorEssencia[essencia] = Math.Max(maiorPorEssencia.GetValueOrDefault(essencia), valor);
}
