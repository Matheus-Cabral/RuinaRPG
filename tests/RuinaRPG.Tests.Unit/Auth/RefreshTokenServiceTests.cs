using FluentAssertions;
using RuinaRPG.Infrastructure.Auth;

namespace RuinaRPG.Tests.Unit.Auth;

public class RefreshTokenServiceTests
{
    [Fact]
    public void Generate_returns_a_plaintext_token_whose_hash_matches_the_entity()
    {
        var sut = new RefreshTokenService();
        var userId = Guid.NewGuid();

        var (plainTextToken, entity) = sut.Generate(userId, refreshTokenDays: 30);

        entity.UserId.Should().Be(userId);
        entity.TokenHash.Should().Be(sut.Hash(plainTextToken));
        entity.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        entity.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Hash_is_deterministic_for_the_same_input()
    {
        var sut = new RefreshTokenService();

        sut.Hash("same-token").Should().Be(sut.Hash("same-token"));
    }
}
