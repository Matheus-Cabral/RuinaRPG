using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcSkill
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public int PericiaId { get; set; }
    public int Gasto { get; set; }
    public Atributo? AtributoEscolhido { get; set; }
}
