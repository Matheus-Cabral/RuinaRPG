using RuinaRPG.Infrastructure.Auth;

namespace RuinaRPG.Smoke;

public record SmokeSettings(string BaseUrl, string ConnectionString, JwtOptions Jwt)
{
    public static SmokeSettings FromEnvironment(IReadOnlyDictionary<string, string?> env)
    {
        string Required(string name) =>
            env.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : throw new InvalidOperationException($"Variável de ambiente ausente: {name}");
        string Optional(string name, string fallback) =>
            env.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

        var connection = $"Host={Optional("POSTGRES_HOST", "db")};Database={Optional("POSTGRES_DB", "ruinarpg")};" +
                         $"Username={Required("POSTGRES_USER")};Password={Required("POSTGRES_PASSWORD")}";
        var jwt = new JwtOptions
        {
            SigningKey = Required("JWT_SIGNING_KEY"),
            Issuer = Required("JWT_ISSUER"),
            Audience = Required("JWT_AUDIENCE"),
        };
        return new SmokeSettings(Required("SMOKE_BASE_URL"), connection, jwt);
    }
}
