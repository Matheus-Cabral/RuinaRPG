using System.Globalization;

namespace RuinaRPG.Domain.Afinidades;

/// <summary>
/// Regras de Afinidades configuradas pelo Auditor. Em <paramref name="Visibilidade"/>, Vocacao é o nome do
/// enum <c>Vocacao</c> e Classe o nome da Classe como gravado na ficha; uma linha vale se casar com qualquer um.
/// É o catálogo único de elementos do app (Auditoria de Regras R0018): ficha, Criatura, Condições e Passivas.
/// </summary>
public sealed record CatalogoDeAfinidades(
    IReadOnlyList<ElementoDoCatalogo> Elementos,
    IReadOnlyList<Cruzamento> Cruzamentos,
    IReadOnlyDictionary<int, int> TotalPorCirculo,
    IReadOnlyList<(string? Vocacao, string? Classe, int ElementoId)> Visibilidade)
{
    /// <summary>
    /// Os elementos que podem ser uma Afinidade (campo <i>Afinidade</i> das fichas, alvo das Condições, requisito
    /// das Passivas): todos menos os Caminhos, na ordem do catálogo.
    /// </summary>
    public IReadOnlyList<ElementoDoCatalogo> Afinidades => Elementos.Where(e => e.Tipo != TipoDeElemento.Caminho).ToList();

    /// <summary>O nome do elemento; um id fora do catálogo aparece como o próprio número.</summary>
    public string NomeDe(int id) => Elementos.FirstOrDefault(e => e.Id == id)?.Nome ?? id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Se o alvo específico de uma linha de Condição (o id do elemento em texto) é uma Afinidade do catálogo.
    /// </summary>
    public bool EhAfinidade(string valor) => IdDoAlvo(valor) is { } id && Afinidades.Any(e => e.Id == id);

    /// <summary>
    /// O nome exibido do alvo específico de uma linha de Condição; um valor que não é id do catálogo (um nome antigo
    /// que a conversão não casou) aparece como está.
    /// </summary>
    public string NomeDoAlvo(string valor) =>
        IdDoAlvo(valor) is { } id && Elementos.FirstOrDefault(e => e.Id == id) is { } e ? e.Nome : valor;

    /// <summary>
    /// Null quando a Afinidade escolhida numa ficha ou Criatura (1.a/2.b) é aceita. Reenviar o valor já salvo nunca é
    /// recusado (um Caminho antigo continua); uma escolha nova tem de ser um elemento do catálogo que não é Caminho.
    /// </summary>
    public string? MotivoParaRecusarAfinidade(int? nova, int? atual)
    {
        if (nova is not { } id || nova == atual)
            return null;
        var elemento = Elementos.FirstOrDefault(e => e.Id == id);
        if (elemento is null)
            return "Afinidade inválida.";
        return elemento.Tipo == TipoDeElemento.Caminho ? $"{elemento.Nome} é um Caminho, não uma Afinidade." : null;
    }

    private static int? IdDoAlvo(string valor) =>
        int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
}
