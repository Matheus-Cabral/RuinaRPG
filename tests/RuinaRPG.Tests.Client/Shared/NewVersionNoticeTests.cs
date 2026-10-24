using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class NewVersionNoticeTests : MudBunitContext
{
    private sealed class FixedHandler(string sha) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new VersionResponse("2.0.0", sha)) });
    }

    [Fact]
    public async Task Shows_a_snackbar_with_a_reload_action_when_a_new_version_appears()
    {
        var watcher = new NewVersionWatcher(new HttpClient(new FixedHandler("bbbbbbb")) { BaseAddress = new Uri("https://localhost/") }, "aaaaaaa");
        Services.AddSingleton(watcher);
        var root = Render(b =>
        {
            b.OpenComponent<MudSnackbarProvider>(0);
            b.CloseComponent();
            b.OpenComponent<NewVersionNotice>(1);
            b.CloseComponent();
        });

        await watcher.CheckAsync();

        root.WaitForAssertion(() =>
        {
            root.Markup.Should().Contain("Nova versão disponível");
            root.Markup.Should().Contain("Recarregar");
        });
    }
}
