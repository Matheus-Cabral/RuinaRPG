using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CreatureChildTableMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CreatureChildTableMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_all_12_creature_child_tables_and_each_round_trips_a_row()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCreatureChildTables"));

        // Seed real Trait rows from the imported Compêndio de Regras pipeline so CreatureTrait's
        // FK genuinely targets a real, seeded Trait row rather than a synthetic one.
        var caracteristicasMarkdown = RulesDataProvider.ReadResource("Caracteristicas.md");
        await TraitSeeder.SeedAsync(db, caracteristicasMarkdown);
        var trait = await db.Traits.FirstAsync();

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@creaturechildtest.com", Email = "gm@creaturechildtest.com", Nickname = "CreatureChildTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var sheet = new CreatureSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        db.CreatureSheets.Add(sheet);

        var weaponItem = new Arma { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Garras", Peso = 1, Preco = 10, DurabilidadeMaxima = 20 };
        var armorItem = new Armadura { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Escama", Peso = 1, Preco = 10, DurabilidadeMaxima = 10 };
        var shieldItem = new Escudo { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Broquel", Peso = 1, Preco = 10, DurabilidadeMaxima = 10 };
        var spoilItem = new ItemGeral { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Presa", Peso = 1, Preco = 5 };
        var artifactItem = new Artefato { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Anel do Poder", Peso = 0.1m, Preco = 500 };
        db.Set<Arma>().Add(weaponItem);
        db.Set<Armadura>().Add(armorItem);
        db.Set<Escudo>().Add(shieldItem);
        db.Set<ItemGeral>().Add(spoilItem);
        db.Set<Artefato>().Add(artifactItem);
        await db.SaveChangesAsync();

        db.CreatureAttributes.Add(new CreatureAttribute { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Atributo = AtributoCriatura.Forca, Gasto = 3 });
        db.CreatureSkills.Add(new CreatureSkill { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Pericia = Pericia.Atletismo, Gasto = 6 });
        db.CreatureMasteries.Add(new CreatureMastery { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Nome = "Maestria em Pontaria", Pericia = Pericia.Pontaria, Atributo = AtributoCriatura.Destreza, GastoMaestria = 3 });
        db.CreatureWeapons.Add(new CreatureWeapon { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, ItemId = weaponItem.Id, IsEquipped = true, DurabilidadeAtual = 20 });
        db.CreatureArmorSlots.Add(new CreatureArmorSlot { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Slot = ArmorSlotType.Capacete, ItemId = armorItem.Id, DurabilidadeAtual = 10 });
        db.CreatureShields.Add(new CreatureShield { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, ItemId = shieldItem.Id, IsEquipped = true, DurabilidadeAtual = 10 });
        db.CreatureSpoils.Add(new CreatureSpoil { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, ItemId = spoilItem.Id, Qtd = 3, DT = 12 });
        db.CreatureArtifacts.Add(new CreatureArtifact { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, ArtifactItemId = artifactItem.Id });
        db.CreatureAffections.Add(new CreatureAffection { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Nome = "Domesticado pelo caçador", Favorabilidade = 5 });
        db.CreatureTraits.Add(new CreatureTrait { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, TraitId = trait.Id, Polaridade = trait.Polaridade });

        var spellAbility = new CreatureSpellAbility
        {
            Id = Guid.NewGuid(),
            CreatureSheetId = sheet.Id,
            Nome = "Sopro de Fogo",
            Tipo = SpellAbilityTipo.Habilidade,
            Grau = 3,
            GastoEmPI = 12,
            Custo = 15,
            Descricao = "Uma explosão de fogo."
        };
        spellAbility.Efeitos.Add(new CreatureSpellAbilityEffect { Id = Guid.NewGuid(), CreatureSpellAbilityId = spellAbility.Id, EfeitoNome = "Dano", Quantidade = 4, CustoPI = 8 });
        db.CreatureSpellAbilities.Add(spellAbility);

        // Weapon hybrid model: ItemId null = a manual natural attack (garras, presas, etc.), not
        // a Catálogo item — the FK must be nullable so this round-trips without violating it.
        var naturalAttack = new CreatureWeapon
        {
            Id = Guid.NewGuid(),
            CreatureSheetId = sheet.Id,
            ItemId = null,
            IsEquipped = true,
            DurabilidadeAtual = null,
            ManualNome = "Garras Afiadas",
            ManualTipoDeDano = TipoDeDano.Cortante,
            ManualDados = "2d6",
            ManualDano = 7
        };
        db.CreatureWeapons.Add(naturalAttack);

        await db.SaveChangesAsync();

        (await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheet.Id)).Gasto.Should().Be(3);
        (await db.CreatureSkills.SingleAsync(s => s.CreatureSheetId == sheet.Id)).Gasto.Should().Be(6);
        (await db.CreatureMasteries.SingleAsync(m => m.CreatureSheetId == sheet.Id)).GastoMaestria.Should().Be(3);
        (await db.CreatureWeapons.SingleAsync(w => w.CreatureSheetId == sheet.Id && w.ItemId == weaponItem.Id)).ItemId.Should().Be(weaponItem.Id);
        (await db.CreatureArmorSlots.SingleAsync(a => a.CreatureSheetId == sheet.Id)).ItemId.Should().Be(armorItem.Id);
        (await db.CreatureShields.SingleAsync(s => s.CreatureSheetId == sheet.Id)).ItemId.Should().Be(shieldItem.Id);
        (await db.CreatureSpoils.SingleAsync(i => i.CreatureSheetId == sheet.Id)).Qtd.Should().Be(3);
        (await db.CreatureArtifacts.SingleAsync(a => a.CreatureSheetId == sheet.Id)).ArtifactItemId.Should().Be(artifactItem.Id);
        (await db.CreatureAffections.SingleAsync(a => a.CreatureSheetId == sheet.Id)).Favorabilidade.Should().Be(5);
        (await db.CreatureTraits.SingleAsync(t => t.CreatureSheetId == sheet.Id)).TraitId.Should().Be(trait.Id);
        (await db.CreatureSpellAbilities.SingleAsync(e => e.CreatureSheetId == sheet.Id)).Nome.Should().Be("Sopro de Fogo");
        (await db.CreatureSpellAbilityEffects.SingleAsync(ef => ef.CreatureSpellAbilityId == spellAbility.Id)).EfeitoNome.Should().Be("Dano");

        var roundTrippedNaturalAttack = await db.CreatureWeapons.SingleAsync(w => w.Id == naturalAttack.Id);
        roundTrippedNaturalAttack.ItemId.Should().BeNull();
        roundTrippedNaturalAttack.DurabilidadeAtual.Should().BeNull();
        roundTrippedNaturalAttack.ManualNome.Should().Be("Garras Afiadas");
        roundTrippedNaturalAttack.ManualTipoDeDano.Should().Be(TipoDeDano.Cortante);
        roundTrippedNaturalAttack.ManualDados.Should().Be("2d6");
        roundTrippedNaturalAttack.ManualDano.Should().Be(7);

        // Deleting the CreatureSheet cascades to every child table (they're meaningless without
        // their parent) and to the spell/ability's own effects.
        db.CreatureSheets.Remove(sheet);
        await db.SaveChangesAsync();

        (await db.CreatureAttributes.AnyAsync(a => a.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureSkills.AnyAsync(s => s.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureMasteries.AnyAsync(m => m.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureWeapons.AnyAsync(w => w.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureArmorSlots.AnyAsync(a => a.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureShields.AnyAsync(s => s.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureSpoils.AnyAsync(i => i.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureArtifacts.AnyAsync(a => a.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureAffections.AnyAsync(a => a.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureTraits.AnyAsync(t => t.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureSpellAbilities.AnyAsync(e => e.CreatureSheetId == sheet.Id)).Should().BeFalse();
        (await db.CreatureSpellAbilityEffects.AnyAsync(ef => ef.CreatureSpellAbilityId == spellAbility.Id)).Should().BeFalse();

        // Trait itself is NOT cascade-deleted by a CreatureTrait row (Restrict) — confirm it survived.
        (await db.Traits.AnyAsync(t => t.Id == trait.Id)).Should().BeTrue();
    }
}
