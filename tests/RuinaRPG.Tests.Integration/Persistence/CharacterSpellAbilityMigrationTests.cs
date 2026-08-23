using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterSpellAbilityMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterSpellAbilityMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_character_spell_ability_tables_with_a_cascading_effects_relationship()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterSpellAbilities"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@charspelltest.com", Email = "gm@charspelltest.com", Nickname = "CharSpellTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@charspelltest.com", Email = "player@charspelltest.com", Nickname = "CharSpellTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        var spellAbility = new CharacterSpellAbility
        {
            Id = Guid.NewGuid(),
            CharacterSheetId = sheet.Id,
            Nome = "Bola de Fogo",
            Tipo = SpellAbilityTipo.Magia,
            Grau = 3,
            GastoEmPI = 12,
            Custo = 15,
            Descricao = "Uma explosão de fogo."
        };
        spellAbility.Efeitos.Add(new CharacterSpellAbilityEffect { Id = Guid.NewGuid(), CharacterSpellAbilityId = spellAbility.Id, EfeitoNome = "Dano", Quantidade = 4, CustoPI = 8 });
        db.CharacterSpellAbilities.Add(spellAbility);
        await db.SaveChangesAsync();

        (await db.CharacterSpellAbilities.CountAsync()).Should().Be(1);
        (await db.CharacterSpellAbilityEffects.CountAsync()).Should().Be(1);

        db.CharacterSpellAbilities.Remove(spellAbility);
        await db.SaveChangesAsync();

        // Deleting the spell/ability cascades to its own effects (they're meaningless without their
        // parent) — this is a different cascade than the R0006 "deleting a bank entry doesn't affect
        // a ficha's copy" guarantee, which is about SourceBankEntryId not being a cascading FK at all.
        (await db.CharacterSpellAbilityEffects.CountAsync()).Should().Be(0);
    }
}
