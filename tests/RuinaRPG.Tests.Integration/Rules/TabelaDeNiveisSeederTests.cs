using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Tests.Integration.Rules;

public class TabelaDeNiveisSeederTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TabelaDeNiveisSeederTests(PostgresFixture fixture) => _fixture = fixture;

    private RuinaRpgDbContext NewDb() =>
        new(new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options);

    [Fact]
    public async Task Seeded_table_reproduces_todays_budgets_xp_and_eap()
    {
        await using var db = NewDb();
        await db.Database.MigrateAsync();
        var rules = new RulesDataProvider();
        await TabelaDeNiveisSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Níveis.md"), rules.XpPorNivel, rules.EapPorNivel);

        var tabela = await new TabelaDeNiveis(db).ObterAsync();

        tabela.UltimoNivel.Should().Be(50);
        // Literals = what the regex calculators (deleted since) produced over the Markdown; the
        // full 50-level snapshot lives in NivelBonusExtractorTests.
        foreach (var (nivel, atributo, pericia, caracteristica, ignicao) in new[] { (1, 9, 4, 5, 10), (10, 13, 6, 7, 50), (25, 23, 17, 8, 202), (50, 56, 43, 11, 648) })
        {
            AttributePointBudgetCalculator.Compute(nivel, tabela).Should().Be(atributo);
            SkillPointBudgetCalculator.Compute(nivel, tabela).Should().Be(pericia);
            TraitPointBudgetCalculator.Compute(nivel, tabela).Should().Be(caracteristica);
            PontosDeIgnicaoCalculator.ComputeTotal(nivel, 0, tabela).Should().Be(ignicao);
        }
        tabela.ComoXpPorNivel().Select(x => x.XpAbsoluto).Should().Equal(rules.XpPorNivel.OrderBy(x => x.Nivel).Select(x => x.XpAbsoluto));
        tabela.ComoEapPorNivel().Select(x => x.ValorAbsoluto).Should().Equal(rules.EapPorNivel.OrderBy(x => x.Nivel).Select(x => x.ValorAbsoluto));
        tabela.Limite(ChavesDeNivel.MaxPericia, 50).Should().BeNull();
    }

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        await using var db = NewDb();
        await db.Database.MigrateAsync();
        var rules = new RulesDataProvider();
        var md = RulesDataProvider.ReadResource("Tabela de Níveis.md");
        await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel);

        (await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel)).Should().Be(0);
        (await db.ColunasDeNivel.CountAsync()).Should().Be(ChavesDeNivel.Sistema.Count);
    }

    [Fact]
    public async Task Api_host_starts_with_the_table_seeded()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();

        var tabela = await scope.ServiceProvider.GetRequiredService<ITabelaDeNiveis>().ObterAsync();

        tabela.UltimoNivel.Should().Be(50);
    }
}
