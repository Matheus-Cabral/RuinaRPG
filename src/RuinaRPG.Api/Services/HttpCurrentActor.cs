using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Api.Services;

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
