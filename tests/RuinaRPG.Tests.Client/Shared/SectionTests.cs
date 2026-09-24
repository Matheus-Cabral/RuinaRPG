using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class SectionTests : MudBunitContext
{
    [Fact]
    public void TitleInfo_renders_next_to_the_title_when_provided()
    {
        var cut = Render<Section>(p => p
            .Add(x => x.Title, "Geral")
            .Add(x => x.TitleInfo, (RenderFragment)(b => b.AddMarkupContent(0, "<span class=\"marker-titleinfo\">i</span>"))));

        cut.Markup.Should().Contain("marker-titleinfo");
        cut.Markup.Should().Contain("Geral");
    }

    [Fact]
    public void TitleInfo_is_absent_when_not_provided()
    {
        var cut = Render<Section>(p => p.Add(x => x.Title, "Geral"));

        cut.Markup.Should().NotContain("marker-titleinfo");
    }
}
