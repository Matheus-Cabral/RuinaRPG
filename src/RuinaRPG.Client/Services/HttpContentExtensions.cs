using System.Text.Json;

namespace RuinaRPG.Client.Services;

public static class HttpContentExtensions
{
    // ASP.NET Core's default output formatters pick how a bare BadRequest(string)/Unauthorized(string)
    // message gets serialized based on content negotiation (the request's Accept header, which the
    // Blazor HttpClient doesn't set) — in practice that means the body comes back as plain text
    // ("Mensagem."), not the JSON string literal ("\"Mensagem.\"") ReadFromJsonAsync<string>()
    // expects, and that mismatch throws and crashes the page. Reading the raw text first and only
    // unwrapping it as JSON when it actually looks JSON-quoted works either way.
    public static async Task<string?> ReadErrorMessageAsync(this HttpContent content)
    {
        var raw = await content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(raw))
            return null;

        if (raw.Length > 1 && raw[0] == '"')
        {
            try
            {
                return JsonSerializer.Deserialize<string>(raw);
            }
            catch (JsonException)
            {
                // Not actually JSON despite the leading quote — fall through and use it as-is.
            }
        }

        return raw;
    }
}
