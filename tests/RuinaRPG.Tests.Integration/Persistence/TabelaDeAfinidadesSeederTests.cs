using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class TabelaDeAfinidadesSeederTests : IClassFixture<PostgresFixture>
{
    private const string TresLinhas = """
        | Afinidade | Eficiência | Dano |
        | --------- | ---------- | ---- |
        | 0         | 0          | 0    |
        | 1         | 1          | 0    |
        | 2         | 1          | 1    |
        """;

    private readonly PostgresFixture _fixture;

    public TabelaDeAfinidadesSeederTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        // Banco compartilhado pela classe: começa de uma tabela vazia, seja qual for o estado anterior.
        await db.TabelaDeAfinidades.ExecuteDeleteAsync();
        return db;
    }

    private static async Task RestaurarDoArquivoRealAsync(RuinaRpgDbContext db)
    {
        await db.TabelaDeAfinidades.ExecuteDeleteAsync();
        await TabelaDeAfinidadesSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Afinidades.md"));
    }

    [Fact]
    public async Task Seed_inserts_every_row_when_the_table_is_empty()
    {
        await using var db = await NewDbAsync();
        try
        {
            var inseridas = await TabelaDeAfinidadesSeeder.SeedAsync(db, TresLinhas);

            inseridas.Should().Be(3);
            var linhas = await db.TabelaDeAfinidades.AsNoTracking().OrderBy(l => l.Afinidade).ToListAsync();
            linhas.Select(l => (l.Afinidade, l.Eficiencia, l.Dano)).Should().Equal((0, 0, 0), (1, 1, 0), (2, 1, 1));
        }
        finally { await RestaurarDoArquivoRealAsync(db); }
    }

    [Fact]
    public async Task Seed_inserts_nothing_when_the_table_already_has_rows()
    {
        await using var db = await NewDbAsync();
        try
        {
            db.TabelaDeAfinidades.Add(new AfinidadeElementalLinha { Id = Guid.NewGuid(), Afinidade = 4, Eficiencia = 9, Dano = 9 });
            await db.SaveChangesAsync();

            var inseridas = await TabelaDeAfinidadesSeeder.SeedAsync(db, TresLinhas);

            inseridas.Should().Be(0);
            var linhas = await db.TabelaDeAfinidades.AsNoTracking().ToListAsync();
            linhas.Select(l => (l.Afinidade, l.Eficiencia, l.Dano)).Should().Equal((4, 9, 9));
        }
        finally { await RestaurarDoArquivoRealAsync(db); }
    }

    [Fact]
    public async Task Seed_of_the_real_file_inserts_22_rows()
    {
        await using var db = await NewDbAsync();

        var inseridas = await TabelaDeAfinidadesSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Afinidades.md"));

        inseridas.Should().Be(22);
    }
}
