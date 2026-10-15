using FluentAssertions;
using RuinaRPG.Infrastructure.Encounters;

namespace RuinaRPG.Tests.Unit.Encounters;

public class EncounterLogOrdemTests
{
    [Fact]
    public void Proxima_is_strictly_increasing_across_consecutive_calls()
    {
        var valores = Enumerable.Range(0, 10_000).Select(_ => EncounterLogOrdem.Proxima()).ToList();

        valores.Zip(valores.Skip(1), (a, b) => b > a).Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public async Task Proxima_yields_no_duplicates_across_parallel_tasks()
    {
        var tarefas = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            Enumerable.Range(0, 5_000).Select(_ => EncounterLogOrdem.Proxima()).ToList()));

        var todos = (await Task.WhenAll(tarefas)).SelectMany(l => l).ToList();

        todos.Should().OnlyHaveUniqueItems();
    }
}
