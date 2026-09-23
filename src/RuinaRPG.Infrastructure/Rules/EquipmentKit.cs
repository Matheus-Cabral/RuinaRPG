namespace RuinaRPG.Infrastructure.Rules;

public class EquipmentKit
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Ciclos { get; set; }
    public bool IsDeleted { get; set; }
}
