namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>
/// O que o servidor escreve por conta própria no log de ficha durante a ação de um usuário (Técnico R0012). Scoped,
/// como o SheetLogInterceptor que o lê: as marcas valem até o próximo salvamento que der certo.
/// <list type="bullet">
/// <item><see cref="MarcarAutomatica"/>: uma propriedade de ficha que o servidor mudou sozinho (o corte do Atual
/// quando uma Condição reduz o total). A linha que o interceptor monta dela sai com <c>Automatico = true</c>; o resto
/// do mesmo salvamento continua sendo do autor. A marca é por escrita, não por salvamento, porque a ação do GM e o
/// corte que ela provoca vão no mesmo SaveChanges.</item>
/// <item><see cref="Registrar"/>: uma linha que não vem de coluna nenhuma da ficha ("Condição X aplicada"). O
/// interceptor a assina (instante, autor, se é o GM) junto das outras e a descarta se a ficha estiver fora do log.</item>
/// </list>
/// </summary>
public sealed class SheetLogDoServidor
{
    public sealed record LinhaExplicita(SheetLogKind Kind, Guid SheetId, string Secao, SheetLogAcao Acao, string ItemNome, bool Automatico);

    private readonly HashSet<(object Entidade, string Propriedade)> _automaticas = new(new PorReferencia());
    private readonly List<LinhaExplicita> _explicitas = [];

    public void MarcarAutomatica(object entidade, string propriedade) => _automaticas.Add((entidade, propriedade));

    public void Registrar(LinhaExplicita linha) => _explicitas.Add(linha);

    internal bool EhAutomatica(object entidade, string propriedade) => _automaticas.Contains((entidade, propriedade));

    internal IReadOnlyList<LinhaExplicita> Explicitas => _explicitas;

    /// <summary>Depois de um salvamento que deu certo; num que falhou ficam, para uma nova tentativa gravá-las.</summary>
    internal void Limpar()
    {
        _automaticas.Clear();
        _explicitas.Clear();
    }

    /// <summary>A mesma entidade rastreada, não uma igual: duas fichas nunca se confundem.</summary>
    private sealed class PorReferencia : IEqualityComparer<(object Entidade, string Propriedade)>
    {
        public bool Equals((object Entidade, string Propriedade) x, (object Entidade, string Propriedade) y) =>
            ReferenceEquals(x.Entidade, y.Entidade) && x.Propriedade == y.Propriedade;

        public int GetHashCode((object Entidade, string Propriedade) obj) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Entidade), obj.Propriedade);
    }
}
