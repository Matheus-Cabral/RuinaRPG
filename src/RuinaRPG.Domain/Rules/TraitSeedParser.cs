using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules;

public static partial class TraitSeedParser
{
    public static IReadOnlyList<TraitSeed> Parse(string markdown)
    {
        var sections = MarkdownSectionParser.Parse(markdown);
        var results = new List<TraitSeed>();

        foreach (var polaritySection in sections.Where(s => s.Title is "Positivas" or "Negativas"))
        {
            var polaridade = polaritySection.Title == "Positivas" ? "Positiva" : "Negativa";

            foreach (var trait in polaritySection.Children)
            {
                var tiers = CostTierRegex().Matches(trait.Body);
                if (tiers.Count == 0)
                    continue; // traits with no parseable cost line (e.g. a pure prose prerequisite paragraph) are skipped

                var multiTier = tiers.Count > 1;
                foreach (Match tier in tiers)
                {
                    var custo = int.Parse(tier.Groups[1].Value) * (tier.Groups[1].Value.StartsWith('-') ? 1 : polaridade == "Negativa" ? -1 : 1);
                    var nome = multiTier ? $"{trait.Title} ({tier.Groups[1].Value.TrimStart('-')} ponto{(Math.Abs(custo) == 1 ? "" : "s")})" : trait.Title;
                    results.Add(new TraitSeed(nome, tier.Groups[2].Value.Trim(), custo, polaridade));
                }
            }
        }

        return results;
    }

    // Matches "N ponto(s): description" or "-N ponto(s): description", tolerating a trailing
    // qualifier between "ponto(s)" and the colon (e.g. "2 pontos por sentido:", "-1 ponto cada:").
    // Description runs to the next cost-tier line or end of body. Case-insensitive: the real
    // source document has at least one cost line capitalized as "Ponto" ("Fanfarronice") amid
    // dozens of lowercase "ponto" lines, and this parser must not silently drop it.
    [GeneratedRegex(@"(-?\d+)\s*pontos?(?:\s+[^\n:]+)?:\s*(.+?)(?=\r?\n\r?\n-?\d+\s*pontos?|\z)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CostTierRegex();
}
