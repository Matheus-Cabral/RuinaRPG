namespace RuinaRPG.Contracts.Invites;

public record InviteCodeResponse(
    string Code,
    string Status,
    DateTime GeneratedAt,
    DateTime? ExpiresAt,
    string? RedeemedByNickname,
    string? RedeemedByEmail,
    DateTime? RedeemedAt);
