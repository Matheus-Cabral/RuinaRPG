namespace RuinaRPG.Infrastructure.Runes;

/// <summary>
/// Uma Runa na biblioteca do GM (Requisitos - Banco de Runas). Os campos são os mesmos de
/// CharacterRune/NpcRune — Nome, Descrição e Grau; toda Runa de ficha tem uma cópia independente aqui.
/// </summary>
public class RuneBankEntry
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Grau { get; set; }
}
