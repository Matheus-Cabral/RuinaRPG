using System.Text.RegularExpressions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules;

public static partial class HistoricoSeedParser
{
    // Historico.md spells out each Perícia's proper Portuguese label (e.g. "Empatia c/ Animais"),
    // not the Pericia enum's unaccented member name — same label set as
    // RuinaRPG.Client.Shared.PericiaDisplay, inverted. Duplicated rather than shared because Domain
    // cannot reference the Client project.
    private static readonly Dictionary<string, Pericia> PericiaPorRotulo = new()
    {
        ["Acrobacia"] = Pericia.Acrobacia,
        ["Alquimia"] = Pericia.Alquimia,
        ["Arcano"] = Pericia.Arcano,
        ["Armadilhas"] = Pericia.Armadilhas,
        ["Armas Brancas"] = Pericia.ArmasBrancas,
        ["Artefatos Mágicos"] = Pericia.ArtefatosMagicos,
        ["Artístico"] = Pericia.Artistico,
        ["Atletismo"] = Pericia.Atletismo,
        ["Avaliação"] = Pericia.Avaliacao,
        ["Biblioteca"] = Pericia.Biblioteca,
        ["Brigar"] = Pericia.Brigar,
        ["Condução"] = Pericia.Conducao,
        ["Conhecimentos"] = Pericia.Conhecimentos,
        ["Crime"] = Pericia.Crime,
        ["Empatia c/ Animais"] = Pericia.EmpatiaComAnimais,
        ["Enganação"] = Pericia.Enganacao,
        ["Força de Vontade"] = Pericia.ForcaDeVontade,
        ["Fortitude"] = Pericia.Fortitude,
        ["Furtividade"] = Pericia.Furtividade,
        ["Herborismo"] = Pericia.Herborismo,
        ["Intimidação"] = Pericia.Intimidacao,
        ["Intuição"] = Pericia.Intuicao,
        ["Investigação"] = Pericia.Investigacao,
        ["Lábia"] = Pericia.Labia,
        ["Liderança"] = Pericia.Lideranca,
        ["Linguística"] = Pericia.Linguistica,
        ["Medicina"] = Pericia.Medicina,
        ["Navegação"] = Pericia.Navegacao,
        ["Ocultismo"] = Pericia.Ocultismo,
        ["Ofício"] = Pericia.Oficio,
        ["Percepção"] = Pericia.Percepcao,
        ["Pontaria"] = Pericia.Pontaria,
        ["Prontidão"] = Pericia.Prontidao,
        ["Reflexos"] = Pericia.Reflexos,
        ["Religião"] = Pericia.Religiao,
        ["Saquear"] = Pericia.Saquear,
        ["Sedução"] = Pericia.Seducao,
        ["Senso Comum"] = Pericia.SensoComum,
        ["Sobrevivência"] = Pericia.Sobrevivencia,
    };

    public static IReadOnlyList<HistoricoSeed> Parse(string markdown)
    {
        var sections = MarkdownSectionParser.Parse(markdown);
        var results = new List<HistoricoSeed>();

        foreach (var entry in sections)
        {
            var match = BonusLinesRegex().Match(entry.Body);
            if (!match.Success)
                continue; // no parseable "+6 X"/"+3 Y" pair — skip rather than seed a broken row

            if (!PericiaPorRotulo.TryGetValue(match.Groups[1].Value.Trim(), out var periciaMaisSeis))
                continue; // unrecognized Perícia label — skip rather than crash the whole seed
            if (!PericiaPorRotulo.TryGetValue(match.Groups[2].Value.Trim(), out var periciaMaisTres))
                continue;

            var nome = NumberPrefixRegex().Replace(entry.Title, "").Trim();
            var descricao = entry.Body[..match.Index].Trim();
            results.Add(new HistoricoSeed(nome, descricao, periciaMaisSeis, periciaMaisTres));
        }

        return results;
    }

    // "1. Estudo Acadêmico" -> "Estudo Acadêmico" — the leading "N. " is presentation order in the
    // source doc, not part of the catalog's Nome.
    [GeneratedRegex(@"^\d+\.\s*")]
    private static partial Regex NumberPrefixRegex();

    // Matches the "+6 <Perícia>" / "+3 <Perícia>" pair on two consecutive lines anywhere in the
    // entry's body (there is trailing "---" text after it in the raw body, which this pattern
    // simply never reaches).
    [GeneratedRegex(@"^\+6\s+(.+)$\r?\n^\+3\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex BonusLinesRegex();
}
