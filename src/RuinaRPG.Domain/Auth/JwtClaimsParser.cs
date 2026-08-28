using System.Text;
using System.Text.Json;

namespace RuinaRPG.Domain.Auth;

public static class JwtClaimsParser
{
    public static IReadOnlyDictionary<string, string> ParsePayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            throw new FormatException("Token JWT malformado: esperado 3 partes separadas por '.'.");

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));

        using var document = JsonDocument.Parse(payloadJson);
        var claims = new Dictionary<string, string>();
        foreach (var property in document.RootElement.EnumerateObject())
            claims[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()!
                : property.Value.GetRawText();

        return claims;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
