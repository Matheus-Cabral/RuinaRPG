using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class TraitAuditFieldsMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TraitAuditFieldsMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_new_Trait_audit_columns_defaulted_false_and_null()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddTraitAuditFields"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "traitauditgm@teste.com", Email = "traitauditgm@teste.com", Nickname = "TraitAuditGm", Role = UserRole.GM };
        db.Users.Add(gm);
        var trait = new Trait { Id = Guid.NewGuid(), Nome = "Teste Migração", Descricao = "Teste.", Custo = 1, Polaridade = Polaridade.Positiva };
        db.Traits.Add(trait);
        await db.SaveChangesAsync();

        var reloaded = await db.Traits.SingleAsync(t => t.Id == trait.Id);
        reloaded.IsCustomized.Should().BeFalse();
        reloaded.IsDeleted.Should().BeFalse();
        reloaded.UpdatedByUserId.Should().BeNull();
        reloaded.UpdatedAt.Should().BeNull();

        reloaded.IsCustomized = true;
        reloaded.IsDeleted = true;
        reloaded.UpdatedByUserId = gm.Id;
        reloaded.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var reReloaded = await db.Traits.SingleAsync(t => t.Id == trait.Id);
        reReloaded.IsCustomized.Should().BeTrue();
        reReloaded.IsDeleted.Should().BeTrue();
        reReloaded.UpdatedByUserId.Should().Be(gm.Id);
        reReloaded.UpdatedAt.Should().NotBeNull();
    }
}
