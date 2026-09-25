using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class EstrelaSelectTests : MudBunitContext
{
    private readonly List<string> _requestedPaths = new();

    // Ficha de Personagem R0001 1.a: o popup mostra o cartão da Estrela vindo do Livro de Regras
    // (GET api/rulebook/estrelas-alkerianas), então uma edição do Auditor aparece nele.
    private void RegisterRulebookWith(params RulebookSectionResponse[] sections) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requestedPaths.Add(request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new RulebookDocumentResponse("estrelas-alkerianas", "As Estrelas", null, sections.ToList())),
            };
        }));

    // Same MudDialogProvider idiom as HistoricoSelectTests.
    private IRenderedComponent<ContainerFragment> RenderSelect(string? value) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<EstrelaSelect>(1);
            builder.AddAttribute(2, nameof(EstrelaSelect.Value), value);
            builder.CloseComponent();
        });

    [Fact]
    public void With_no_Estrela_selected_the_info_button_is_hidden()
    {
        RegisterRulebookWith();

        var cut = RenderSelect(null);

        cut.FindComponents<MudIconButton>().Should().BeEmpty();
    }

    [Fact]
    public void The_popup_shows_the_Estrelas_card_from_the_Livro_de_Regras()
    {
        RegisterRulebookWith(
            new RulebookSectionResponse("sina", "Sina", "<p>Texto da Sina.</p>", null),
            new RulebookSectionResponse("i-aeurer", "🌿 I — AEURER", "<p>Texto de Aeurer editado pelo Auditor.</p>", null));

        var cut = RenderSelect("Aeurer");
        cut.Find("button").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Texto de Aeurer editado pelo Auditor."));
        cut.Markup.Should().Contain("🌿 I — AEURER");
        cut.Markup.Should().NotContain("Texto da Sina.");
        _requestedPaths.Should().Equal("/api/rulebook/estrelas-alkerianas");
    }

    [Fact]
    public void Every_opening_fetches_the_card_again_so_an_edit_shows_up_without_reloading_the_sheet()
    {
        RegisterRulebookWith(new RulebookSectionResponse("i-aeurer", "🌿 I — AEURER", "<p>Aeurer.</p>", null));

        var cut = RenderSelect("Aeurer");
        cut.Find("button").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Aeurer."));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Fechar").Click();
        cut.Find("button").Click();

        cut.WaitForAssertion(() => _requestedPaths.Should().HaveCount(2));
    }

    [Fact]
    public void When_the_card_is_missing_from_the_Livro_de_Regras_the_popup_says_so()
    {
        RegisterRulebookWith(new RulebookSectionResponse("sina", "Sina", "<p>Texto da Sina.</p>", null));

        var cut = RenderSelect("Sadir");
        cut.Find("button").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Descrição não encontrada no Livro de Regras."));
    }
}
