using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Auth;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class TrocaSenhaObrigatoriaTests : MudBunitContext
{
    [Fact]
    public async Task Submitting_matching_passwords_posts_to_change_password_and_invokes_the_callback()
    {
        ChangePasswordRequest? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            captured = req.Content!.ReadFromJsonAsync<ChangePasswordRequest>().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        Services.AddScoped(_ => http);

        var trocada = 0;
        var cut = Render<TrocaSenhaObrigatoria>(p => p
            .Add(x => x.OnSenhaTrocada, EventCallback.Factory.Create(this, () => trocada++)));

        var novaSenha = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Nova senha");
        var confirmacao = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Confirmação da nova senha");
        await cut.InvokeAsync(() => novaSenha.Instance.ValueChanged.InvokeAsync("NovaSenha!123"));
        await cut.InvokeAsync(() => confirmacao.Instance.ValueChanged.InvokeAsync("NovaSenha!123"));

        cut.Find("button:contains('Salvar nova senha')").Click();

        captured.Should().NotBeNull();
        captured!.NovaSenha.Should().Be("NovaSenha!123");
        captured.ConfirmacaoNovaSenha.Should().Be("NovaSenha!123");
        trocada.Should().Be(1);
    }

    [Fact]
    public void A_failed_response_shows_an_error_and_does_not_invoke_the_callback()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("A confirmação de senha não confere com a nova senha."),
        });
        Services.AddScoped(_ => http);

        var trocada = 0;
        var cut = Render<TrocaSenhaObrigatoria>(p => p
            .Add(x => x.OnSenhaTrocada, EventCallback.Factory.Create(this, () => trocada++)));

        cut.Find("button:contains('Salvar nova senha')").Click();

        cut.Markup.Should().Contain("A confirmação de senha não confere com a nova senha.");
        trocada.Should().Be(0);
    }
}
