using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Persistence;

public class SpellAbilityBankMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public SpellAbilityBankMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_bank_tables_with_a_cascading_effects_relationship()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddSpellAbilityBank"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@banktest.com", Email = "gm@banktest.com", Nickname = "BankTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var entry = new SpellAbilityBankEntry
        {
            Id = Guid.NewGuid(),
            GmId = gm.Id,
            Nome = "Bola de Fogo",
            Tipo = SpellAbilityTipo.Magia,
            Grau = 3,
            GastoEmPI = 12,
            Custo = 15,
            Descricao = "Uma explosão de fogo."
        };
        entry.Efeitos.Add(new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = entry.Id, EfeitoNome = "Dano", Quantidade = 4, CustoPI = 8 });
        db.SpellAbilityBankEntries.Add(entry);
        await db.SaveChangesAsync();

        (await db.SpellAbilityBankEntries.CountAsync()).Should().Be(1);
        (await db.SpellAbilityBankEffects.CountAsync()).Should().Be(1);

        db.SpellAbilityBankEntries.Remove(entry);
        await db.SaveChangesAsync();

        // Deleting the entry cascades to its own effects (they're meaningless without their parent) —
        // this is NOT the R0006 "deleting doesn't affect a ficha's copy" guarantee, which is about a
        // DIFFERENT table (a future CharacterSpellAbilities row) never referencing this one by a
        // cascading FK at all.
        (await db.SpellAbilityBankEffects.CountAsync()).Should().Be(0);
    }
}
