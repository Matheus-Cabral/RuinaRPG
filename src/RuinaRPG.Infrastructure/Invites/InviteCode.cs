namespace RuinaRPG.Infrastructure.Invites;

public class InviteCode
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public Guid GmId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RedeemedByUserId { get; set; }
    public DateTime? RedeemedAt { get; set; }
}
