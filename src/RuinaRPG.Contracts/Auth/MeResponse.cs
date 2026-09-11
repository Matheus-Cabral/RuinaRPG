namespace RuinaRPG.Contracts.Auth;

public record MeResponse(string Id, string Nickname, string Role, bool IsRulesAuditor);
