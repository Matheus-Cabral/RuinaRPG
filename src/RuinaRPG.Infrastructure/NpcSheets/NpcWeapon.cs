using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcWeapon : IEquipavelDoCatalogo
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Guid ItemId { get; set; }
    public bool IsEquipped { get; set; }
    public int DurabilidadeAtual { get; set; }
}
