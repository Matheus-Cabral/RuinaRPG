using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterAttributeAndSkillMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterAttributeAndSkillMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_attribute_and_skill_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterAttributesAndSkills"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@attrtest.com", Email = "gm@attrtest.com", Nickname = "AttrTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@attrtest.com", Email = "player@attrtest.com", Nickname = "AttrTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        db.CharacterAttributes.Add(new CharacterAttribute { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Atributo = Atributo.Forca, Gasto = 3 });
        db.CharacterSkills.Add(new CharacterSkill { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Pericia = Pericia.Atletismo, Gasto = 6 });
        await db.SaveChangesAsync();

        (await db.CharacterAttributes.CountAsync()).Should().Be(1);
        (await db.CharacterSkills.CountAsync()).Should().Be(1);
    }
}
