using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterWeapon : IEquipavelDoCatalogo
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Guid ItemId { get; set; }
    public bool IsEquipped { get; set; }
    public int DurabilidadeAtual { get; set; }
}
