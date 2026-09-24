using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class HistoricoSelectTests : MudBunitContext
{
    private static HttpClient ClientWithHistoricos() => FakeHttpMessageHandler.CreateClient(request =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[]
            {
                new
                {
                    Id = "hist-1",
                    Nome = "Órfão de Guerra",
                    Descricao = "Cresceu entre ruínas e perdas.",
                    PericiaMaisSeis = "ArmasBrancas",
                    PericiaMaisTres = "Sobrevivencia",
                    IsCustomized = false,
                },
            }),
        });

    // HistoricoSelect's inline <MudDialog> (now rendered through InfoPopup) only renders its content
    // through a MudDialogProvider present elsewhere in the render tree — same idiom as
    // ChangelogDialogTests/CatalogoItemPickerTests.
    private IRenderedComponent<ContainerFragment> RenderSelect(string? value) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<HistoricoSelect>(1);
            builder.AddAttribute(2, nameof(HistoricoSelect.Value), value);
            builder.CloseComponent();
        });

    [Fact]
    public async Task With_no_Historico_selected_the_info_button_is_hidden()
    {
        Services.AddScoped(_ => ClientWithHistoricos());

        var cut = RenderSelect(null);
        await Task.Delay(50);

        cut.FindComponents<MudIconButton>().Should().BeEmpty();
    }

    [Fact]
    public async Task With_a_Historico_selected_the_info_button_is_shown_and_opens_a_popup_with_Descricao_and_the_bonus_line()
    {
        Services.AddScoped(_ => ClientWithHistoricos());

        var cut = RenderSelect("hist-1");
        await Task.Delay(50);

        cut.FindComponents<MudIconButton>().Should().ContainSingle();

        cut.Find("button").Click();

        cut.Markup.Should().Contain("Órfão de Guerra");
        cut.Markup.Should().Contain("Cresceu entre ruínas e perdas.");
        cut.Markup.Should().Contain("+6");
        cut.Markup.Should().Contain("+3");
    }
}
