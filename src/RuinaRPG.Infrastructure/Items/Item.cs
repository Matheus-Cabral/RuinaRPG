namespace RuinaRPG.Infrastructure.Items;

public abstract class Item
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public decimal Peso { get; set; }
    public int Preco { get; set; }
    public Guid? ImageId { get; set; }
}
