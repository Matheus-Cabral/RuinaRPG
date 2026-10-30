using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules.ReferenceData;
using RuinaRPG.Infrastructure.Afinidades;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class TotalDeAfinidadePorCirculoSeederTests : IClassFixture<BlankDatabaseFixture>
{
    private static readonly CirculoGrauPorEap[] DuasLinhas =
    [
        new(0, "0", "0", 3, 0),
        new(1, "100", "100", 5, 2),
    ];

    private readonly BlankDatabaseFixture _fixture;

    public TotalDeAfinidadePorCirculoSeederTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        // Banco compartilhado pela classe: começa de uma tabela vazia, seja qual for o estado anterior.
        await db.TotaisDeAfinidadePorCirculo.ExecuteDeleteAsync();
        return db;
    }

    [Fact]
    public async Task Seed_inserts_the_Afinidade_absoluta_of_every_Circulo_when_the_table_is_empty()
    {
        await using var db = await NewDbAsync();

        var inseridas = await TotalDeAfinidadePorCirculoSeeder.SeedAsync(db, DuasLinhas);

        inseridas.Should().Be(2);
        (await db.TotaisDeAfinidadePorCirculo.AsNoTracking().OrderBy(t => t.Circulo).Select(t => new { t.Circulo, t.Total }).ToListAsync())
            .Should().Equal(new { Circulo = 0, Total = 3 }, new { Circulo = 1, Total = 5 });
    }

    [Fact]
    public async Task Seed_inserts_nothing_when_the_Auditor_already_keeps_the_table()
    {
        await using var db = await NewDbAsync();
        db.TotaisDeAfinidadePorCirculo.Add(new TotalDeAfinidadePorCirculo { Circulo = 4, Total = 99 });
        await db.SaveChangesAsync();

        var inseridas = await TotalDeAfinidadePorCirculoSeeder.SeedAsync(db, DuasLinhas);

        inseridas.Should().Be(0);
        (await db.TotaisDeAfinidadePorCirculo.AsNoTracking().Select(t => new { t.Circulo, t.Total }).ToListAsync())
            .Should().Equal(new { Circulo = 4, Total = 99 });
    }

    [Fact]
    public async Task Seed_of_the_real_table_takes_the_Afinidade_Valores_Absolutos_column()
    {
        await using var db = await NewDbAsync();

        await TotalDeAfinidadePorCirculoSeeder.SeedAsync(db, new RulesDataProvider().CirculoGrauPorEap);

        (await db.TotaisDeAfinidadePorCirculo.AsNoTracking().OrderBy(t => t.Circulo).Select(t => t.Total).ToListAsync())
            .Should().Equal(3, 5, 7, 9, 11, 13, 15, 17, 19, 21);
    }
}
