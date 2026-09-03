using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterAffinity
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Elemento? Elemento { get; set; }
    public int? ElementoValor { get; set; }
    public SubElemento? SubElemento { get; set; }
    public int? SubElementoValor { get; set; }
    public string? CaminhoNome { get; set; }
    public int? Experiencia { get; set; }
}
