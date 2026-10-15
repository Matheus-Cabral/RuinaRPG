using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class DesabilitadoChipTests : MudBunitContext
{
    [Fact]
    public void Shows_the_condition_that_disables_the_row()
    {
        var cut = Render<DesabilitadoChip>(p => p.Add(c => c.Condicao, "Atordoado"));

        cut.Markup.Should().Contain("Desabilitado por: Atordoado");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Renders_nothing_without_a_condition(string? condicao)
    {
        var cut = Render<DesabilitadoChip>(p => p.Add(c => c.Condicao, condicao));

        cut.Markup.Trim().Should().BeEmpty();
    }
}
