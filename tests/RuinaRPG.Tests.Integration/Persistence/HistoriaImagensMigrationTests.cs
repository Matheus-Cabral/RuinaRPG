using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class HistoriaImagensMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public HistoriaImagensMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    private RuinaRpgDbContext NewContext() =>
        new(new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options);

    private static async Task<(Guid CharacterSheetId, Guid NpcSheetId, Guid ImageA, Guid ImageB)> SeedAsync(RuinaRpgDbContext db, string tag)
    {
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"gm{tag}@histimg.com", Email = $"gm{tag}@histimg.com", Nickname = $"HistImgGm{tag}", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        var character = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = gm.Id };
        var npc = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        db.CharacterSheets.Add(character);
        db.NpcSheets.Add(npc);
        var a = new Image { Id = Guid.NewGuid(), Path = $"{Guid.NewGuid()}.png", ContentType = "image/png", UploadedByUserId = gm.Id, CreatedAt = DateTime.UtcNow };
        var b = new Image { Id = Guid.NewGuid(), Path = $"{Guid.NewGuid()}.png", ContentType = "image/png", UploadedByUserId = gm.Id, CreatedAt = DateTime.UtcNow };
        db.Images.AddRange(a, b);
        await db.SaveChangesAsync();
        return (character.Id, npc.Id, a.Id, b.Id);
    }

    [Fact]
    public async Task Migrate_creates_the_historia_image_tables_and_they_keep_the_order()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddImagensDaHistoria"));

        var (characterId, npcId, a, b) = await SeedAsync(db, "1");
        db.CharacterSheetHistoriaImages.AddRange(
            new CharacterSheetHistoriaImage { CharacterSheetId = characterId, ImageId = a, Ordem = 1 },
            new CharacterSheetHistoriaImage { CharacterSheetId = characterId, ImageId = b, Ordem = 0 });
        db.NpcSheetHistoriaImages.AddRange(
            new NpcSheetHistoriaImage { NpcSheetId = npcId, ImageId = a, Ordem = 0 },
            new NpcSheetHistoriaImage { NpcSheetId = npcId, ImageId = b, Ordem = 1 });
        await db.SaveChangesAsync();

        (await db.CharacterSheetHistoriaImages.Where(i => i.CharacterSheetId == characterId).OrderBy(i => i.Ordem).Select(i => i.ImageId).ToListAsync()).Should().Equal(b, a);
        (await db.NpcSheetHistoriaImages.Where(i => i.NpcSheetId == npcId).OrderBy(i => i.Ordem).Select(i => i.ImageId).ToListAsync()).Should().Equal(a, b);
    }

    [Fact]
    public async Task The_same_image_cannot_be_attached_twice_to_the_same_sheet()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var (characterId, _, a, _) = await SeedAsync(db, "2");
        db.CharacterSheetHistoriaImages.Add(new CharacterSheetHistoriaImage { CharacterSheetId = characterId, ImageId = a, Ordem = 0 });
        await db.SaveChangesAsync();

        await using var other = NewContext();
        other.CharacterSheetHistoriaImages.Add(new CharacterSheetHistoriaImage { CharacterSheetId = characterId, ImageId = a, Ordem = 1 });

        await FluentActions.Awaiting(() => other.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Deleting_the_sheet_or_the_image_removes_the_gallery_rows()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var (characterId, npcId, a, b) = await SeedAsync(db, "3");
        db.CharacterSheetHistoriaImages.AddRange(
            new CharacterSheetHistoriaImage { CharacterSheetId = characterId, ImageId = a, Ordem = 0 },
            new CharacterSheetHistoriaImage { CharacterSheetId = characterId, ImageId = b, Ordem = 1 });
        db.NpcSheetHistoriaImages.AddRange(
            new NpcSheetHistoriaImage { NpcSheetId = npcId, ImageId = a, Ordem = 0 },
            new NpcSheetHistoriaImage { NpcSheetId = npcId, ImageId = b, Ordem = 1 });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Images\" WHERE \"Id\" = {a}");
        (await db.CharacterSheetHistoriaImages.AsNoTracking().Where(i => i.CharacterSheetId == characterId).Select(i => i.ImageId).ToListAsync()).Should().Equal(b);
        (await db.NpcSheetHistoriaImages.AsNoTracking().Where(i => i.NpcSheetId == npcId).Select(i => i.ImageId).ToListAsync()).Should().Equal(b);

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"CharacterSheets\" WHERE \"Id\" = {characterId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"NpcSheets\" WHERE \"Id\" = {npcId}");
        (await db.CharacterSheetHistoriaImages.AsNoTracking().AnyAsync(i => i.CharacterSheetId == characterId)).Should().BeFalse();
        (await db.NpcSheetHistoriaImages.AsNoTracking().AnyAsync(i => i.NpcSheetId == npcId)).Should().BeFalse();
        (await db.Images.AsNoTracking().AnyAsync(i => i.Id == b)).Should().BeTrue("apagar a ficha não apaga a imagem");
    }
}
