namespace RuinaRPG.Infrastructure.Campaigns;

public class Campaign
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public Guid? ImageId { get; set; }
}
