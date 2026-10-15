using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class MinhasCampanhasTests : MudBunitContext
{
    /// <summary>Campanha R0015: each campaign card shows how many Notas Secretas are still unread there.</summary>
    [Fact]
    public async Task Campaign_card_shows_its_unread_secret_notes_and_only_where_there_are_some()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("jogador");
        authContext.SetRoles("Jogador");
        var campaigns = new List<CampaignResponse>
        {
            new("c1", "Ruína", "", null),
            new("c2", "Outra", "", null),
        };
        var unread = new List<UnreadSecretNotesResponse> { new("c1", "Ruína", 2) };
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
            request.RequestUri!.AbsolutePath.EndsWith("secret-notes/unread")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(unread) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(campaigns) }));
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync();

        var cut = Render<MinhasCampanhas>();

        cut.WaitForAssertion(() =>
        {
            var chips = cut.FindAll(".rr-unread-chip");
            chips.Should().ContainSingle();
            chips[0].TextContent.Should().Contain("2 notas secretas não lidas");
            chips[0].Closest(".mud-card")!.TextContent.Should().Contain("Ruína");
        });
    }
}
