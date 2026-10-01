namespace RuinaRPG.Domain.Rules.ReferenceData;

/// <summary>
/// Vida e Arcana de uma Vocação/Classe/Arquétipo num nível. As tabelas de origem têm um número
/// fixo de níveis (50); acima do último, vale a linha mais alta que seja menor ou igual ao nível.
/// Sem linha aplicável (nome desconhecido ou nível abaixo da primeira linha) o resultado é (0, 0).
/// </summary>
public static class VidaEArcanaPorNivel
{
    public static (int Vida, int Arcana) Vocacao(IEnumerable<VocacaoProgressao> tabela, string nome, int nivel)
        => Escolher(tabela.Where(v => v.Vocacao == nome).Select(v => (v.Nivel, v.Vida, v.Arcana)), nivel);

    public static (int Vida, int Arcana) Classe(IEnumerable<ClasseProgressao> tabela, string nome, int nivel)
        => Escolher(tabela.Where(v => v.Classe == nome).Select(v => (v.Nivel, v.Vida, v.Arcana)), nivel);

    public static (int Vida, int Arcana) Arquetipo(IEnumerable<ArquetipoProgressao> tabela, string nome, int nivel)
        => Escolher(tabela.Where(v => v.Arquetipo == nome).Select(v => (v.Nivel, v.Vida, v.Arcana)), nivel);

    private static (int Vida, int Arcana) Escolher(IEnumerable<(int Nivel, int Vida, int Arcana)> linhas, int nivel)
    {
        var linha = linhas.Where(l => l.Nivel <= nivel).OrderByDescending(l => l.Nivel).Cast<(int Nivel, int Vida, int Arcana)?>().FirstOrDefault();
        return linha is { } l ? (l.Vida, l.Arcana) : (0, 0);
    }
}
