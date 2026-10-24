using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcAffection : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public required string Nome { get; set; }
    public int Favorabilidade { get; set; }
}
