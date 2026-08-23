using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class NpcChildTableMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public NpcChildTableMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_all_14_npc_child_tables_and_each_round_trips_a_row()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddNpcChildTables"));

        // Seed real Trait rows from the imported Compêndio de Regras pipeline so NpcTrait's FK
        // genuinely targets a real, seeded Trait row rather than a synthetic one.
        var caracteristicasMarkdown = RulesDataProvider.ReadResource("Caracteristicas.md");
        await TraitSeeder.SeedAsync(db, caracteristicasMarkdown);
        var trait = await db.Traits.FirstAsync();

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@npcchildtest.com", Email = "gm@npcchildtest.com", Nickname = "NpcChildTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var sheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        db.NpcSheets.Add(sheet);

        var weaponItem = new Arma { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Espada", Peso = 1, Preco = 10, DurabilidadeMaxima = 20 };
        var armorItem = new Armadura { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Elmo", Peso = 1, Preco = 10, DurabilidadeMaxima = 10 };
        var shieldItem = new Escudo { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Broquel", Peso = 1, Preco = 10, DurabilidadeMaxima = 10 };
        var generalItem = new ItemGeral { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Corda", Peso = 1, Preco = 5 };
        var artifactItem = new Artefato { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Anel do Poder", Peso = 0.1m, Preco = 500 };
        db.Set<Arma>().Add(weaponItem);
        db.Set<Armadura>().Add(armorItem);
        db.Set<Escudo>().Add(shieldItem);
        db.Set<ItemGeral>().Add(generalItem);
        db.Set<Artefato>().Add(artifactItem);
        await db.SaveChangesAsync();

        db.NpcAttributes.Add(new NpcAttribute { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Atributo = Atributo.Forca, Gasto = 3 });
        db.NpcSkills.Add(new NpcSkill { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Pericia = Pericia.Atletismo, Gasto = 6 });
        db.NpcAffinities.Add(new NpcAffinity { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Elemento = Elemento.Fogo, SubElemento = SubElemento.Vida, CaminhoNome = "Caminho da Fênix", Experiencia = 10 });
        db.NpcWeapons.Add(new NpcWeapon { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, ItemId = weaponItem.Id, IsEquipped = true, DurabilidadeAtual = 20 });
        db.NpcArmorSlots.Add(new NpcArmorSlot { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Slot = ArmorSlotType.Capacete, ItemId = armorItem.Id, DurabilidadeAtual = 10 });
        db.NpcShields.Add(new NpcShield { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, ItemId = shieldItem.Id, IsEquipped = true, DurabilidadeAtual = 10 });
        db.NpcRunes.Add(new NpcRune { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Nome = "Runa do Fogo", Descricao = "Queima o alvo.", Grau = 1 });
        db.NpcMasteries.Add(new NpcMastery { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Nome = "Maestria em Pontaria", Pericia = Pericia.Pontaria, Atributo = Atributo.Destreza, GastoMaestria = 3 });
        db.NpcInventoryItems.Add(new NpcInventoryItem { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, ItemId = generalItem.Id, Qtd = 3 });
        db.NpcArtifacts.Add(new NpcArtifact { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, ArtifactItemId = artifactItem.Id });
        db.NpcAffections.Add(new NpcAffection { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Nome = "Amigo de infância", Favorabilidade = 5 });
        db.NpcTraits.Add(new NpcTrait { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, TraitId = trait.Id, Polaridade = trait.Polaridade });

        var spellAbility = new NpcSpellAbility
        {
            Id = Guid.NewGuid(),
            NpcSheetId = sheet.Id,
            Nome = "Bola de Fogo",
            Tipo = SpellAbilityTipo.Magia,
            Grau = 3,
            GastoEmPI = 12,
            Custo = 15,
            Descricao = "Uma explosão de fogo."
        };
        spellAbility.Efeitos.Add(new NpcSpellAbilityEffect { Id = Guid.NewGuid(), NpcSpellAbilityId = spellAbility.Id, EfeitoNome = "Dano", Quantidade = 4, CustoPI = 8 });
        db.NpcSpellAbilities.Add(spellAbility);

        await db.SaveChangesAsync();

        (await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheet.Id)).Gasto.Should().Be(3);
        (await db.NpcSkills.SingleAsync(s => s.NpcSheetId == sheet.Id)).Gasto.Should().Be(6);
        (await db.NpcAffinities.SingleAsync(a => a.NpcSheetId == sheet.Id)).CaminhoNome.Should().Be("Caminho da Fênix");
        (await db.NpcWeapons.SingleAsync(w => w.NpcSheetId == sheet.Id)).ItemId.Should().Be(weaponItem.Id);
        (await db.NpcArmorSlots.SingleAsync(a => a.NpcSheetId == sheet.Id)).ItemId.Should().Be(armorItem.Id);
        (await db.NpcShields.SingleAsync(s => s.NpcSheetId == sheet.Id)).ItemId.Should().Be(shieldItem.Id);
        (await db.NpcRunes.SingleAsync(r => r.NpcSheetId == sheet.Id)).Nome.Should().Be("Runa do Fogo");
        (await db.NpcMasteries.SingleAsync(m => m.NpcSheetId == sheet.Id)).GastoMaestria.Should().Be(3);
        (await db.NpcInventoryItems.SingleAsync(i => i.NpcSheetId == sheet.Id)).Qtd.Should().Be(3);
        (await db.NpcArtifacts.SingleAsync(a => a.NpcSheetId == sheet.Id)).ArtifactItemId.Should().Be(artifactItem.Id);
        (await db.NpcAffections.SingleAsync(a => a.NpcSheetId == sheet.Id)).Favorabilidade.Should().Be(5);
        (await db.NpcTraits.SingleAsync(t => t.NpcSheetId == sheet.Id)).TraitId.Should().Be(trait.Id);
        (await db.NpcSpellAbilities.SingleAsync(e => e.NpcSheetId == sheet.Id)).Nome.Should().Be("Bola de Fogo");
        (await db.NpcSpellAbilityEffects.SingleAsync(ef => ef.NpcSpellAbilityId == spellAbility.Id)).EfeitoNome.Should().Be("Dano");

        // Deleting the NpcSheet cascades to every child table (they're meaningless without their
        // parent) and to the spell/ability's own effects.
        db.NpcSheets.Remove(sheet);
        await db.SaveChangesAsync();

        (await db.NpcAttributes.AnyAsync(a => a.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcSkills.AnyAsync(s => s.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcAffinities.AnyAsync(a => a.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcWeapons.AnyAsync(w => w.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcArmorSlots.AnyAsync(a => a.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcShields.AnyAsync(s => s.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcRunes.AnyAsync(r => r.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcMasteries.AnyAsync(m => m.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcInventoryItems.AnyAsync(i => i.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcArtifacts.AnyAsync(a => a.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcAffections.AnyAsync(a => a.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcTraits.AnyAsync(t => t.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcSpellAbilities.AnyAsync(e => e.NpcSheetId == sheet.Id)).Should().BeFalse();
        (await db.NpcSpellAbilityEffects.AnyAsync(ef => ef.NpcSpellAbilityId == spellAbility.Id)).Should().BeFalse();

        // Trait itself is NOT cascade-deleted by an NpcTrait row (Restrict) — confirm it survived.
        (await db.Traits.AnyAsync(t => t.Id == trait.Id)).Should().BeTrue();
    }
}
