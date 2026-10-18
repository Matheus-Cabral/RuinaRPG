using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSpoil : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Guid ItemId { get; set; }
    public int Qtd { get; set; }
    public int DT { get; set; }
}
