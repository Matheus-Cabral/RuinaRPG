using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcAttribute : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Atributo Atributo { get; set; }
    public int Gasto { get; set; }
    public int Bonus { get; set; }
    public bool TemMaestria { get; set; }
}
