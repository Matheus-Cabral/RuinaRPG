using RuinaRPG.Infrastructure.Fichas;
namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureAffection : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public required string Nome { get; set; }
    public int Favorabilidade { get; set; }
}
