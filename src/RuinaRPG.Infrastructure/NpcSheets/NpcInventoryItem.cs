using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcInventoryItem : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Guid ItemId { get; set; }
    public int Qtd { get; set; }
}
