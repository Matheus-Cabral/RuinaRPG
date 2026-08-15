using Blazored.LocalStorage;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Client.Services;

public class AuthStateService(ILocalStorageService localStorage)
{
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    public async Task SetTokensAsync(AuthResponse response)
    {
        await localStorage.SetItemAsStringAsync(AccessTokenKey, response.AccessToken);
        await localStorage.SetItemAsStringAsync(RefreshTokenKey, response.RefreshToken);
    }

    public ValueTask<string?> GetAccessTokenAsync() => localStorage.GetItemAsStringAsync(AccessTokenKey);
    public ValueTask<string?> GetRefreshTokenAsync() => localStorage.GetItemAsStringAsync(RefreshTokenKey);

    public async Task ClearAsync()
    {
        await localStorage.RemoveItemAsync(AccessTokenKey);
        await localStorage.RemoveItemAsync(RefreshTokenKey);
    }
}
