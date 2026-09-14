using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace RuinaRPG.Client.Services;

public class BearerTokenHandler(
    AuthStateService authState,
    NavigationManager navigation,
    TokenAuthenticationStateProvider authProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await authState.GetAccessTokenAsync();
        var hadToken = !string.IsNullOrWhiteSpace(accessToken);
        if (hadToken)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await base.SendAsync(request, cancellationToken);

        // Only a 401 on a request that actually carried a token means "the access token expired
        // or was rejected" — a 401 with no token attached is a normal failed-login attempt (wrong
        // nickname/senha on the anonymous auth/login endpoint) and must not trigger a logout.
        if (hadToken && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await authState.ClearAsync();
            authProvider.NotifyUserChanged();
            navigation.NavigateTo("/login?sessaoExpirada=true");
        }

        return response;
    }
}
