using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class SpellAbilityCriadaNaFichaMigrationTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _fixture;

    public SpellAbilityCriadaNaFichaMigrationTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_adds_CriadaNaFicha_false_by_default_to_the_three_sheet_spell_ability_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        foreach (var tabela in new[] { "CharacterSpellAbilities", "NpcSpellAbilities", "CreatureSpellAbilities" })
        {
            var padrao = await db.Database.SqlQuery<string>(
                $"SELECT column_default AS \"Value\" FROM information_schema.columns WHERE table_name = {tabela} AND column_name = 'CriadaNaFicha'").SingleAsync();
            padrao.Should().Be("false");
        }
    }
}
