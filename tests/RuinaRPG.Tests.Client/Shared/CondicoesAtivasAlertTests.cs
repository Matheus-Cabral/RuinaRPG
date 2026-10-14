using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Condicoes;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CondicoesAtivasAlertTests : MudBunitContext
{
    private List<CondicaoAtivaNaFichaResponse> _ativas = new();
    private int _chamadas;

    private IRenderedComponent<CondicoesAtivasAlert> Render(int versao = 0)
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _chamadas++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_ativas) };
        }));
        return Render<CondicoesAtivasAlert>(p => p.Add(c => c.Url, "character-sheets/s1/condicoes-ativas").Add(c => c.Versao, versao));
    }

    [Fact]
    public void Lists_each_active_condition_with_icon_description_lines_and_remaining_turns()
    {
        _ativas = new()
        {
            new("Veneno", "Corrói a vitalidade.", "Veneno", 3, new() { "Vigor −2", "Vitalidade atual −5 a cada vez" }),
            new("Bênção", "", "Bencao", null, new() { "Força +1" }),
        };

        var cut = Render();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Veneno"));
        cut.Markup.Should().Contain("Corrói a vitalidade.").And.Contain("3 turnos")
            .And.Contain("Vigor −2").And.Contain("Vitalidade atual −5 a cada vez").And.Contain("Força +1");
        cut.Markup.Should().Contain(CondicaoIcone.Icone("Veneno"));
        cut.FindAll(".mud-alert").Should().ContainSingle();
        cut.Markup.Should().NotContain("turnos)").And.NotContain("Bênção — ");
    }

    [Fact]
    public void Renders_nothing_when_no_condition_is_active()
    {
        var cut = Render();

        cut.WaitForState(() => _chamadas > 0);
        cut.FindAll(".mud-alert").Should().BeEmpty();
    }

    [Fact]
    public void Reloads_when_Versao_changes()
    {
        var cut = Render();
        cut.WaitForState(() => _chamadas == 1);
        _ativas = new() { new("Fogo", "", "Fogo", 1, new()) };

        cut.Render(p => p.Add(c => c.Url, "character-sheets/s1/condicoes-ativas").Add(c => c.Versao, 1));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Fogo").And.Contain("1 turno"));
    }
}
