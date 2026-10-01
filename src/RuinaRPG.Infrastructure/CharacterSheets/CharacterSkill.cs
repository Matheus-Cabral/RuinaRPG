using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSkill
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public int PericiaId { get; set; }
    public int Gasto { get; set; }
    public Atributo? AtributoEscolhido { get; set; }
}
