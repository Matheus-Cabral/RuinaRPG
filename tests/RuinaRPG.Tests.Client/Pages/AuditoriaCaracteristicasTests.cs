using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class AuditoriaCaracteristicasTests : MudBunitContext
{
    // Info popups' inline <MudDialog> only renders its content through a MudDialogProvider present
    // elsewhere in the render tree (the real app has one in MainLayout) — same idiom as
    // ChangelogDialogTests/CatalogoItemPickerTests/BancoDeMagiasFormTests.
    private IRenderedComponent<ContainerFragment> RenderWithDialogProvider()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("traits")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        Services.AddScoped(_ => http);

        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaCaracteristicas>(1);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task Adicionar_form_has_an_info_popup_next_to_Exige_Especificacao_with_the_exact_help_text()
    {
        var cut = RenderWithDialogProvider();
        await Task.Delay(50);

        var infoButton = cut.Find("button[title='O que é Especificação']");
        infoButton.GetAttribute("aria-label").Should().Be("O que é Especificação");

        infoButton.Click();

        var content = TextNormalization.Collapse(cut.Find(".mud-dialog-content").TextContent);
        content.Should().Be(TextNormalization.Collapse(
            "Marque quando a característica pede que o jogador escolha ou nomeie algo ao recebê-la. Exemplos: qual sentido em Sentidos Aguçados, qual substância em Alergia. Nesse caso, a ficha passa a exigir o campo de texto Especificação para essa característica. Deixe desmarcado para características que valem sozinhas."));
    }
}
