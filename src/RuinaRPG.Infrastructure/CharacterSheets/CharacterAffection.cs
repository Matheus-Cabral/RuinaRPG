namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterAffection
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public required string Nome { get; set; }
    public int Favorabilidade { get; set; }
}
