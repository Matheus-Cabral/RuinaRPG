namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Quais Escolas de Magia cada Vocação libera — mapeamento dado pelo usuário, sem equivalente em
/// nenhum documento do sistema (ver docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
/// Campeão e Caçador (ausentes do dicionário) e uma Vocação nula não liberam Escola nenhuma.
/// </summary>
public static class VocacaoEscolaMap
{
    private static readonly Dictionary<Vocacao, EscolaDeMagia[]> Escolas = new()
    {
        [Vocacao.Feiticeiro] = [EscolaDeMagia.Dobra, EscolaDeMagia.Maculacao],
        [Vocacao.Adepto] = [EscolaDeMagia.Dobra, EscolaDeMagia.Consagracao],
        [Vocacao.Bruxo] = [EscolaDeMagia.Dobra, EscolaDeMagia.Transmutacao],
    };

    public static bool PodeEscolherElemento(Vocacao? vocacao, Elemento elemento) =>
        vocacao is { } v && Escolas.TryGetValue(v, out var escolas) && escolas.Contains(EscolaDeMagiaCatalog.DoElemento(elemento));

    public static bool PodeEscolherSubElemento(Vocacao? vocacao, SubElemento subElemento) =>
        vocacao is { } v && Escolas.TryGetValue(v, out var escolas) && escolas.Contains(EscolaDeMagiaCatalog.DoSubElemento(subElemento));

    /// <summary>
    /// AfinidadeElemental (o dropdown único de 1.a) compartilha os mesmos nomes de membro que
    /// Elemento (4) e SubElemento (14) — resolve pra qual dos dois enums o valor pertence antes
    /// de checar a Escola.
    /// </summary>
    public static bool PodeEscolherAfinidade(Vocacao? vocacao, AfinidadeElemental afinidade) =>
        Enum.TryParse<Elemento>(afinidade.ToString(), out var elemento)
            ? PodeEscolherElemento(vocacao, elemento)
            : PodeEscolherSubElemento(vocacao, Enum.Parse<SubElemento>(afinidade.ToString()));
}
