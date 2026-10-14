using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Domain.Condicoes;

namespace RuinaRPG.Client.Shared;

/// <summary>O texto de uma linha de Condição, do motor do Domain (o mesmo que a ficha mostra), para o diálogo de aplicar.</summary>
public static class CondicaoResumo
{
    /// <summary>"1 turno" / "3 turnos".</summary>
    public static string Turnos(int turnos) => turnos == 1 ? "1 turno" : $"{turnos} turnos";

    /// <summary>
    /// O dano por Elemento que só as Condições dão ("Dano de Fogo: +3"), um por Elemento diferente de zero, na ordem da Matriz.
    /// Nada quando nenhuma Condição mexe nele.
    /// </summary>
    public static IEnumerable<string> LinhasDeDanoElemental(IReadOnlyDictionary<string, int>? danoElemental)
    {
        if (danoElemental is null)
            yield break;

        foreach (var (valor, rotulo) in Fields.AfinidadeEssenciaField.Elementos)
        {
            if (danoElemental.TryGetValue(valor, out var dano) && dano != 0)
                yield return $"Dano de {rotulo}: {(dano < 0 ? "−" : "+")}{Math.Abs(dano)}";
        }
    }

    /// <summary>A linha em português; <paramref name="valor"/> troca o valor da entrada do catálogo (o ajuste do GM).</summary>
    public static string Linha(CondicaoModificadorDto linha, int valor, Func<string, string> nomeDaPericia)
    {
        if (!Enum.TryParse<AlvoDeCondicao>(linha.Alvo, out var alvo) || !Enum.TryParse<TipoDeValorDeCondicao>(linha.TipoDeValor, out var tipo))
            return linha.Alvo;

        var modificador = new ModificadorDeCondicao(alvo, linha.AlvoEspecifico, tipo, valor,
            Enum.TryParse<MomentoDeCondicao>(linha.Momento, out var momento) ? momento : null);
        var ativa = new CondicaoAtiva(Guid.Empty, "", [modificador], new Dictionary<CategoriaDesabilitavel, IReadOnlySet<Guid>>());
        return new EfeitosDeCondicao([ativa]).Explicar(nomeDaPericia)[0].Linhas[0];
    }
}
