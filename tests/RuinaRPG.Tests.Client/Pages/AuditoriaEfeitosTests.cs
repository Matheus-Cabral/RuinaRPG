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

public class AuditoriaEfeitosTests : MudBunitContext
{
    // Info popups' inline <MudDialog> only renders its content through a MudDialogProvider present
    // elsewhere in the render tree (the real app has one in MainLayout) — same idiom as
    // ChangelogDialogTests/CatalogoItemPickerTests/BancoDeMagiasFormTests.
    private IRenderedComponent<ContainerFragment> RenderWithDialogProvider()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("efeitos")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        Services.AddScoped(_ => http);

        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaEfeitos>(1);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task Adicionar_Efeito_section_has_an_info_popup_with_the_exact_help_text()
    {
        var cut = RenderWithDialogProvider();
        await Task.Delay(50);

        var infoButton = cut.Find("button[title='Como cadastrar um Efeito']");
        infoButton.GetAttribute("aria-label").Should().Be("Como cadastrar um Efeito");

        infoButton.Click();

        var content = TextNormalization.Collapse(cut.Find(".mud-dialog-content").TextContent);
        content.Should().Be(TextNormalization.Collapse(string.Join(" ",
            "Cada Efeito pertence a um Grau/Círculo e fica disponível para Magias desse Grau em diante. O Tipo de Custo define como o PI é calculado:",
            "Fixo: sempre o valor de Custo Fixo.",
            "Por Unidade: Custo por Unidade × a quantidade escolhida na Magia. O Rótulo da Unidade é o nome dessa unidade, por exemplo \"Dado\".",
            "Manual: o Mestre digita o custo total na hora.",
            "Manual por Unidade: o Mestre digita a taxa, que é multiplicada pela quantidade.",
            "Derivado de outro Efeito: Custo por Unidade × a quantidade do Efeito de origem, informado pelo nome.",
            "O Teto de Unidades limita a quantidade. Com \"escala por Grau\", o teto é multiplicado pelo Grau da Magia, contando a partir do Grau informado, se houver. O Custo Alternativo substitui o custo normal a partir do Grau indicado. Nos Pré-requisitos, separe grupos com ; e alternativas com ,. Por exemplo, Duração;Atordoamento,Congelar significa \"exige Duração e (Atordoamento ou Congelar)\". O catálogo vale para todo o servidor.")));
    }
}
