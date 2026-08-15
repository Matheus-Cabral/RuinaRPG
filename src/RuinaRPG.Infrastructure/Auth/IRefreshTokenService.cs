using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Auth;

public interface IRefreshTokenService
{
    (string plainTextToken, RefreshToken entity) Generate(Guid userId, int refreshTokenDays);
    string Hash(string plainTextToken);
}
