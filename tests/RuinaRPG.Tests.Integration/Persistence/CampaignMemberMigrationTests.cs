using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CampaignMemberMigrationTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _fixture;

    public CampaignMemberMigrationTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CampaignMembers_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@membertest.com", Email = "gm@membertest.com", Nickname = "MemberTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@membertest.com", Email = "player@membertest.com", Nickname = "MemberTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();

        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        db.CampaignMembers.Add(new CampaignMember { Id = Guid.NewGuid(), CampaignId = campaign.Id, UserId = player.Id });
        await db.SaveChangesAsync();

        (await db.CampaignMembers.CountAsync()).Should().Be(1);
    }
}
