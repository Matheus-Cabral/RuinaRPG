using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Domain.Rules.Niveis;

/// <summary>Tetos por nível da Tabela de Níveis (Máx. de Atributo/Perícia/Passivas). Null = permitido; senão, a mensagem do 400.</summary>
public static class LimitesDeNivel
{
    // Reduzir (ou manter) um valor que já passou do teto é sempre permitido: o teto pode ter sido
    // editado depois, e bloquear a redução trancaria a ficha num estado inválido.
    public static string? Gasto(string rotulo, int gastoAtual, int gastoNovo, int? limite, int nivel) =>
        limite is null || gastoNovo <= limite || gastoNovo <= gastoAtual
            ? null
            : $"{rotulo} não pode passar de {limite} pontos no nível {nivel}.";

    /// <summary>
    /// Limite de Passivas ao adicionar uma de <paramref name="categoria"/>. Cada categoria tem o próprio limite
    /// (coluna inteiramente vazia = sem limite); o que passa dele ocupa vagas da coluna Passivas Coringa, que
    /// servem a qualquer categoria. Nada é marcado na Passiva: a conta é refeita a cada adição.
    /// </summary>
    public static string? Passivas(CategoriaDePassiva categoria, IReadOnlyCollection<CategoriaDePassiva> naFicha, ProgressaoDeNivel tabela, int nivel)
    {
        int? Limite(CategoriaDePassiva c) => tabela.LimiteAcumulado(ChavesDeNivel.MaxPassivas(c), nivel);

        // Sem limite na categoria, ou ainda dentro dele: não precisa de coringa.
        if (Limite(categoria) is not { } limite || naFicha.Count(c => c == categoria) < limite)
            return null;

        var coringas = tabela.LimiteAcumulado(ChavesDeNivel.MaxPassivasCoringa, nivel) ?? 0;
        var emUso = Enum.GetValues<CategoriaDePassiva>()
            .Sum(c => Limite(c) is { } l ? Math.Max(0, naFicha.Count(x => x == c) - l) : 0);
        if (emUso < coringas)
            return null;

        var daCategoria = $"O nível {nivel} permite no máximo {limite} Passiva(s) {Rotulo(categoria)}";
        return coringas == 0 ? daCategoria + "." : $"{daCategoria}, e as {coringas} vaga(s) de Habilidade Passiva já estão em uso.";
    }

    private static string Rotulo(CategoriaDePassiva categoria) => categoria switch
    {
        CategoriaDePassiva.Livre => "Livre(s)",
        CategoriaDePassiva.Vocacional => "Vocacional(is)",
        _ => "De Classe",
    };
}
