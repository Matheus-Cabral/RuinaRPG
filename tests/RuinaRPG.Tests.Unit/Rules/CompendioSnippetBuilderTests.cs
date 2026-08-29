using FluentAssertions;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class CompendioSnippetBuilderTests
{
    [Fact]
    public void Build_with_empty_content_returns_an_empty_snippet()
    {
        var snippet = CompendioSnippetBuilder.Build("", "qualquer", maxLength: 20);

        snippet.Before.Should().BeEmpty();
        snippet.Match.Should().BeNull();
        snippet.After.Should().BeEmpty();
    }

    [Fact]
    public void Build_with_no_query_truncates_from_the_start()
    {
        var snippet = CompendioSnippetBuilder.Build("Um texto bem mais longo do que a janela permitida.", null, maxLength: 10);

        snippet.Before.Should().Be("Um texto b…");
        snippet.Match.Should().BeNull();
        snippet.After.Should().BeEmpty();
    }

    [Fact]
    public void Build_with_content_shorter_than_maxLength_returns_it_whole_without_an_ellipsis()
    {
        var snippet = CompendioSnippetBuilder.Build("Curto.", null, maxLength: 160);

        snippet.Before.Should().Be("Curto.");
        snippet.Match.Should().BeNull();
    }

    [Fact]
    public void Build_when_the_query_is_not_found_falls_back_to_truncating_from_the_start()
    {
        var snippet = CompendioSnippetBuilder.Build("Um texto qualquer sem o termo buscado.", "inexistente", maxLength: 200);

        snippet.Before.Should().Be("Um texto qualquer sem o termo buscado.");
        snippet.Match.Should().BeNull();
    }

    [Fact]
    public void Build_centers_the_window_on_the_match_with_ellipses_on_both_sides()
    {
        var content = "O Guerreiro empunha a Espada Flamejante contra a horda de goblins que se aproxima pela névoa.";

        var snippet = CompendioSnippetBuilder.Build(content, "Espada Flamejante", maxLength: 40);

        snippet.Before.Should().StartWith("…");
        snippet.Match.Should().Be("Espada Flamejante");
        snippet.After.Should().EndWith("…");
        (snippet.Before + snippet.Match + snippet.After).Length.Should().BeLessThanOrEqualTo(40 + 2); // +2 for the two ellipses
    }

    [Fact]
    public void Build_matches_case_insensitively_but_preserves_the_contents_original_casing()
    {
        var snippet = CompendioSnippetBuilder.Build("A Bola de Fogo causa dano em área.", "bola de fogo", maxLength: 160);

        snippet.Match.Should().Be("Bola de Fogo"); // original casing from content, not the lowercase query
    }

    [Fact]
    public void Build_with_a_match_near_the_very_start_has_no_leading_ellipsis()
    {
        var content = "Bola de Fogo causa dano em área, empurrando alvos próximos ao ponto de impacto pela força da explosão.";

        var snippet = CompendioSnippetBuilder.Build(content, "Bola de Fogo", maxLength: 30);

        snippet.Before.Should().NotStartWith("…");
        snippet.Match.Should().Be("Bola de Fogo");
    }

    [Fact]
    public void Build_with_a_match_near_the_very_end_has_no_trailing_ellipsis()
    {
        var content = "Após uma longa jornada pelas ruínas, os aventureiros finalmente encontram a Bola de Fogo.";

        var snippet = CompendioSnippetBuilder.Build(content, "Bola de Fogo", maxLength: 30);

        snippet.After.Should().NotEndWith("…");
        snippet.Match.Should().Be("Bola de Fogo");
    }

    [Fact]
    public void Build_reconstructs_a_window_no_longer_than_maxLength_plus_ellipses()
    {
        var content = string.Concat(Enumerable.Repeat("palavra ", 50)) + "ALVO" + string.Concat(Enumerable.Repeat(" palavra", 50));

        var snippet = CompendioSnippetBuilder.Build(content, "ALVO", maxLength: 50);

        (snippet.Before + snippet.Match + snippet.After).Length.Should().BeLessThanOrEqualTo(50 + 2);
        snippet.Match.Should().Be("ALVO");
    }
}
