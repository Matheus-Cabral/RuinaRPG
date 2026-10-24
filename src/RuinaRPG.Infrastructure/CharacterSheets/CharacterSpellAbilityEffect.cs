using RuinaRPG.Infrastructure.Fichas;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSpellAbilityEffect : IEfeitoDaMagiaDaFicha
{
    public Guid Id { get; set; }
    public Guid CharacterSpellAbilityId { get; set; }
    public required string EfeitoNome { get; set; }
    public int? Quantidade { get; set; }
    public int CustoPI { get; set; }
}
