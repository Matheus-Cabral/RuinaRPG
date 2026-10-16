using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    }

    [Fact]
    public async Task Initializing_twice_is_idempotent()
    {
        await using var services = BuildServices();
        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);
        var first = await CountSeededTablesAsync(services);

        await DatabaseInitializer.InitializeAsync(services, NullLogger.Instance);

        (await CountSeededTablesAsync(services)).Should().Equal(first);
    }
}
