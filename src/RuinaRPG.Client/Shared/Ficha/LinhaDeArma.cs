using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// Uma linha da tabela de Armas: o record compartilhado e a Durabilidade como a tela a mostra. A Durabilidade é anulável
/// porque a arma da Criatura pode não ter (o ataque natural, ou uma arma do Catálogo sem Durabilidade): ela aparece em
/// branco, nunca 0 (R-E2). O record compartilhado não a tem anulável e recebe 0 do adaptador; a tela não lê esse 0.
/// </summary>
public sealed record LinhaDeArma(WeaponResponse Arma, int? DurabilidadeAtual, int? DurabilidadeMaxima)
{
    public static LinhaDeArma De(WeaponResponse arma) => new(arma, arma.DurabilidadeAtual, arma.DurabilidadeMaxima);

    public static LinhaDeArma De(CreatureWeaponResponse arma) => new(AdaptadorDeCriatura.ParaArma(arma), arma.DurabilidadeAtual, arma.DurabilidadeMaximo);
}
