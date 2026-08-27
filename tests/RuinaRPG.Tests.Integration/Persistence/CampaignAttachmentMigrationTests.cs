using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CampaignAttachmentMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CampaignAttachmentMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CampaignAttachments_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCampaignAttachments"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@attachmenttest.com", Email = "gm@attachmenttest.com", Nickname = "AttachmentTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var item = new ItemGeral { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Corda", Peso = 0.5m, Preco = 5 };
        db.Set<ItemGeral>().Add(item);
        await db.SaveChangesAsync();

        var attachmentId = Guid.NewGuid();
        db.CampaignAttachments.Add(new CampaignAttachment
        {
            Id = attachmentId,
            CampaignId = campaign.Id,
            ItemId = item.Id,
        });
        await db.SaveChangesAsync();

        var stored = await db.CampaignAttachments.SingleAsync(a => a.Id == attachmentId);
        stored.CampaignId.Should().Be(campaign.Id);
        stored.ItemId.Should().Be(item.Id);
        stored.NpcSheetId.Should().BeNull();
        stored.CreatureSheetId.Should().BeNull();
        stored.SpellAbilityBankEntryId.Should().BeNull();
        stored.ImageId.Should().BeNull();
        stored.IsPublic.Should().BeFalse();
        stored.NpcNomePublico.Should().BeFalse();
        stored.NpcImagemPublica.Should().BeFalse();
        stored.CreatureNomePublico.Should().BeFalse();
        stored.CreatureImagemPublica.Should().BeFalse();
    }
}
