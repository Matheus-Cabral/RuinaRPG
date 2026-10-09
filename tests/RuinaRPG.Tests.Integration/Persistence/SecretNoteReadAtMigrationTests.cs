using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class SecretNoteReadAtMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public SecretNoteReadAtMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_stamps_pre_existing_recipients_as_read_at_the_note_creation_time()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var anterior = db.Database.GetMigrations().Single(m => m.EndsWith("_AddTabelaDeAfinidades"));
        await migrator.MigrateAsync(anterior);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@readat.com", Email = "gm@readat.com", Nickname = "ReadAtGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "p@readat.com", Email = "p@readat.com", Nickname = "ReadAtPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaignId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        // Raw SQL: the entities already carry columns this point of the schema doesn't have yet.
        await SchemaInsert.AtCurrentSchemaAsync(db, "Campaigns", new() { ["Id"] = campaignId, ["GmId"] = gm.Id, ["Nome"] = "Teste", ["Descricao"] = "" });
        await SchemaInsert.AtCurrentSchemaAsync(db, "DiaryEntries", new() { ["Id"] = noteId, ["AuthorUserId"] = gm.Id, ["CampaignId"] = campaignId, ["IsSecretNote"] = true, ["Texto"] = "Pista antiga.", ["CreatedAt"] = createdAt });
        await SchemaInsert.AtCurrentSchemaAsync(db, "DiaryEntryRecipients", new() { ["DiaryEntryId"] = noteId, ["UserId"] = player.Id });

        await migrator.MigrateAsync();

        var recipient = await db.DiaryEntryRecipients.AsNoTracking().SingleAsync(r => r.DiaryEntryId == noteId);
        recipient.ReadAt.Should().Be(createdAt);
    }
}
