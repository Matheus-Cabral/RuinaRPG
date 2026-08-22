using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterAffectionAndTraitMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterAffectionAndTraitMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_affection_and_trait_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterAffectionsAndTraits"));

        // Seed real Trait rows from the imported Compêndio de Regras pipeline so the CharacterTrait
        // FK below genuinely targets a real, seeded Trait row rather than a synthetic one.
        var caracteristicasMarkdown = RulesDataProvider.ReadResource("Caracteristicas.md");
        await TraitSeeder.SeedAsync(db, caracteristicasMarkdown);
        var trait = await db.Traits.FirstAsync();

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@afftest.com", Email = "gm@afftest.com", Nickname = "AffTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@afftest.com", Email = "player@afftest.com", Nickname = "AffTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        db.Set<CharacterAffection>().Add(new CharacterAffection { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Nome = "Amigo de infância", Favorabilidade = 5 });
        db.Set<CharacterTrait>().Add(new CharacterTrait { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, TraitId = trait.Id, Polaridade = trait.Polaridade });
        await db.SaveChangesAsync();

        (await db.Set<CharacterAffection>().CountAsync()).Should().Be(1);
        (await db.Set<CharacterTrait>().CountAsync()).Should().Be(1);
    }
}
