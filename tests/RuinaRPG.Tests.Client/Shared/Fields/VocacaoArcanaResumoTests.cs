using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class VocacaoArcanaResumoTests : MudBunitContext
{
    private IRenderedComponent<VocacaoArcanaResumo> RenderResumo(int gasto, int maxima, int adicional = 0, Action<int>? onAdicional = null) =>
        Render<VocacaoArcanaResumo>(p => p
            .Add(c => c.Gasto, gasto)
            .Add(c => c.Maxima, maxima)
            .Add(c => c.AfinidadeAdicional, adicional)
            .Add(c => c.AfinidadeAdicionalChanged, onAdicional ?? (_ => { })));

    [Fact]
    public void Shows_the_gasto_over_the_maximum()
    {
        var cut = RenderResumo(gasto: 2, maxima: 5);

        cut.Markup.Should().Contain("Vocação Arcana: 2 / 5");
    }

    [Fact]
    public void Within_the_maximum_the_counter_is_not_highlighted_as_an_error()
    {
        var cut = RenderResumo(gasto: 5, maxima: 5);

        cut.FindComponent<MudText>().Instance.Color.Should().NotBe(Color.Error);
    }

    [Fact]
    public void Beyond_the_maximum_the_counter_is_highlighted_as_an_error()
    {
        var cut = RenderResumo(gasto: 6, maxima: 5);

        cut.FindComponent<MudText>().Instance.Color.Should().Be(Color.Error);
    }

    [Fact]
    public void Editing_the_afinidade_adicional_raises_the_change()
    {
        int? recebido = null;
        var cut = RenderResumo(gasto: 0, maxima: 3, adicional: 1, onAdicional: v => recebido = v);

        var field = cut.FindComponent<MudNumericField<int>>();
        field.Instance.Label.Should().Be("Afinidade Adicional");
        cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(4));

        recebido.Should().Be(4);
    }

    [Fact]
    public void CalcularGasto_parses_the_affinity_rows_the_same_way_the_server_counts_them()
    {
        var gasto = VocacaoArcanaResumo.CalcularGasto([
            ("Fogo", 2, "Terra", 1, 1),
            ("Ar", 1, "Fogo", 1, 2),
            (null, null, null, null, null),
        ]);

        gasto.Should().Be(7); // Fogo 2 + Terra 1 + Ar 1 + Sub-Elementos 1 + 2
    }
}
