using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.Rules.Niveis;

public sealed record ColunaDeNivelDef(Guid Id, string Nome, TipoDeColunaDeNivel Tipo, string? ChaveDeSistema, int Ordem);

public sealed record LinhaDeNivel(int Nivel, string? OutrosBonus, IReadOnlyDictionary<Guid, int?> Valores);

public sealed class ProgressaoDeNivel
{
    public ProgressaoDeNivel(IReadOnlyList<ColunaDeNivelDef> colunas, IReadOnlyList<LinhaDeNivel> linhas)
    {
        Colunas = colunas.OrderBy(c => c.Ordem).ToList();
        Linhas = linhas.OrderBy(l => l.Nivel).ToList();
    }

    public IReadOnlyList<ColunaDeNivelDef> Colunas { get; }
    public IReadOnlyList<LinhaDeNivel> Linhas { get; }
    public int UltimoNivel => Linhas.Count == 0 ? 0 : Linhas[^1].Nivel;

    private ColunaDeNivelDef? Coluna(string chave) => Colunas.FirstOrDefault(c => c.ChaveDeSistema == chave);

    public int Acumulado(string chave, int nivel) => Coluna(chave) is { } c ? Soma(c, nivel) : 0;
    public int? Limite(string chave, int nivel) => Coluna(chave) is { } c ? Herdado(c, nivel) : null;
    public int? ValorExato(string chave, int nivel) => Coluna(chave) is { } c ? Celula(c, nivel) : null;

    public int? Resolver(ColunaDeNivelDef coluna, int nivel) => coluna.Tipo switch
    {
        TipoDeColunaDeNivel.Acumulativa => Soma(coluna, nivel),
        _ when ChavesDeNivel.SemHeranca(coluna.ChaveDeSistema) => Celula(coluna, nivel),
        _ => Herdado(coluna, nivel),
    };

    public IReadOnlyList<string> LinhasDeBonus(int nivel)
    {
        var linhas = Colunas.Where(c => c.Tipo == TipoDeColunaDeNivel.Acumulativa)
            .Select(c => (c, v: Celula(c, nivel) ?? 0)).Where(x => x.v != 0)
            .Select(x => $"{x.c.Nome}: +{x.v}").ToList();
        var outros = Linhas.FirstOrDefault(l => l.Nivel == nivel)?.OutrosBonus;
        if (!string.IsNullOrWhiteSpace(outros))
            linhas.AddRange(outros.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        return linhas;
    }

    /// <summary>O último nível é sempre "Lvl. Max", seja qual for o XP gravado nele (não há próximo nível).</summary>
    public IReadOnlyList<XpPorNivel> ComoXpPorNivel() =>
        Linhas.Select(l => new XpPorNivel(l.Nivel, l.Nivel == UltimoNivel ? "Lvl. Max" : ValorExato(ChavesDeNivel.XpParaProximoNivel, l.Nivel)?.ToString() ?? "Lvl. Max", "")).ToList();

    public IReadOnlyList<EapPorNivel> ComoEapPorNivel() =>
        Linhas.Select(l => new EapPorNivel(l.Nivel, ValorExato(ChavesDeNivel.EapBase, l.Nivel) ?? 0)).ToList();

    public IReadOnlyList<LevelBonus> ComoLevelBonus() =>
        Linhas.Select(l => new LevelBonus(l.Nivel, string.Join("<br>", LinhasDeBonus(l.Nivel)))).ToList();

    private int? Celula(ColunaDeNivelDef c, int nivel) =>
        Linhas.FirstOrDefault(l => l.Nivel == nivel)?.Valores.GetValueOrDefault(c.Id);

    private int Soma(ColunaDeNivelDef c, int nivel) =>
        Linhas.Where(l => l.Nivel <= nivel).Sum(l => l.Valores.GetValueOrDefault(c.Id) ?? 0);

    private int? Herdado(ColunaDeNivelDef c, int nivel) =>
        Linhas.Where(l => l.Nivel <= nivel).Reverse().Select(l => l.Valores.GetValueOrDefault(c.Id)).FirstOrDefault(v => v is not null);
}
