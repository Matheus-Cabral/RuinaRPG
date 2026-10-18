using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterInventoryItem : IItemDoInventarioDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Guid ItemId { get; set; }
    public int Qtd { get; set; }
}
