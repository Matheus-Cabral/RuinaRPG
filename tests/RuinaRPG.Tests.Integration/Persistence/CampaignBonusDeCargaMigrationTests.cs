using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CampaignBonusDeCargaMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CampaignBonusDeCargaMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }

    [Fact]
    public async Task Migrate_adds_the_BonusDeCarga_column_to_Campaigns()
    {
        await using var db = await NewDbAsync();

        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddBonusDeCargaDaCampanha"));
        var columns = await db.Database
            .SqlQuery<string>($"SELECT CAST(column_name AS text) AS \"Value\" FROM information_schema.columns WHERE table_name = 'Campaigns'")
            .ToListAsync();
        columns.Should().Contain("BonusDeCarga");
    }

    [Fact]
    public async Task A_campaign_inserted_without_a_bonus_defaults_to_zero_and_a_negative_one_round_trips()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@bonuscargatest.com", Email = "gm@bonuscargatest.com", Nickname = "BonusCargaGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        var legacyId = Guid.NewGuid();
        // Raw insert mimics a row that existed before the column: the DB default must be 0.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Campaigns\" (\"Id\", \"GmId\", \"Nome\", \"Descricao\") VALUES ({legacyId}, {gm.Id}, 'Antiga', '')");
        var negativeId = Guid.NewGuid();
        db.Campaigns.Add(new Campaign { Id = negativeId, GmId = gm.Id, Nome = "Negativa", Descricao = "", BonusDeCarga = -2.5m });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        (await db.Campaigns.SingleAsync(c => c.Id == legacyId)).BonusDeCarga.Should().Be(0m);
        (await db.Campaigns.SingleAsync(c => c.Id == negativeId)).BonusDeCarga.Should().Be(-2.5m);
    }
}
