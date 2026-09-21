using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Runes;

namespace RuinaRPG.Tests.Integration.Persistence;

public class RuneBankMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RuneBankMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_rune_bank_table_and_the_new_columns()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddRuneBank"));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"gm{suffix}@runebank.test", Email = $"gm{suffix}@runebank.test", Nickname = $"RuneBank{suffix}", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var entry = new RuneBankEntry { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Runa do Fogo", Descricao = "Queima o alvo.", Grau = 2 };
        db.RuneBankEntries.Add(entry);
        await db.SaveChangesAsync();

        (await db.RuneBankEntries.CountAsync(e => e.GmId == gm.Id)).Should().Be(1);

        (await ColumnsOfAsync(db, "CharacterRunes")).Should().Contain("SourceBankEntryId");
        (await ColumnsOfAsync(db, "NpcRunes")).Should().Contain("SourceBankEntryId");
        (await ColumnsOfAsync(db, "CampaignAttachments")).Should().Contain("RuneBankEntryId");

        db.RuneBankEntries.Remove(entry);
        await db.SaveChangesAsync();
    }

    private static Task<List<string>> ColumnsOfAsync(RuinaRpgDbContext db, string table) =>
        db.Database
            .SqlQuery<string>($"SELECT CAST(column_name AS text) AS \"Value\" FROM information_schema.columns WHERE table_name = {table}")
            .ToListAsync();
}
