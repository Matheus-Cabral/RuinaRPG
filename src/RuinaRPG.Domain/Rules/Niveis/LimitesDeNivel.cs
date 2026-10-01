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

    public static string? Passivas(CategoriaDePassiva categoria, int jaNaFicha, int? limite, int nivel) =>
        limite is null || jaNaFicha < limite
            ? null
            : $"O nível {nivel} permite no máximo {limite} Passiva(s) {Rotulo(categoria)}.";

    private static string Rotulo(CategoriaDePassiva categoria) => categoria switch
    {
        CategoriaDePassiva.Livre => "Livre(s)",
        CategoriaDePassiva.Vocacional => "Vocacional(is)",
        _ => "De Classe",
    };
}
