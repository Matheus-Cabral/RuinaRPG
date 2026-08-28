using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using RuinaRPG.Domain.Auth;

namespace RuinaRPG.Client.Services;

public class TokenAuthenticationStateProvider(AuthStateService authState) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal AnonymousPrincipal = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await authState.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(AnonymousPrincipal);

        IReadOnlyDictionary<string, string> payload;
        try
        {
            payload = JwtClaimsParser.ParsePayload(token);
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or InvalidOperationException)
        {
            await authState.ClearAsync();
            return new AuthenticationState(AnonymousPrincipal);
        }

        var claims = new List<Claim>();
        if (payload.TryGetValue("sub", out var sub))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        if (payload.TryGetValue("role", out var role))
            claims.Add(new Claim(ClaimTypes.Role, role));
        if (payload.TryGetValue("nickname", out var nickname))
            claims.Add(new Claim(ClaimTypes.Name, nickname));

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyUserChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
