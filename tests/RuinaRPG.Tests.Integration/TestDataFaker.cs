using Bogus;

namespace RuinaRPG.Tests.Integration;

/// <summary>
/// Requisitos - Técnico R0003 lists Bogus as part of the required test stack. Registration
/// endpoints require a unique Nickname/Email per call, and this suite's own history is full of
/// hand-incremented suffixes ("Gm7", "Gm8", ...) colliding across test files sharing the same
/// database — a realistic-looking but GUID-suffixed value from here is guaranteed collision-free
/// without that bookkeeping, for any NEW test that wants one.
/// </summary>
public static class TestDataFaker
{
    private static readonly Faker Faker = new("pt_BR");

    /// <summary>A realistic-looking nickname, always unique across the whole test run.</summary>
    public static string UniqueNickname() => $"{Faker.Name.FirstName()}{Guid.NewGuid():N}"[..24];

    /// <summary>
    /// A realistic-looking, always-unique email address. Deliberately does NOT use any Bogus
    /// Internet.* generator for the host — the pt_BR locale draws domain words from person names
    /// and can come back accented (e.g. "sílvia.name"), which Identity's default
    /// AllowedUserNameCharacters rejects outright (UserName is set to Email in this app). A fixed
    /// ASCII domain, matching every other test file's "@teste.com" convention, keeps the address
    /// realistic-looking while staying always valid.
    /// </summary>
    public static string UniqueEmail() => $"{Guid.NewGuid():N}@teste.com";
}
