namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcSpellAbilityEffect
{
    public Guid Id { get; set; }
    public Guid NpcSpellAbilityId { get; set; }
    public required string EfeitoNome { get; set; }
    public int? Quantidade { get; set; }
    public int CustoPI { get; set; }
}
