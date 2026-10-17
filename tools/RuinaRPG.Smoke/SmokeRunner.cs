using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Smoke;

public static class SmokeRunner
{
    /// <summary>Envia cada GET com um token do usuário; devolve "&lt;status&gt; &lt;path&gt;" das falhas.</summary>
    public static async Task<IReadOnlyList<string>> RunAsync(
        HttpClient http, IJwtTokenService jwt, RuinaRpgDbContext db, IReadOnlyList<SmokeRequest> plan)
    {
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id);
        var failures = new List<string>();

        foreach (var r in plan)
        {
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
