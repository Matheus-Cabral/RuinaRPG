using FluentAssertions;
using RuinaRPG.Smoke;

namespace RuinaRPG.Tests.Unit.Smoke;

public class SmokeSettingsTests
{
    private static Dictionary<string, string?> Env() => new()
    {
        ["SMOKE_BASE_URL"] = "http://api-blue:8080", ["POSTGRES_USER"] = "u", ["POSTGRES_PASSWORD"] = "p",
        ["JWT_SIGNING_KEY"] = new string('k', 40), ["JWT_ISSUER"] = "i", ["JWT_AUDIENCE"] = "a",
    };

    [Fact]
    public void Reads_settings_with_defaults_for_host_and_database()
    {
        var s = SmokeSettings.FromEnvironment(Env());
        s.BaseUrl.Should().Be("http://api-blue:8080");
        s.ConnectionString.Should().Contain("Host=db").And.Contain("Database=ruinarpg");
        s.Jwt.Issuer.Should().Be("i");
    }

    [Fact]
    public void Names_the_missing_variable()
    {
        var env = Env(); env.Remove("JWT_SIGNING_KEY");
        var act = () => SmokeSettings.FromEnvironment(env);
        act.Should().Throw<InvalidOperationException>().WithMessage("*JWT_SIGNING_KEY*");
    }

    [Fact]
    public void Deadline_defaults_to_four_minutes_and_can_be_set_in_seconds()
    {
        SmokeSettings.FromEnvironment(Env()).Deadline.Should().Be(TimeSpan.FromMinutes(4));
        var env = Env(); env["SMOKE_DEADLINE_SECONDS"] = "90";
        SmokeSettings.FromEnvironment(env).Deadline.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("-5")]
    public void Rejects_an_invalid_deadline(string value)
    {
        var env = Env(); env["SMOKE_DEADLINE_SECONDS"] = value;
        var act = () => SmokeSettings.FromEnvironment(env);
        act.Should().Throw<InvalidOperationException>().WithMessage("*SMOKE_DEADLINE_SECONDS*");
    }
}
