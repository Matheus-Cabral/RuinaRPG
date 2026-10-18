using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureMastery : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public required string Nome { get; set; }
    public int PericiaId { get; set; }
    public AtributoCriatura Atributo { get; set; }
    public int GastoMaestria { get; set; }
}
