using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.Afinidades;

/// <summary>
/// O que o Auditor pode gravar no catálogo de Afinidades (Auditoria de Regras R0018–R0021): elementos, cruzamentos,
/// ligações de visibilidade e totais por Círculo. Cada método devolve null quando aceita, ou a mensagem da recusa.
/// </summary>
public static class AuditoriaDeAfinidades
{
    /// <summary>
    /// Um elemento novo (<paramref name="id"/> nulo) ou editado. O nome é obrigatório e único (sem diferenciar caixa
    /// nem espaços nas pontas); o teto é vazio ou ≥ 1; um Sub-Elemento que um cruzamento gera não muda de tipo.
    /// </summary>
    public static string? MotivoParaRecusarElemento(CatalogoDeAfinidades c, int? id, string nome, TipoDeElemento tipo, int? teto)
    {
        var limpo = nome.Trim();
        if (limpo.Length == 0)
            return "O nome do elemento é obrigatório.";
        if (c.Elementos.FirstOrDefault(e => e.Id != id && string.Equals(e.Nome.Trim(), limpo, StringComparison.OrdinalIgnoreCase)) is { } repetido)
            return $"Já existe um elemento chamado {repetido.Nome}.";
        if (teto is < 1)
            return "O teto deve ser vazio (sem limite) ou um inteiro maior ou igual a 1.";
        if (id is { } atual && tipo != TipoDeElemento.SubElemento && c.Cruzamentos.Any(x => x.SubElemento == atual))
            return $"{c.NomeDe(atual)} é gerado por um cruzamento: continua Sub-Elemento enquanto o cruzamento existir.";
        return null;
    }

    /// <summary>A recusa de remover um elemento ainda usado; as contagens zero ficam de fora do texto.</summary>
    public static string? MotivoParaRecusarRemocao(int fichas, int condicoes, int passivas)
    {
        var partes = new List<string>();
        if (fichas > 0) partes.Add($"{fichas} ficha(s)");
        if (condicoes > 0) partes.Add($"{condicoes} Condição(ões)");
        if (passivas > 0) partes.Add($"{passivas} Passiva(s)");
        if (partes.Count == 0)
            return null;
        var lista = partes.Count == 1 ? partes[0] : $"{string.Join(", ", partes[..^1])} e {partes[^1]}";
        return $"Este elemento é usado por {lista}.";
    }

    /// <summary>
    /// Um cruzamento novo (<paramref name="atual"/> nulo) ou a edição de <paramref name="atual"/>: dois elementos
    /// diferentes do catálogo geram um Sub-Elemento que não é nenhum dos dois, com mínimos ≥ 1, e o par (simétrico)
    /// não pode repetir o de outro cruzamento.
    /// </summary>
    public static string? MotivoParaRecusarCruzamento(CatalogoDeAfinidades c, Cruzamento novo, Cruzamento? atual)
    {
        if (novo.ElementoA == novo.ElementoB)
            return "Os dois elementos do cruzamento devem ser diferentes.";
        if (new[] { novo.ElementoA, novo.ElementoB, novo.SubElemento }.Any(id => c.Elementos.All(e => e.Id != id)))
            return "Elemento inexistente.";
        if (c.Elementos.First(e => e.Id == novo.SubElemento).Tipo != TipoDeElemento.SubElemento)
            return $"{c.NomeDe(novo.SubElemento)} não é um Sub-Elemento.";
        if (novo.SubElemento == novo.ElementoA || novo.SubElemento == novo.ElementoB)
            return "O Sub-Elemento não pode ser um dos dois elementos do cruzamento.";
        if (novo.MinimoA < 1 || novo.MinimoB < 1)
            return "Os mínimos do cruzamento devem ser inteiros maiores ou iguais a 1.";
        if (c.Cruzamentos.FirstOrDefault(x => x.MesmoPar(novo) && (atual is null || !x.MesmoPar(atual))) is { } repetido)
            return $"Já existe um cruzamento de {c.NomeDe(repetido.ElementoA)} e {c.NomeDe(repetido.ElementoB)}.";
        return null;
    }

    /// <summary>
    /// Ligar ou desligar um Caminho/Sub-Elemento de uma Vocação mágica (pelo nome do membro de <see cref="Vocacao"/>)
    /// ou de uma Classe da tabela atual (<see cref="ClassesPorVocacao"/>). Os Elementos base aparecem sempre.
    /// </summary>
    public static string? MotivoParaRecusarVisibilidade(CatalogoDeAfinidades c, OrigemDaVisibilidade origem, string chave, int elementoId)
    {
        var elemento = c.Elementos.FirstOrDefault(e => e.Id == elementoId);
        if (elemento is null)
            return "Elemento inexistente.";
        if (elemento.Tipo == TipoDeElemento.Base)
            return $"{elemento.Nome} é um Elemento base: aparece sempre.";
        if (origem == OrigemDaVisibilidade.Vocacao)
        {
            var magica = Enum.TryParse<Vocacao>(chave, ignoreCase: false, out var v) && Enum.IsDefined(v)
                && v.ToString() == chave && VocacaoArcanaCalculator.EhMagica(v);
            return magica ? null : "Só as Vocações mágicas (Feiticeiro, Adepto e Bruxo) têm elementos ligados.";
        }
        return ClassesPorVocacao.Existe(chave) ? null : $"Classe desconhecida: {chave}.";
    }

    /// <summary>O total de pontos de afinidade de um Círculo é um inteiro ≥ 0.</summary>
    public static string? MotivoParaRecusarTotal(int total) =>
        total < 0 ? "O total deve ser um inteiro maior ou igual a zero." : null;
}
