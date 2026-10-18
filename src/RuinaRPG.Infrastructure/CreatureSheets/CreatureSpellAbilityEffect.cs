using RuinaRPG.Infrastructure.Fichas;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSpellAbilityEffect : IEfeitoDaMagiaDaFicha
{
    public Guid Id { get; set; }
    public Guid CreatureSpellAbilityId { get; set; }
    public required string EfeitoNome { get; set; }
    public int? Quantidade { get; set; }
    public int CustoPI { get; set; }
}
