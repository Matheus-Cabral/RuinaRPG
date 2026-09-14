using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CreatureExclusiveTraitMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CreatureExclusiveTraitMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_CreatureExclusiveTraits_and_lets_a_CreatureTrait_reference_either_catalog()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCreatureExclusiveTraits"));

        var exclusiveTrait = new CreatureExclusiveTrait
        {
            Id = Guid.NewGuid(),
            Nome = "Regeneração Bestial",
            Descricao = "Recupera Vitalidade a cada rodada.",
            Custo = 3,
            Polaridade = Polaridade.Positiva,
            RequerEspecificacao = false,
        };
        db.Set<CreatureExclusiveTrait>().Add(exclusiveTrait);

        var normalTrait = new RuinaRPG.Infrastructure.Rules.Trait
        {
            Id = Guid.NewGuid(),
            Nome = "Coragem (teste exclusivo criatura)",
            Descricao = "Descrição de teste.",
            Custo = 1,
            Polaridade = Polaridade.Positiva,
        };
        db.Traits.Add(normalTrait);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@creatureexclusivetest.com", Email = "gm@creatureexclusivetest.com", Nickname = "CreatureExclusiveTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var sheet = new CreatureSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        db.CreatureSheets.Add(sheet);
        await db.SaveChangesAsync();

        // Two CreatureTrait rows on the same sheet, each pointing at a different catalog — exactly
        // one of TraitId/CreatureExclusiveTraitId is set per row, mirroring how
        // EncounterParticipant.SourceCharacterSheetId/SourceNpcSheetId/SourceCreatureSheetId already
        // does this for three possible sources instead of two.
        db.CreatureTraits.Add(new CreatureTrait { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, TraitId = normalTrait.Id, Polaridade = normalTrait.Polaridade });
        db.CreatureTraits.Add(new CreatureTrait { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, CreatureExclusiveTraitId = exclusiveTrait.Id, Polaridade = exclusiveTrait.Polaridade });
        await db.SaveChangesAsync();

        var rows = await db.CreatureTraits.Where(t => t.CreatureSheetId == sheet.Id).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().ContainSingle(r => r.TraitId == normalTrait.Id && r.CreatureExclusiveTraitId == null);
        rows.Should().ContainSingle(r => r.CreatureExclusiveTraitId == exclusiveTrait.Id && r.TraitId == null);

        (await db.Set<CreatureExclusiveTrait>().SingleAsync(t => t.Id == exclusiveTrait.Id)).Nome.Should().Be("Regeneração Bestial");
    }
}
