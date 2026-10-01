using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSkill
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public int PericiaId { get; set; }
    public int Gasto { get; set; }
    public AtributoCriatura? AtributoEscolhido { get; set; }
}
