using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Runes;
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

    [Fact]
    public async Task Migrate_adds_the_optional_ImageId_column_to_every_rune_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddRuneImage"));
        (await ColumnsOfAsync(db, "RuneBankEntries")).Should().Contain("ImageId");
        (await ColumnsOfAsync(db, "CharacterRunes")).Should().Contain("ImageId");
        (await ColumnsOfAsync(db, "NpcRunes")).Should().Contain("ImageId");
    }

    [Fact]
    public async Task Migrate_adds_the_nullable_Tipo_column_to_every_rune_table_and_existing_rows_keep_null()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddTipoDeRuna"));
        foreach (var table in new[] { "RuneBankEntries", "CharacterRunes", "NpcRunes" })
        {
            (await ColumnsOfAsync(db, table)).Should().Contain("Tipo");
            (await IsNullableAsync(db, table, "Tipo")).Should().Be("YES");
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"gm{suffix}@runetipo.test", Email = $"gm{suffix}@runetipo.test", Nickname = $"RuneTipo{suffix}", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        // Uma Runa "antiga": inserida sem informar o tipo, como as que já existiam antes da migration.
        var entryId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"RuneBankEntries\" (\"Id\", \"GmId\", \"Nome\", \"Descricao\", \"Grau\") VALUES ({entryId}, {gm.Id}, 'Runa Antiga', 'Sem tipo.', 1)");

        (await db.RuneBankEntries.AsNoTracking().SingleAsync(e => e.Id == entryId)).Tipo.Should().BeNull();

        db.RuneBankEntries.Add(new RuneBankEntry { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Nova", Descricao = "Negra.", Grau = 1, Tipo = TipoDeRuna.Negra });
        await db.SaveChangesAsync();
        (await db.Database.SqlQuery<string>($"SELECT \"Tipo\" AS \"Value\" FROM \"RuneBankEntries\" WHERE \"Nome\" = 'Nova' AND \"GmId\" = {gm.Id}").SingleAsync()).Should().Be("Negra");
    }

    private static Task<string> IsNullableAsync(RuinaRpgDbContext db, string table, string column) =>
        db.Database
            .SqlQuery<string>($"SELECT CAST(is_nullable AS text) AS \"Value\" FROM information_schema.columns WHERE table_name = {table} AND column_name = {column}")
            .SingleAsync();

    private static Task<List<string>> ColumnsOfAsync(RuinaRpgDbContext db, string table) =>
        db.Database
            .SqlQuery<string>($"SELECT CAST(column_name AS text) AS \"Value\" FROM information_schema.columns WHERE table_name = {table}")
            .ToListAsync();
}
