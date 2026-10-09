using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.Rules;

/// <summary>Lê a tabela "| Afinidade | Eficiência | Dano |" de Tabela de Afinidades.md.</summary>
public static class TabelaDeAfinidadesParser
{
    public static IReadOnlyList<LinhaDaTabelaDeAfinidades> Parse(string markdown)
    {
        var linhas = new List<LinhaDaTabelaDeAfinidades>();
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('|'))
                continue;
            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells[0] == "Afinidade" || cells[0].StartsWith('-'))
                continue;

            if (cells.Length != 3
                || !int.TryParse(cells[0], out var afinidade) || afinidade < 0
                || !int.TryParse(cells[1], out var eficiencia) || eficiencia < 0
                || !int.TryParse(cells[2], out var dano) || dano < 0)
                throw new FormatException($"Linha inválida na Tabela de Afinidades: {line}");

            if (linhas.Any(l => l.Afinidade == afinidade))
                throw new FormatException($"Afinidade repetida na Tabela de Afinidades: {afinidade}");

            linhas.Add(new LinhaDaTabelaDeAfinidades(afinidade, eficiencia, dano));
        }
        return linhas;
    }
}
