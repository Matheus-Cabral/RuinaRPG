using System.Net;
using Blazored.LocalStorage;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Auth;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class BearerTokenHandlerTests
{
    [Fact]
    public async Task A_401_on_an_authenticated_request_clears_tokens_and_redirects_to_login()
    {
        var localStorage = new InMemoryLocalStorageService();
        var authState = new AuthStateService(localStorage);
        await authState.SetTokensAsync(new AuthResponse("expired-access-token", "some-refresh-token"));
        var authProvider = new TokenAuthenticationStateProvider(authState);
        var navigation = new FakeNavigationManager();
        var handler = new BearerTokenHandler(authState, navigation, authProvider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.Unauthorized)
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        await client.GetAsync("character-sheets/mine");

        (await authState.GetAccessTokenAsync()).Should().BeNull();
        (await authState.GetRefreshTokenAsync()).Should().BeNull();
        navigation.NavigatedTo.Should().Be("/login?sessaoExpirada=true");
    }

    [Fact]
    public async Task A_401_with_no_token_ever_sent_is_left_alone_so_a_wrong_password_on_Login_does_not_trigger_a_redirect()
    {
        var localStorage = new InMemoryLocalStorageService();
        var authState = new AuthStateService(localStorage);
        var authProvider = new TokenAuthenticationStateProvider(authState);
        var navigation = new FakeNavigationManager();
        var handler = new BearerTokenHandler(authState, navigation, authProvider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.Unauthorized)
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        await client.PostAsync("auth/login", null);

        navigation.NavigatedTo.Should().BeNull();
    }

    [Fact]
    public async Task A_successful_authenticated_request_never_redirects()
    {
        var localStorage = new InMemoryLocalStorageService();
        var authState = new AuthStateService(localStorage);
        await authState.SetTokensAsync(new AuthResponse("valid-access-token", "some-refresh-token"));
        var authProvider = new TokenAuthenticationStateProvider(authState);
        var navigation = new FakeNavigationManager();
        var handler = new BearerTokenHandler(authState, navigation, authProvider)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK)
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        await client.GetAsync("character-sheets/mine");

        (await authState.GetAccessTokenAsync()).Should().Be("valid-access-token");
        navigation.NavigatedTo.Should().BeNull();
    }

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode));
    }

    /// <summary>
    /// Minimal deterministic NavigationManager double, deliberately not routed through bUnit's own
    /// FakeNavigationManager — this test exercises plain service/handler logic, not a rendered
    /// component, so a self-contained fake keeps it independent of bUnit's render pipeline.
    /// </summary>
    private sealed class FakeNavigationManager : NavigationManager
    {
        public FakeNavigationManager() => Initialize("https://localhost/", "https://localhost/");

        public string? NavigatedTo { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options) => NavigatedTo = uri;
    }

    /// <summary>
    /// Minimal in-memory ILocalStorageService double. Blazored.LocalStorage's real implementation
    /// goes through JS interop, which bUnit's fake JSRuntime can't transparently round-trip in
    /// Loose mode (an unconfigured Get after a Set just returns default, not the stored value) —
    /// so AuthStateService needs a real backing dictionary here to make Clear-actually-clears
    /// assertions meaningful.
    /// </summary>
    private sealed class InMemoryLocalStorageService : ILocalStorageService
    {
        private readonly Dictionary<string, string> _store = new();

#pragma warning disable CS0067 // required by ILocalStorageService; this fake never raises them
        public event EventHandler<ChangingEventArgs>? Changing;
        public event EventHandler<ChangedEventArgs>? Changed;
#pragma warning restore CS0067

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            _store.Clear();
            return ValueTask.CompletedTask;
        }

        public ValueTask<T?> GetItemAsync<T>(string key, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used by AuthStateService.");

        public ValueTask<string?> GetItemAsStringAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_store.TryGetValue(key, out var value) ? value : null);

        public ValueTask<string?> KeyAsync(int index, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<string?>(_store.Keys.ElementAt(index));

        public ValueTask<IEnumerable<string>> KeysAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IEnumerable<string>>(_store.Keys.ToList());

        public ValueTask<bool> ContainKeyAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_store.ContainsKey(key));

        public ValueTask<int> LengthAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_store.Count);

        public ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveItemsAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            foreach (var key in keys)
                _store.Remove(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask SetItemAsync<T>(string key, T data, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used by AuthStateService.");

        public ValueTask SetItemAsStringAsync(string key, string data, CancellationToken cancellationToken = default)
        {
            _store[key] = data;
            return ValueTask.CompletedTask;
        }
    }
}
