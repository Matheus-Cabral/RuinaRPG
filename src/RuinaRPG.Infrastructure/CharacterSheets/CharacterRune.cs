namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterRune
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Grau { get; set; }
}
