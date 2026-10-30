using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RuinaRPG.Api;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class DatabaseInitializerTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _postgres;

    public DatabaseInitializerTests(BlankDatabaseFixture postgres) => _postgres = postgres;

    private ServiceProvider BuildServices() => new ServiceCollection()
        .AddDbContext<RuinaRpgDbContext>(o => o.UseNpgsql(_postgres.ConnectionString))
        .AddScoped<LivroDeRegrasEfeitosSync>()
        .AddSingleton<IRulesDataProvider, RulesDataProvider>()
        .BuildServiceProvider();

    private async Task<Dictionary<string, int>> CountSeededTablesAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return new Dictionary<string, int>
        {
            ["Traits"] = await db.Traits.CountAsync(),
            ["Historicos"] = await db.Historicos.CountAsync(),
            ["Efeitos"] = await db.Efeitos.CountAsync(),
            ["EquipmentKits"] = await db.EquipmentKits.CountAsync(),
            ["DurabilidadesPorRank"] = await db.DurabilidadesPorRank.CountAsync(),
            ["TabelaDeAfinidades"] = await db.TabelaDeAfinidades.CountAsync(),
            ["NiveisProgressao"] = await db.NiveisProgressao.CountAsync(),
        };
    }

    [Fact]
    public async Task Initializing_a_blank_database_migrates_it_and_runs_every_seeder()
    {
        await using var services = BuildServices();

        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await CountSeededTablesAsync(services)).Values.Should().OnlyContain(count => count > 0);
        // Tabela da 2.2.0 (ainda não existe num banco da 1.5.0, por isso fora de CountSeededTablesAsync).
        (await db.TotaisDeAfinidadePorCirculo.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task Initializing_twice_is_idempotent()
    {
        await using var services = BuildServices();
        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);
        var first = await CountSeededTablesAsync(services);

        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);

        (await CountSeededTablesAsync(services)).Should().Equal(first);
        await using var scope = services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>().TotaisDeAfinidadePorCirculo.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task Initializing_a_1_5_0_database_consolidates_its_history_and_keeps_the_data()
    {
        await using var services = BuildServices();
        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            // Um banco da 1.5.0 ainda não tem o que as migrations posteriores à baseline acrescentaram: voltar à
            // baseline roda o Down de cada uma delas (a baseline é o schema da 1.5.0), sem desfazer nada à mão.
            await db.GetService<IMigrator>().MigrateAsync(HistoricoDeMigracoes150.Baseline);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\"");
            foreach (var id in HistoricoDeMigracoes150.Ids)
                await db.Database.ExecuteSqlAsync($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({id}, '8.0.10')");
        }
        var before = await CountSeededTablesAsync(services);

        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);

        await using var check = services.CreateAsyncScope();
        var migrated = check.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await migrated.Database.GetAppliedMigrationsAsync()).Should().Equal(migrated.Database.GetMigrations());
        (await CountSeededTablesAsync(services)).Should().Equal(before);
    }
}
