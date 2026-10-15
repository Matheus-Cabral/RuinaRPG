using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Onde as Condições ativas entram nos números calculados das três fichas: sempre DEPOIS da fórmula e dos
/// modificadores de Artefato/penalidade, uma vez por valor, no alvo daquele valor. Quem depende de outro valor
/// (Perícia e Sub-atributo de um Atributo, máximo de um recurso) recebe o outro já ajustado e depois passa pelas
/// suas próprias linhas. Com <see cref="EfeitosDeCondicao.Nenhum"/> todo método devolve o valor recebido.
/// Pisos: só onde o valor já não podia ficar abaixo sem Condições (Movimentação ≥ 1, máximos e Afinidade ≥ 0).
/// </summary>
public static class CondicoesNosNumeros
{
    public static int NoAtributo(this EfeitosDeCondicao efeitos, string atributo, int total) =>
        efeitos.Ajustar(AlvoDeCondicao.Atributo, atributo, total);

    public static int NaPericia(this EfeitosDeCondicao efeitos, string chave, int total) =>
        efeitos.Ajustar(AlvoDeCondicao.Pericia, chave, total);

    /// <summary><paramref name="maestriaId"/> é a linha de Maestria da ficha: uma linha restrita na aplicação só vale nas escolhidas.</summary>
    public static int NaMaestria(this EfeitosDeCondicao efeitos, string chaveDaPericia, Guid maestriaId, int total) =>
        efeitos.Ajustar(AlvoDeCondicao.Maestria, chaveDaPericia, total, maestriaId);

    /// <summary><paramref name="alvo"/> é uma constante de <see cref="SubAtributoAlvo"/>.</summary>
    public static int NoSubAtributo(this EfeitosDeCondicao efeitos, string alvo, int valor) =>
        efeitos.Ajustar(AlvoDeCondicao.SubAtributo, alvo, valor);

    /// <summary>SubAttributeFormulas.Movimentacao nunca dá menos de 1; com Condições também não.</summary>
    public static int NaMovimentacao(this EfeitosDeCondicao efeitos, int valor) =>
        Math.Max(1, efeitos.NoSubAtributo(SubAtributoAlvo.Movimentacao, valor));

    public static int NoRecursoTotal(this EfeitosDeCondicao efeitos, RecursoDeCondicao recurso, int maximo) =>
        Math.Max(0, efeitos.Ajustar(AlvoDeCondicao.RecursoTotal, recurso.ToString(), maximo));

    /// <summary>
    /// Os quatro máximos (1.c). <paramref name="vigorTotal"/> e <paramref name="astuciaTotal"/> chegam já ajustados
    /// por <see cref="NoAtributo"/>. O bônus de Artefato em "Adrenalina" é um Sub-atributo que soma ao total de
    /// Adrenalina, então esse total passa pelas linhas de Sub-atributo Adrenalina e depois pelas de Recurso total.
    /// </summary>
    public static (int Vitalidade, int Foco, int Adrenalina, int Estresse) NosMaximos(this EfeitosDeCondicao efeitos,
        int vigorTotal, int statusVida, int astuciaTotal, int statusFoco, int artefatoBonusParaAdrenalina) => (
        efeitos.NoRecursoTotal(RecursoDeCondicao.Vitalidade, ResourceMaximumCalculator.Vitalidade(vigorTotal, statusVida)),
        efeitos.NoRecursoTotal(RecursoDeCondicao.Foco, ResourceMaximumCalculator.Foco(astuciaTotal, statusFoco)),
        efeitos.NoRecursoTotal(RecursoDeCondicao.Adrenalina, efeitos.NoSubAtributo(SubAtributoAlvo.Adrenalina, ResourceMaximumCalculator.Adrenalina(artefatoBonusParaAdrenalina))),
        efeitos.NoRecursoTotal(RecursoDeCondicao.Estresse, ResourceMaximumCalculator.Estresse()));

    /// <summary>
    /// O valor exibido da Essência 1 de uma linha de Afinidade (2.c). Linha sem Elemento ou sem valor fica como está.
    /// Nunca abaixo de 0 (nem do próprio valor guardado, se ele já for negativo).
    /// </summary>
    public static int? NaAfinidade(this EfeitosDeCondicao efeitos, Elemento? elemento, int? valor) =>
        elemento is null || valor is null
            ? valor
            : Math.Max(Math.Min(valor.Value, 0), efeitos.Ajustar(AlvoDeCondicao.AfinidadeElemental, elemento.Value.ToString(), valor.Value));

    /// <summary>As linhas de Afinidade com a Essência 1 ajustada — entrada da Eficiência e do Dano Elemental (2.b).</summary>
    public static List<LinhaDeAfinidade> NasAfinidades(this EfeitosDeCondicao efeitos, List<LinhaDeAfinidade> linhas) =>
        efeitos.Vazio ? linhas : linhas.Select(l => l with { ElementoValor = efeitos.NaAfinidade(l.Elemento, l.ElementoValor) }).ToList();

    /// <summary>
    /// 3.f: por Tipo de Dano, a soma dos modificadores (Artefatos e penalidades) e então as linhas de Dano; por
    /// Elemento, só o que as linhas de Dano elemental dão (parte de 0).
    /// </summary>
    public static ModificadorDeDanoResponse NoModificadorDeDano(this EfeitosDeCondicao efeitos, IReadOnlyList<ArtifactBonusInput> modificadores)
    {
        int De(TipoDeDano tipo) => efeitos.Ajustar(AlvoDeCondicao.Dano, tipo.ToString(), ArtifactBonusCalculator.Sum(modificadores, TipoDeAlvo.Dano, tipo.ToString()));

        return new ModificadorDeDanoResponse(De(TipoDeDano.Cortante), De(TipoDeDano.Perfurante), De(TipoDeDano.Contundente), De(TipoDeDano.Arcano),
            Enum.GetValues<Elemento>().ToDictionary(e => e.ToString(), e => efeitos.Ajustar(AlvoDeCondicao.DanoElemental, e.ToString(), 0)));
    }
}
