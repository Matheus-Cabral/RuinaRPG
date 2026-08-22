using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterArsenalMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterArsenalMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_weapon_armor_and_shield_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterArsenal"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@arsenaltest.com", Email = "gm@arsenaltest.com", Nickname = "ArsenalTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@arsenaltest.com", Email = "player@arsenaltest.com", Nickname = "ArsenalTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        var weaponItem = new Arma { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Espada", Peso = 1, Preco = 10, DurabilidadeMaxima = 20 };
        db.Set<Arma>().Add(weaponItem);
        await db.SaveChangesAsync();

        db.CharacterWeapons.Add(new CharacterWeapon { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, ItemId = weaponItem.Id, IsEquipped = true, DurabilidadeAtual = 20 });
        db.CharacterArmorSlots.Add(new CharacterArmorSlot { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Slot = ArmorSlotType.Capacete });
        await db.SaveChangesAsync();

        (await db.CharacterWeapons.CountAsync()).Should().Be(1);
        (await db.CharacterArmorSlots.CountAsync()).Should().Be(1);
    }
}
