using Bunit;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class AuditoriaLivroDeRegrasTests : MudBunitContext
{
    [Fact]
    public void Every_editable_document_is_labeled_with_its_Livro_de_Regras_tab_name_not_its_slug()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<RulebookDocumentOverrideResponse>
            {
                new("sistema-basico", "# Sistema", true),
                new("graus-e-circulos", "# Graus", true),
                new("tabela-de-niveis", "| Nível |", true),
                new("estrelas-alkerianas", "# Sina", true),
            }),
        }));

        var cut = Render<AuditoriaLivroDeRegras>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("As Estrelas"));
        cut.Markup.Should().Contain("Sistema Básico").And.Contain("Graus &amp; Círculos").And.Contain("Tabela de Níveis");
        cut.Markup.Should().NotContain(">estrelas-alkerianas<");
    }
}
