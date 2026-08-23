using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterInventoryAndArtifactMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterInventoryAndArtifactMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_inventory_and_artifact_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterInventoryAndArtifacts"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@invtest.com", Email = "gm@invtest.com", Nickname = "InvTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@invtest.com", Email = "player@invtest.com", Nickname = "InvTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        var generalItem = new ItemGeral { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Corda", Peso = 1, Preco = 5 };
        db.Set<ItemGeral>().Add(generalItem);
        var artifactItem = new Artefato { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Anel do Poder", Peso = 0.1m, Preco = 500 };
        db.Set<Artefato>().Add(artifactItem);
        await db.SaveChangesAsync();

        db.Set<CharacterInventoryItem>().Add(new CharacterInventoryItem { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, ItemId = generalItem.Id, Qtd = 3 });
        db.Set<CharacterArtifact>().Add(new CharacterArtifact { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, ArtifactItemId = artifactItem.Id });
        await db.SaveChangesAsync();

        (await db.Set<CharacterInventoryItem>().CountAsync()).Should().Be(1);
        (await db.Set<CharacterArtifact>().CountAsync()).Should().Be(1);
    }
}
