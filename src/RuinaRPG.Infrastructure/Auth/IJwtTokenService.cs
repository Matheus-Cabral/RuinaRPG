using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Auth;

public interface IJwtTokenService
{
    string CreateAccessToken(ApplicationUser user);
}
