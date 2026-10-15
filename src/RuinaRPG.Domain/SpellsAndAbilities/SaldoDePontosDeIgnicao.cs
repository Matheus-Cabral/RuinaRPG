namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// Ficha de Personagem 4.b: montar ou adicionar uma Magia/Habilidade consome o Gasto em PI do saldo
/// da ficha, remover devolve, e editar acerta só a diferença. Vale para todos, GM incluído.
/// </summary>
public static class SaldoDePontosDeIgnicao
{
    public static bool Consome(SpellAbilityTipo tipo) => tipo is SpellAbilityTipo.Magia or SpellAbilityTipo.Habilidade;

    public static string? ErroAoDebitar(int saldo, int gasto) =>
        gasto > saldo ? $"Pontos de Ignição insuficientes: custa {gasto} e a ficha tem {saldo}." : null;

    public static int Debitar(int saldo, int gasto) => saldo - gasto;
}
