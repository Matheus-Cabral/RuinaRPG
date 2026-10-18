using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterMastery : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public required string Nome { get; set; }
    public int PericiaId { get; set; }
    public Atributo Atributo { get; set; }
    public int GastoMaestria { get; set; }
}
