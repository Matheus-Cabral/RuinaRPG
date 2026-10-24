using RuinaRPG.Infrastructure.Auth;

namespace RuinaRPG.Smoke;

public record SmokeSettings(string BaseUrl, string ConnectionString, JwtOptions Jwt)
{
    /// <summary>
    /// Prazo total do smoke (SMOKE_DEADLINE_SECONDS, padrão 4 min): abaixo do timeout que o deploy.sh
    /// impõe ao smoke inteiro (RUINA_SMOKE_TIMEOUT, 300 s), para que o relatório chegue antes do corte.
    /// </summary>
    public TimeSpan Deadline { get; init; } = TimeSpan.FromMinutes(4);

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
        var settings = new SmokeSettings(Required("SMOKE_BASE_URL"), connection, jwt);
        if (env.TryGetValue("SMOKE_DEADLINE_SECONDS", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            if (!int.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds) || seconds < 1)
                throw new InvalidOperationException($"SMOKE_DEADLINE_SECONDS deve ser um inteiro >= 1: {raw}");
            settings = settings with { Deadline = TimeSpan.FromSeconds(seconds) };
        }
        return settings;
    }
}
