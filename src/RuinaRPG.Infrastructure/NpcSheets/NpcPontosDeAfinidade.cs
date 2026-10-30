using RuinaRPG.Infrastructure.Fichas;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcPontosDeAfinidade : IPontosDeAfinidadeDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public int ElementoId { get; set; }
    public int Pontos { get; set; }
    public int? Experiencia { get; set; }
}
