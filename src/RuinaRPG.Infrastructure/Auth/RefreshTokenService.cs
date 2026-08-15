using System.Security.Cryptography;
using System.Text;
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Auth;

public class RefreshTokenService : IRefreshTokenService
{
    public (string plainTextToken, RefreshToken entity) Generate(Guid userId, int refreshTokenDays)
    {
        var plainTextToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(plainTextToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays)
        };

        return (plainTextToken, entity);
    }

    public string Hash(string plainTextToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainTextToken));
        return Convert.ToHexString(bytes);
    }
}
