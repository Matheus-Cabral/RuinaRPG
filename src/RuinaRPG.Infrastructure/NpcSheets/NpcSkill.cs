using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcSkill : IPericiaDaFicha<Atributo>
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public int PericiaId { get; set; }
    public int Gasto { get; set; }
    public Atributo? AtributoEscolhido { get; set; }
}
