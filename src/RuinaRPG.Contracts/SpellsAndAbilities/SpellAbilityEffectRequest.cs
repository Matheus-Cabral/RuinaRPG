using System.ComponentModel.DataAnnotations;

namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record SpellAbilityEffectRequest(string EfeitoNome, int? Quantidade, [Range(0, int.MaxValue)] int CustoPI);
