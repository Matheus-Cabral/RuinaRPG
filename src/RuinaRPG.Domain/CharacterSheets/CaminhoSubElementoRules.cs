namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Prever, Purificar, Curar, Aprimorar, Ecomancia, Hemomancia, Necromancia e Invocação só ficam
/// disponíveis numa linha de Afinidade quando o Caminho da linha (Alma, Vida ou Mundano) é o que a
/// Matriz Elemental liga ao Sub-Elemento e o Elemento da linha é um dos combináveis com ele
/// (ex.: Curar exige Fogo + Vida). Os demais Sub-Elementos não dependem de Caminho.
/// </summary>
public static class CaminhoSubElementoRules
{
    private static readonly Dictionary<SubElemento, Caminho> Exigidos = new()
    {
        [SubElemento.Prever] = Caminho.Alma,
        [SubElemento.Purificar] = Caminho.Alma,
        [SubElemento.Curar] = Caminho.Vida,
        [SubElemento.Aprimorar] = Caminho.Vida,
        [SubElemento.Ecomancia] = Caminho.Mundano,
        [SubElemento.Hemomancia] = Caminho.Mundano,
        [SubElemento.Necromancia] = Caminho.Mundano,
        [SubElemento.Invocacao] = Caminho.Mundano,
    };

    // Alma e Vida continuam nos enums SubElemento/AfinidadeElemental (gravados como inteiro — remover
    // um membro deslocaria os valores já salvos), mas são Caminhos: não se escolhe mais um deles como
    // Sub-Elemento (2.c) nem como Afinidade (1.a). Só valores antigos já salvos sobrevivem.
    public static bool EhCaminho(SubElemento subElemento) => subElemento is SubElemento.Alma or SubElemento.Vida;

    public static bool EhCaminho(AfinidadeElemental afinidade) => afinidade is AfinidadeElemental.Alma or AfinidadeElemental.Vida;

    public static Caminho? CaminhoExigido(SubElemento subElemento) =>
        Exigidos.TryGetValue(subElemento, out var caminho) ? caminho : null;

    public static bool Permite(Elemento? elemento, Caminho? caminho, SubElemento subElemento)
    {
        if (CaminhoExigido(subElemento) is not { } exigido)
            return true;

        return caminho == exigido
            && elemento is { } e
            && ElementoSubElementoValidator.IsValidCombination(e, subElemento);
    }

    // Nomes exatos — o Enum.TryParse aceitaria "1" ou "vida", o que deixaria texto livre antigo
    // passar por Caminho.
    public static bool TryParseCaminho(string? raw, out Caminho caminho)
    {
        switch (raw)
        {
            case "Alma": caminho = Caminho.Alma; return true;
            case "Vida": caminho = Caminho.Vida; return true;
            case "Mundano": caminho = Caminho.Mundano; return true;
            default: caminho = default; return false;
        }
    }

    /// <summary>
    /// Valida o Caminho de uma linha de Afinidade e, quando o Sub-Elemento depende de Caminho, o
    /// trio Elemento/Caminho/Sub-Elemento. Devolve a mensagem de erro, ou null se a linha é válida.
    /// Só checa o que mudou em relação ao que já estava salvo — uma linha antiga (inclusive com
    /// Caminho em texto livre) reenviada sem alteração nunca é invalidada. Numa linha nova, passe
    /// null nos três valores antigos.
    /// </summary>
    public static string? Validar(Elemento? elemento, SubElemento? subElemento, string? caminhoNome,
        Elemento? elementoAntigo, SubElemento? subElementoAntigo, string? caminhoNomeAntigo)
    {
        var caminhoMudou = caminhoNome != caminhoNomeAntigo;
        Caminho? caminho = TryParseCaminho(caminhoNome, out var parsed) ? parsed : null;

        if (caminhoMudou && !string.IsNullOrEmpty(caminhoNome) && caminho is null)
            return "Caminho desconhecido — use Alma, Vida ou Mundano.";

        var trioMudou = caminhoMudou || elemento != elementoAntigo || subElemento != subElementoAntigo;
        if (trioMudou && subElemento is { } se && CaminhoExigido(se) is { } exigido && !Permite(elemento, caminho, se))
            return $"Esse Sub-Elemento exige o Caminho {exigido} e um Elemento compatível na mesma linha.";

        return null;
    }
}
