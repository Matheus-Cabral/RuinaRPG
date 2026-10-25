using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

// Cada teste mexe no histórico de migrations, então cada um ganha seu próprio banco vazio.
public class ConsolidacaoDoHistoricoTests
{
    private static async Task ComBancoNovoAsync(Func<RuinaRpgDbContext, Task> corpo)
    {
        var banco = new BlankDatabaseFixture();
        await banco.InitializeAsync();
        try
        {
            var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(banco.ConnectionString).Options;
            await using var db = new RuinaRpgDbContext(options);
            await corpo(db);
        }
        finally
        {
            await banco.DisposeAsync();
        }
    }

    private static Task<List<string>> HistoricoAsync(RuinaRpgDbContext db) =>
        db.Database.SqlQueryRaw<string>("SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\" ORDER BY 1").ToListAsync();

    private static Task<List<string>> LinhasDoHistoricoAsync(RuinaRpgDbContext db) =>
        db.Database.SqlQueryRaw<string>("SELECT \"MigrationId\" || '|' || \"ProductVersion\" AS \"Value\" FROM \"__EFMigrationsHistory\" ORDER BY 1").ToListAsync();

    // Produção acumulou linhas gravadas por várias versões do EF 8; a consolidação olha só o MigrationId.
    private static async Task DefinirHistoricoAsync(RuinaRpgDbContext db, IEnumerable<string> ids)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\"");
        var i = 0;
        foreach (var id in ids)
        {
            var versao = i++ % 2 == 0 ? "8.0.8" : "8.0.10";
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({id}, {versao})");
        }
    }

    private static async Task<ApplicationUser> CriarGmAsync(RuinaRpgDbContext db)
    {
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@consolidacao.com", Email = "gm@consolidacao.com", Nickname = "ConsolidacaoGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        return gm;
    }

    private static async Task<ApplicationUser?> GmAsync(RuinaRpgDbContext db, Guid id) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);

    [Fact]
    public async Task Banco_novo_nao_e_tocado_e_o_migrate_cria_a_baseline_com_as_pericias()
    {
        await ComBancoNovoAsync(async db =>
        {
            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);

            (await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NULL AS \"Value\"").SingleAsync())
                .Should().BeTrue("a consolidação não cria a tabela de histórico");

            await db.Database.MigrateAsync();

            // A baseline cria o schema da 1.5.0 mais o das migrations seguintes; o histórico fica com todas.
            (await HistoricoAsync(db)).Should().Equal(db.Database.GetMigrations().Order(StringComparer.Ordinal));
            (await db.Pericias.CountAsync()).Should().Be(39);
        });
    }

    [Fact]
    public async Task Banco_na_1_5_0_troca_o_historico_pela_baseline_sem_tocar_nos_dados()
    {
        await ComBancoNovoAsync(async db =>
        {
            await db.Database.MigrateAsync();
            var gm = await CriarGmAsync(db);
            await DefinirHistoricoAsync(db, HistoricoDeMigracoes150.Ids);
            var pericias = await db.Pericias.CountAsync();

            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);

            (await HistoricoAsync(db)).Should().Equal(HistoricoDeMigracoes150.Baseline);
            // Um banco da 1.5.0 fica só com a baseline; as migrations posteriores ainda estão pendentes.
            (await db.Database.GetPendingMigrationsAsync()).Should().Equal(db.Database.GetMigrations().Where(m => m != HistoricoDeMigracoes150.Baseline));
            (await GmAsync(db, gm.Id))!.Nickname.Should().Be("ConsolidacaoGm");
            (await db.Pericias.CountAsync()).Should().Be(pericias);
        });
    }

    [Fact]
    public async Task Consolidar_duas_vezes_nao_muda_nada_na_segunda()
    {
        await ComBancoNovoAsync(async db =>
        {
            await db.Database.MigrateAsync();
            await DefinirHistoricoAsync(db, HistoricoDeMigracoes150.Ids);
            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);
            var depoisDaPrimeira = await LinhasDoHistoricoAsync(db);

            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);

            (await LinhasDoHistoricoAsync(db)).Should().Equal(depoisDaPrimeira);
        });
    }

    [Fact]
    public async Task Baseline_seguida_de_migrations_novas_fica_como_esta()
    {
        await ComBancoNovoAsync(async db =>
        {
            await db.Database.MigrateAsync();
            await DefinirHistoricoAsync(db, [HistoricoDeMigracoes150.Baseline, "20991231000000_Futura"]);
            var antes = await LinhasDoHistoricoAsync(db);

            await HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance);

            (await LinhasDoHistoricoAsync(db)).Should().Equal(antes);
        });
    }

    public static TheoryData<string> EstadosRecusados => new() { "parcial", "desconhecida", "misturado" };

    private static IEnumerable<string> Historico(string estado) => estado switch
    {
        "parcial" => HistoricoDeMigracoes150.Ids.Take(HistoricoDeMigracoes150.Ids.Count - 1),
        "desconhecida" => HistoricoDeMigracoes150.Ids.Append("20261015000000_Desconhecida"),
        "misturado" => HistoricoDeMigracoes150.Ids.Take(10).Append(HistoricoDeMigracoes150.Baseline),
        _ => throw new ArgumentOutOfRangeException(nameof(estado)),
    };

    [Theory]
    [MemberData(nameof(EstadosRecusados))]
    public async Task Qualquer_outro_historico_e_recusado_sem_mudar_nada(string estado)
    {
        await ComBancoNovoAsync(async db =>
        {
            await db.Database.MigrateAsync();
            var gm = await CriarGmAsync(db);
            await DefinirHistoricoAsync(db, Historico(estado));
            var antes = await LinhasDoHistoricoAsync(db);

            var erro = await FluentActions.Awaiting(() => HistoricoDeMigracoes150.ConsolidarAsync(db, NullLogger.Instance))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*1.5.0*Nada foi alterado*");
            if (estado == "parcial")
                erro.Which.Message.Should().Contain($"Faltando da 1.5.0: {HistoricoDeMigracoes150.Ids[^1]}");
            if (estado == "desconhecida")
                erro.Which.Message.Should().Contain("Inesperadas: 20261015000000_Desconhecida");

            (await LinhasDoHistoricoAsync(db)).Should().Equal(antes);
            (await GmAsync(db, gm.Id)).Should().NotBeNull();
        });
    }
}
