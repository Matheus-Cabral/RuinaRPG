using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class HistoricoSeederTests
{
    // The real, current Docs/Sistema RPG/Historico.md — read once here the same way
    // RulesDataProvider.ReadResource would at runtime, via the embedded resource. If this test
    // fails after editing Historico.md, the doc's heading/bonus-line shape broke the parser.
    private static string RealHistoricoMarkdown() =>
        System.IO.File.ReadAllText(FindRepoRoot() + "/Docs/Sistema RPG/Historico.md");

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(dir, "RuinaRPG.sln")))
            dir = System.IO.Directory.GetParent(dir)!.FullName;
        return dir;
    }

    [Fact]
    public void The_real_Historico_md_parses_into_exactly_26_entries()
    {
        var result = HistoricoSeedParser.Parse(RealHistoricoMarkdown());

        result.Should().HaveCount(26);
    }

    [Fact]
    public void The_real_Historico_md_has_no_duplicate_Nome()
    {
        var result = HistoricoSeedParser.Parse(RealHistoricoMarkdown());

        result.Select(h => h.Nome).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_real_Historico_md_never_bonifies_the_same_Pericia_twice_in_one_entry()
    {
        var result = HistoricoSeedParser.Parse(RealHistoricoMarkdown());

        result.Should().OnlyContain(h => h.PericiaMaisSeis != h.PericiaMaisTres);
    }

    [Fact]
    public void Estudo_Academico_parses_with_its_real_fields()
    {
        var result = HistoricoSeedParser.Parse(RealHistoricoMarkdown());

        var estudoAcademico = result.Should().ContainSingle(h => h.Nome == "Estudo Acadêmico").Subject;
        estudoAcademico.PericiaMaisSeis.Should().Be(Pericia.Arcano);
        estudoAcademico.PericiaMaisTres.Should().Be(Pericia.Biblioteca);
    }
}
