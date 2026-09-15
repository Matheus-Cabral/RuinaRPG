using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class EfeitoMigrationAndSeedTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EfeitoMigrationAndSeedTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Efeitos_table_and_SeedAsync_populates_it()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddEfeitos"));

        await EfeitoSeeder.SeedAsync(db);

        var todos = await db.Efeitos.Where(e => !e.IsDeleted).ToListAsync();
        // 46 named (44 with a "## " heading in the source doc + "Congelar" + "Aumentar Max.
        // Arcana", both missing their heading there — see GRAUS & CÍRCULOS.md) + 3 básicos +
        // Libra split em 2 = 51. (The design spec's "45 named" prose undercounts by 2: it caught
        // Congelar's missing heading but not Aumentar Max. Arcana's — verified directly against
        // the source doc while implementing this task.)
        todos.Should().HaveCount(51);

        var dano = todos.Single(e => e.Nome == "Dano");
        dano.TipoDeCusto.Should().Be(TipoDeCusto.PorUnidade);
        dano.CustoPorUnidade.Should().Be(2);
        dano.Grau.Should().Be(1);

        var aumentarArmadura = todos.Single(e => e.Nome == "Aumentar Armadura");
        aumentarArmadura.MaxUnidades.Should().Be(5);
        aumentarArmadura.MaxEscalaPorGrau.Should().BeTrue();
        aumentarArmadura.PreRequisitosJson.Should().Contain("Duração");

        var encantamentoElemental = todos.Single(e => e.Nome == "Encantamento Elemental");
        encantamentoElemental.CustoFixo.Should().Be(2);
        encantamentoElemental.CustoAlternativo.Should().Be(4);
        encantamentoElemental.CustoAlternativoAPartirDoGrau.Should().Be(4);

        var drenoDeVitalidade = todos.Single(e => e.Nome == "Dreno de Vitalidade");
        drenoDeVitalidade.TipoDeCusto.Should().Be(TipoDeCusto.DerivadoDeOutroEfeito);
        drenoDeVitalidade.QuantidadeDerivadaDeEfeito.Should().Be("Dano");

        var atoMultiplo = todos.Single(e => e.Nome == "Ato Múltiplo");
        atoMultiplo.TipoDeCusto.Should().Be(TipoDeCusto.ManualPorUnidade);
        atoMultiplo.MaxContandoAPartirDoGrau.Should().Be(3);

        var libraVitalidade = todos.Single(e => e.Nome == "Libra (Vitalidade)");
        var libraArcana = todos.Single(e => e.Nome == "Libra (Arcana)");
        libraVitalidade.CustoFixo.Should().Be(4);
        libraArcana.CustoFixo.Should().Be(6);

        var detrito = todos.Single(e => e.Nome == "Detrito");
        detrito.PreRequisitosJson.Should().Contain("Atordoamento").And.Contain("Congelar").And.Contain("Enraizar");
        detrito.PreRequisitosJson.Should().NotContain("Selar");
    }

    [Fact]
    public async Task SeedAsync_never_overwrites_a_row_the_Auditor_has_customized()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();
        await EfeitoSeeder.SeedAsync(db);

        var cura = await db.Efeitos.SingleAsync(e => e.Nome == "Cura");
        cura.CustoFixo = 99;
        cura.IsCustomized = true;
        await db.SaveChangesAsync();

        await EfeitoSeeder.SeedAsync(db);

        (await db.Efeitos.SingleAsync(e => e.Nome == "Cura")).CustoFixo.Should().Be(99);
    }

    [Fact]
    public async Task SeedAsync_never_resurrects_a_row_the_Auditor_has_deleted()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();
        await EfeitoSeeder.SeedAsync(db);

        var cura = await db.Efeitos.SingleAsync(e => e.Nome == "Cura");
        cura.IsDeleted = true;
        await db.SaveChangesAsync();

        await EfeitoSeeder.SeedAsync(db);

        (await db.Efeitos.SingleAsync(e => e.Nome == "Cura")).IsDeleted.Should().BeTrue();
    }
}
