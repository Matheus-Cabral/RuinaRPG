using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class RulebookDocumentOverrideMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RulebookDocumentOverrideMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_RulebookDocumentOverrides_table_with_a_unique_Slug()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddRulebookDocumentOverrides"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "rulebookoverridegm@teste.com", Email = "rulebookoverridegm@teste.com", Nickname = "RulebookOverrideGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.RulebookDocumentOverrides.Add(new RulebookDocumentOverride { Id = Guid.NewGuid(), Slug = "sistema-basico", MarkdownText = "# Teste\n\nConteúdo.", UpdatedByUserId = gm.Id, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var reloaded = await db.RulebookDocumentOverrides.SingleAsync(o => o.Slug == "sistema-basico");
        reloaded.MarkdownText.Should().Contain("Conteúdo.");

        // Slug is unique — a second row for the same slug must fail.
        db.RulebookDocumentOverrides.Add(new RulebookDocumentOverride { Id = Guid.NewGuid(), Slug = "sistema-basico", MarkdownText = "Outro.", UpdatedByUserId = gm.Id, UpdatedAt = DateTime.UtcNow });
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
