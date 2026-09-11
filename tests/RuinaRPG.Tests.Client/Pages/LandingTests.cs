using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Pages;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class LandingTests : MudBunitContext
{
    [Fact]
    public void Hero_has_a_rulebook_button_above_the_signup_and_login_buttons()
    {
        // Anonymous visitors can read the rulebook without signing up or logging in (per
        // "Requisitos - Login e Cadastro" R0001) — the hero should offer that path first.
        var cut = Render<Landing>();

        var buttons = cut.FindAll("a.mud-button-root");
        var hrefs = buttons.Select(b => b.GetAttribute("href")).ToList();

        hrefs.Should().ContainInOrder("/livro-de-regras", "/cadastro", "/login");
    }

    [Fact]
    public void Footer_credits_the_site_and_links_to_the_developers_GitHub()
    {
        // Credits the site's own development only — not the RPG system/rulebook, which isn't
        // the developer's work.
        var cut = Render<Landing>();

        var footer = cut.Find("footer.rr-landing-footer");
        footer.TextContent.Should().Contain("Site desenvolvido por Matheus Cabral");

        var link = footer.QuerySelector("a");
        link!.GetAttribute("href").Should().Be("https://github.com/Matheus-Cabral");
    }
}
