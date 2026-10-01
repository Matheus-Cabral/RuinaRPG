using System.Text.RegularExpressions;

namespace RuinaRPG.Domain.Rules.Niveis;

public sealed record ExtracaoDeBonus(IReadOnlyDictionary<string, int> Valores, IReadOnlyList<string> Restante);

public static partial class NivelBonusExtractor
{
    public static ExtracaoDeBonus Extrair(string bonusText)
    {
        var valores = new Dictionary<string, int>();
        var restante = new List<string>();
        var linhas = (bonusText ?? "").Split("<br>", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var linha in linhas)
        {
            var consumida = false;
            foreach (var (chave, regex) in Regras)
            {
                var matches = regex.Matches(linha);
                if (matches.Count == 0) continue;
                valores[chave] = valores.GetValueOrDefault(chave) + matches.Sum(m => int.Parse(m.Groups[1].Value));
                consumida = true;
            }
            if (!consumida) restante.Add(linha);
        }
        return new ExtracaoDeBonus(valores, restante);
    }

    private static IEnumerable<(string Chave, Regex Regex)> Regras
    {
        get
        {
            yield return (ChavesDeNivel.PontosDeAtributo, PontosDeAtributoRegex());
            yield return (ChavesDeNivel.PontosDePericia, PontosDePericiaRegex());
            yield return (ChavesDeNivel.EspacosDeCaracteristica, EspacoDeCaracteristicaRegex());
            yield return (ChavesDeNivel.PontosDeIgnicao, PontosDeIgnicaoRegex());
            yield return (ChavesDeNivel.EspacosDeMaestria, EspacoDeMaestriaRegex());
            yield return (ChavesDeNivel.PontosDeMaestria, PontosDeMaestriaRegex());
        }
    }

    [GeneratedRegex(@"\+(\d+)\s+Pontos?\s+de\s+Atributo\b", RegexOptions.IgnoreCase)]
    private static partial Regex PontosDeAtributoRegex();

    [GeneratedRegex(@"\+(\d+)\s+Pontos?\s+de\s+Per[ií]cia\b", RegexOptions.IgnoreCase)]
    private static partial Regex PontosDePericiaRegex();

    [GeneratedRegex(@"\+(\d+)\s+Espaços?\s+de\s+Caracter[ií]stica\b", RegexOptions.IgnoreCase)]
    private static partial Regex EspacoDeCaracteristicaRegex();

    [GeneratedRegex(@"\+(\d+)\s+Pontos?\s+de\s+Ignição\b", RegexOptions.IgnoreCase)]
    private static partial Regex PontosDeIgnicaoRegex();

    [GeneratedRegex(@"\+(\d+)\s+Espaços?\s+de\s+Maestria\b", RegexOptions.IgnoreCase)]
    private static partial Regex EspacoDeMaestriaRegex();

    [GeneratedRegex(@"\+(\d+)\s+Pontos?\s+de\s+Maestria\b", RegexOptions.IgnoreCase)]
    private static partial Regex PontosDeMaestriaRegex();
}
