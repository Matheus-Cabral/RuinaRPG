using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Tests.Unit.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateSut() => new(Options.Create(new JwtOptions
    {
        SigningKey = "unit-test-signing-key-needs-32-bytes-min",
        Issuer = "RuinaRPG.Tests",
        Audience = "RuinaRPG.Tests",
        AccessTokenMinutes = 15
    }));

    [Fact]
    public void CreateAccessToken_embeds_user_id_and_role_claims()
    {
        var sut = CreateSut();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Nickname = "TestGM",
            Role = UserRole.GM
        };

        var token = sut.CreateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == nameof(UserRole.GM));
    }
}
