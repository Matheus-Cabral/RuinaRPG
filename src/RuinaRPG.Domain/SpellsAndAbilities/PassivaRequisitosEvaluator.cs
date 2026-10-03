using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// Compara os requisitos de uma Passiva com uma ficha. Devolve uma pendência legível por requisito não
/// cumprido, na ordem dos campos; lista vazia = cumpre tudo. <paramref name="nomeDoHistoricoExigido"/> é o
/// nome do Histórico do requisito (nulo se ele não existe mais no catálogo); <paramref name="nomeDaPericia"/>
/// devolve o nome de exibição de uma Perícia a partir do Id (a tabela Pericias vive na Infrastructure).
/// </summary>
public static class PassivaRequisitosEvaluator
{
    public static IReadOnlyList<string> Pendencias(RequisitosDePassiva? requisitos, FichaParaRequisitos ficha, string? nomeDoHistoricoExigido, Func<int, string> nomeDaPericia)
    {
        var pendencias = new List<string>();
        if (requisitos is null)
            return pendencias;

        if (requisitos.Nivel is { } nivel && ficha.Nivel < nivel)
            pendencias.Add($"Nível {nivel}");

        if (ficha.TemIdentidadeDePersonagem)
        {
            if (requisitos.Vocacao is { } vocacao && ficha.Vocacao != vocacao)
                pendencias.Add($"Vocação: {RequisitoLabels.Vocacao(vocacao)}");
            if (!string.IsNullOrWhiteSpace(requisitos.Classe)
                && !string.Equals(requisitos.Classe.Trim(), ficha.Classe?.Trim(), StringComparison.OrdinalIgnoreCase))
                pendencias.Add($"Classe: {requisitos.Classe.Trim()}");
            if (requisitos.Linhagem is { } linhagem && ficha.Linhagem != linhagem)
                pendencias.Add($"Linhagem: {RequisitoLabels.Linhagem(linhagem)}");
            if (requisitos.Variante is { } variante && ficha.Variante != variante)
                pendencias.Add($"Variante: {RequisitoLabels.Variante(variante)}");
            if (requisitos.Graduacao is { } graduacao && ficha.Graduacao < graduacao)
                pendencias.Add($"Grau/Círculo {graduacao}");
            if (requisitos.CoracaoDeMana == true && !ficha.PossuiCoracaoDeMana)
                pendencias.Add("Coração de Mana");
        }

        if (requisitos.Afinidade is { } afinidade && ficha.Afinidade != afinidade)
            pendencias.Add($"Afinidade: {RequisitoLabels.Afinidade(afinidade)}");

        if (ficha.TemIdentidadeDePersonagem)
        {
            if (requisitos.Estrela is { } estrela && ficha.Estrela != estrela)
                pendencias.Add($"Estrela: {estrela}");
            if (requisitos.HistoricoId is { } historicoId && ficha.HistoricoId != historicoId)
                pendencias.Add($"Histórico: {nomeDoHistoricoExigido ?? "(removido)"}");
        }

        foreach (var r in requisitos.Atributos)
            if (ficha.Atributos.TryGetValue(r.Atributo, out var total) && total < r.Minimo)
                pendencias.Add($"{RequisitoLabels.Atributo(r.Atributo)} ≥ {r.Minimo}");

        foreach (var r in requisitos.SubAtributos)
            if (ficha.SubAtributos.TryGetValue(r.SubAtributo, out var valor) && valor < r.Minimo)
                pendencias.Add($"{RequisitoLabels.SubAtributo(r.SubAtributo)} ≥ {r.Minimo}");

        foreach (var r in requisitos.Pericias)
            if (ficha.Pericias.TryGetValue(r.Pericia, out var total) && (total is null || total < r.Minimo))
                pendencias.Add($"{nomeDaPericia(r.Pericia)} ≥ {r.Minimo}");

        return pendencias;
    }

    /// <summary>
    /// Os requisitos por extenso, na ordem dos campos e com os mesmos rótulos de <see cref="Pendencias"/> —
    /// para o Livro de Regras, que não tem ficha para comparar. Uma Perícia sem nome (removida) é omitida.
    /// </summary>
    public static IReadOnlyList<string> Descrever(RequisitosDePassiva? requisitos, string? nomeDoHistoricoExigido, Func<int, string?> nomeDaPericia)
    {
        var itens = new List<string>();
        if (requisitos is null)
            return itens;

        if (requisitos.Nivel is { } nivel)
            itens.Add($"Nível {nivel}");
        if (requisitos.Vocacao is { } vocacao)
            itens.Add($"Vocação: {RequisitoLabels.Vocacao(vocacao)}");
        if (!string.IsNullOrWhiteSpace(requisitos.Classe))
            itens.Add($"Classe: {requisitos.Classe.Trim()}");
        if (requisitos.Linhagem is { } linhagem)
            itens.Add($"Linhagem: {RequisitoLabels.Linhagem(linhagem)}");
        if (requisitos.Variante is { } variante)
            itens.Add($"Variante: {RequisitoLabels.Variante(variante)}");
        if (requisitos.Graduacao is { } graduacao)
            itens.Add($"Grau/Círculo {graduacao}");
        if (requisitos.CoracaoDeMana == true)
            itens.Add("Coração de Mana");
        if (requisitos.Afinidade is { } afinidade)
            itens.Add($"Afinidade: {RequisitoLabels.Afinidade(afinidade)}");
        if (requisitos.Estrela is { } estrela)
            itens.Add($"Estrela: {estrela}");
        if (requisitos.HistoricoId is not null)
            itens.Add($"Histórico: {nomeDoHistoricoExigido ?? "(removido)"}");

        itens.AddRange(requisitos.Atributos.Select(r => $"{RequisitoLabels.Atributo(r.Atributo)} ≥ {r.Minimo}"));
        itens.AddRange(requisitos.SubAtributos.Select(r => $"{RequisitoLabels.SubAtributo(r.SubAtributo)} ≥ {r.Minimo}"));
        foreach (var r in requisitos.Pericias)
            if (nomeDaPericia(r.Pericia) is { } nome)
                itens.Add($"{nome} ≥ {r.Minimo}");

        return itens;
    }
}
