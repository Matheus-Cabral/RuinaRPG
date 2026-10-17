using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Smoke;

public static class SmokeRunner
{
    /// <summary>Envia cada GET com um token do usuário; devolve "&lt;status&gt; &lt;path&gt;" das falhas.</summary>
    public static async Task<IReadOnlyList<string>> RunAsync(
        HttpClient http, IJwtTokenService jwt, RuinaRpgDbContext db, IReadOnlyList<SmokeRequest> plan,
        TimeSpan deadline, TimeProvider? clock = null)
    {
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id);
        return await RunAsync(http, jwt, users, plan, deadline, clock ?? TimeProvider.System);
    }

    /// <summary>
    /// Como acima, com os usuários já carregados. Ao estourar o prazo total nenhuma requisição nova é
    /// enviada: o deploy já trocou o nginx e espera o smoke, então os paths restantes viram falhas
    /// "0 &lt;path&gt; (prazo)" em vez de prender o deploy.
    /// </summary>
    public static async Task<IReadOnlyList<string>> RunAsync(
        HttpClient http, IJwtTokenService jwt, IReadOnlyDictionary<Guid, ApplicationUser> users,
        IReadOnlyList<SmokeRequest> plan, TimeSpan deadline, TimeProvider clock)
    {
        var failures = new List<string>();
        var stopAt = clock.GetUtcNow() + deadline;

        foreach (var r in plan)
        {
            if (clock.GetUtcNow() >= stopAt)
            {
                failures.Add($"0 {r.Path} (prazo)");
                continue;
            }

            if (!users.TryGetValue(r.UserId, out var user))
            {
                failures.Add($"0 {r.Path}");
                continue;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, r.Path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt.CreateAccessToken(user));
            try
            {
                using var response = await http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode != 200 || !IsJson(body))
                    failures.Add($"{(int)response.StatusCode} {r.Path}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                failures.Add($"0 {r.Path}");
            }
        }

        return failures;
    }

    private static bool IsJson(string body)
    {
        try { using var _ = JsonDocument.Parse(body); return true; }
        catch (JsonException) { return false; }
    }
}
