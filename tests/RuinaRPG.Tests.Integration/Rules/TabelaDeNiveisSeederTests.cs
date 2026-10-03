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
    public async Task Seeding_migrates_old_passiva_columns_to_additive_keeping_a_custom_name()
    {
        await using var db = NewDb();
        await db.Database.MigrateAsync();
        var rules = new RulesDataProvider();
        var md = RulesDataProvider.ReadResource("Tabela de Níveis.md");
        await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel);

        // Simula um banco semeado com a definição antiga (PorNivel, "Máx. Passivas ..."); uma coluna foi renomeada pelo Auditor.
        async Task<ColunaDeNivel> Col(string chave) => await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == chave);
        var livres = await Col(ChavesDeNivel.MaxPassivasLivres);
        var vocacionais = await Col(ChavesDeNivel.MaxPassivasVocacionais);
        var deClasse = await Col(ChavesDeNivel.MaxPassivasDeClasse);
        livres.Tipo = vocacionais.Tipo = deClasse.Tipo = TipoDeColunaDeNivel.PorNivel;
        livres.Nome = "Máx. Passivas Livres";
        vocacionais.Nome = "Máx. Passivas Vocacionais";
        deClasse.Nome = "Dons de Classe";
        await db.SaveChangesAsync();

        await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel);

        await using var verify = NewDb();
        var colunas = await verify.ColunasDeNivel.Where(c => c.ChaveDeSistema != null).ToDictionaryAsync(c => c.ChaveDeSistema!);
        colunas[ChavesDeNivel.MaxPassivasLivres].Should().Match<ColunaDeNivel>(c => c.Tipo == TipoDeColunaDeNivel.Acumulativa && c.Nome == "Passivas Livres");
        colunas[ChavesDeNivel.MaxPassivasVocacionais].Should().Match<ColunaDeNivel>(c => c.Tipo == TipoDeColunaDeNivel.Acumulativa && c.Nome == "Passivas Vocacionais");
        colunas[ChavesDeNivel.MaxPassivasDeClasse].Should().Match<ColunaDeNivel>(c => c.Tipo == TipoDeColunaDeNivel.Acumulativa && c.Nome == "Dons de Classe");
        colunas[ChavesDeNivel.MaxPericia].Tipo.Should().Be(TipoDeColunaDeNivel.PorNivel);
    }

    [Fact]
    public async Task Api_host_starts_with_the_table_seeded()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();

        var tabela = await scope.ServiceProvider.GetRequiredService<ITabelaDeNiveis>().ObterAsync();

        tabela.UltimoNivel.Should().Be(50);
    }

    [Fact]
    public async Task A_new_system_column_lands_after_its_predecessor_on_an_already_seeded_table()
    {
        await using var db = NewDb();
        await db.Database.MigrateAsync();
        var rules = new RulesDataProvider();
        var md = RulesDataProvider.ReadResource("Tabela de Níveis.md");
        await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel);

        // Simula um banco de antes da 1.4.2: sem a Coringa, com XP/EAP logo depois de De Classe e uma coluna do Auditor no fim.
        var original = await db.ColunasDeNivel.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Ordem);
        var custom = new ColunaDeNivel { Id = Guid.NewGuid(), Nome = "Fama", Tipo = TipoDeColunaDeNivel.Acumulativa, Ordem = 99 };
        try
        {
            db.ColunasDeNivel.Remove(await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == ChavesDeNivel.MaxPassivasCoringa));
            var deClasse = await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == ChavesDeNivel.MaxPassivasDeClasse);
            (await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == ChavesDeNivel.XpParaProximoNivel)).Ordem = deClasse.Ordem + 1;
            (await db.ColunasDeNivel.SingleAsync(c => c.ChaveDeSistema == ChavesDeNivel.EapBase)).Ordem = deClasse.Ordem + 2;
            custom.Ordem = deClasse.Ordem + 3;
            db.ColunasDeNivel.Add(custom);
            await db.SaveChangesAsync();

            (await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel)).Should().Be(1);

            await using var verify = NewDb();
            var ordem = (await verify.ColunasDeNivel.OrderBy(c => c.Ordem).ToListAsync()).Select(c => c.ChaveDeSistema ?? c.Nome).ToList();
            ordem.Should().OnlyHaveUniqueItems();
            ordem.SkipWhile(c => c != ChavesDeNivel.MaxPassivasDeClasse).Should().Equal(
                ChavesDeNivel.MaxPassivasDeClasse, ChavesDeNivel.MaxPassivasCoringa, ChavesDeNivel.XpParaProximoNivel, ChavesDeNivel.EapBase, "Fama");
            (await verify.ColunasDeNivel.Select(c => c.Ordem).ToListAsync()).Should().OnlyHaveUniqueItems();
        }
        finally
        {
            // A tabela é compartilhada com as outras classes de teste: devolve o estado semeado.
            await using var restore = NewDb();
            restore.ColunasDeNivel.RemoveRange(restore.ColunasDeNivel.Where(c => c.Id == custom.Id));
            foreach (var coluna in await restore.ColunasDeNivel.ToListAsync())
                if (original.TryGetValue(coluna.Id, out var o)) coluna.Ordem = o;
            await restore.SaveChangesAsync();
            await TabelaDeNiveisSeeder.SeedAsync(restore, md, rules.XpPorNivel, rules.EapPorNivel);
        }
    }
}
