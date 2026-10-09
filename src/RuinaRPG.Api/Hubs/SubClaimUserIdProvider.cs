using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;

namespace RuinaRPG.Api.Hubs;

/// <summary>
/// SignalR's default provider reads ClaimTypes.NameIdentifier, but Program.cs keeps the JWT claim
/// types as JwtTokenService wrote them (MapInboundClaims = false), so the user id lives in "sub".
/// Without this, Clients.User(...) would never match any connection.
/// </summary>
public class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
