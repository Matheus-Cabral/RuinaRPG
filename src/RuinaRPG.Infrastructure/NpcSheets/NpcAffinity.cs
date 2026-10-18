using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcAffinity : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public Elemento? Elemento { get; set; }
    public int? ElementoValor { get; set; }
    public SubElemento? SubElemento { get; set; }
    public int? SubElementoValor { get; set; }
    public EssenciaBasica? SegundaEssencia { get; set; }
    public int? SegundaEssenciaValor { get; set; }
    public int? Experiencia { get; set; }
}
