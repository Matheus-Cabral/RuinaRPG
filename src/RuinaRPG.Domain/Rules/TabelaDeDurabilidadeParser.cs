using RuinaRPG.Domain.Items;

namespace RuinaRPG.Domain.Rules;

/// <summary>Lê a tabela "| Rank | Durabilidade |" de Tabela de Durabilidade por Rank.md.</summary>
public static class TabelaDeDurabilidadeParser
{
    public static IReadOnlyList<DurabilidadeDeRank> Parse(string markdown)
    {
        var linhas = new List<DurabilidadeDeRank>();
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('|'))
                continue;
            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length < 2 || cells[0] == "Rank" || cells[0].StartsWith('-'))
                continue;

            if (!Enum.TryParse<RankDeItem>(cells[0], out var rank) || !Enum.IsDefined(rank))
                throw new FormatException($"Rank desconhecido na Tabela de Durabilidade: {cells[0]}");

            if (cells[1].Equals("Inquebrável", StringComparison.OrdinalIgnoreCase))
                linhas.Add(new DurabilidadeDeRank(rank, null, true));
            else if (int.TryParse(cells[1], out var valor) && valor >= 1)
                linhas.Add(new DurabilidadeDeRank(rank, valor, false));
            else
                throw new FormatException($"Durabilidade inválida para o Rank {cells[0]}: {cells[1]}");
        }
        return linhas;
    }
}
