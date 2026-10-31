using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Domain.Condicoes;

namespace RuinaRPG.Client.Shared;

/// <summary>O texto de uma linha de Condição, do motor do Domain (o mesmo que a ficha mostra), para o diálogo de aplicar.</summary>
public static class CondicaoResumo
{
    /// <summary>"1 turno" / "3 turnos".</summary>
    public static string Turnos(int turnos) => turnos == 1 ? "1 turno" : $"{turnos} turnos";

    /// <summary>
    /// O dano por elemento que só as Condições dão ("Dano de Fogo: +3"), um por elemento diferente de zero, na ordem do
    /// catálogo (os ids), com o nome do catálogo. Nada quando nenhuma Condição mexe nele.
    /// </summary>
    public static IEnumerable<string> LinhasDeDanoElemental(IReadOnlyDictionary<int, int>? danoElemental, Func<int, string> nomeDoElemento)
    {
        if (danoElemental is null)
            yield break;

        foreach (var (id, dano) in danoElemental.Where(d => d.Value != 0).OrderBy(d => d.Key))
            yield return $"Dano de {nomeDoElemento(id)}: {(dano < 0 ? "−" : "+")}{Math.Abs(dano)}";
    }

    /// <summary>A linha em português; <paramref name="valor"/> troca o valor da entrada do catálogo (o ajuste do GM).</summary>
    /// <param name="nomeDoElemento">O nome do elemento do catálogo a partir do alvo específico (o id em texto).</param>
    public static string Linha(CondicaoModificadorDto linha, int valor, Func<string, string> nomeDaPericia, Func<string, string> nomeDoElemento)
    {
        if (!Enum.TryParse<AlvoDeCondicao>(linha.Alvo, out var alvo) || !Enum.TryParse<TipoDeValorDeCondicao>(linha.TipoDeValor, out var tipo))
            return linha.Alvo;

        var modificador = new ModificadorDeCondicao(alvo, linha.AlvoEspecifico, tipo, valor,
            Enum.TryParse<MomentoDeCondicao>(linha.Momento, out var momento) ? momento : null);
        var ativa = new CondicaoAtiva(Guid.Empty, "", [modificador], new Dictionary<CategoriaDesabilitavel, IReadOnlySet<Guid>>());
        return new EfeitosDeCondicao([ativa]).Explicar(nomeDaPericia, nomeDoElemento: nomeDoElemento)[0].Linhas[0];
    }
}
