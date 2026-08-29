namespace RuinaRPG.Domain.Rules;

public static class CompendioSearchService
{
    public static IReadOnlyList<CompendioSearchResult> Search(
        string? query,
        IReadOnlyCollection<CompendioCategoria>? categorias,
        IReadOnlyList<TraitSeed> traits,
        IRulesDataProvider rules)
    {
        var results = new List<CompendioSearchResult>();

        if (categorias is null || categorias.Contains(CompendioCategoria.Caracteristica))
        {
            results.AddRange(traits.Select(t =>
                new CompendioSearchResult(CompendioCategoria.Caracteristica, $"Característica {t.Polaridade}", t.Nome, $"{t.Custo} ponto(s): {t.Descricao}")));
        }

        if (categorias is null || categorias.Contains(CompendioCategoria.Efeito))
        {
            results.AddRange(rules.Efeitos.Select(e =>
                new CompendioSearchResult(CompendioCategoria.Efeito, $"Efeito — {e.Grau}º Grau/Círculo {ToRoman(e.Grau)}", e.Nome, e.Descricao)));
        }

        if (categorias is null || categorias.Contains(CompendioCategoria.Tabela))
        {
            results.AddRange(rules.Niveis.Select(n =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de Níveis — Nível {n.Nivel}", $"Nível {n.Nivel}", n.BonusText)));
            results.AddRange(rules.Vocacoes.Select(v =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de Vocação — {v.Vocacao}, Nível {v.Nivel}", v.Vocacao, $"Vida {v.Vida}, Arcana {v.Arcana}")));
            results.AddRange(rules.Arquetipos.Select(a =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de Arquétipos — {a.Arquetipo}, Nível {a.Nivel}", a.Arquetipo, $"Vida {a.Vida}, Arcana {a.Arcana}")));
            results.AddRange(rules.CirculoGrauPorEap.Select(c =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de Círculo e Grau por EAP — {c.CirculoOuGrau}", $"Círculo/Grau {c.CirculoOuGrau}",
                    $"EAP absoluto {c.EapAbsoluto}, EAP relativo {c.EapRelativo}, Afinidade {c.AfinidadeAbsoluta} (+{c.AfinidadeGanhoPorNivel}/nível)")));
            results.AddRange(rules.XpPorNivel.Select(x =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de XP — Nível {x.Nivel}", $"Nível {x.Nivel}",
                    $"XP absoluto {x.XpAbsoluto}, XP relativo {x.XpRelativo}")));
            results.AddRange(rules.EapPorNivel.Select(e =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de EAP por Nível — Nível {e.Nivel}", $"Nível {e.Nivel}", $"EAP {e.ValorAbsoluto}")));
        }

        if (categorias is null || categorias.Contains(CompendioCategoria.Regra))
        {
            results.AddRange(rules.Regras.Select(r =>
                new CompendioSearchResult(CompendioCategoria.Regra, "Sistema Básico", r.Titulo, r.Conteudo)));
        }

        if (string.IsNullOrWhiteSpace(query))
            return results;

        return results
            .Where(r => r.Titulo.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || r.Conteudo.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static readonly string[] RomanNumerals = ["", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"];
    private static string ToRoman(int grau) => grau >= 1 && grau <= 9 ? RomanNumerals[grau] : grau.ToString();
}
