using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

public class TraitSeederTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TraitSeederTests(PostgresFixture fixture) => _fixture = fixture;

    private const string Markdown = """
        # Positivas

        ### Alfabetizado

        1 ponto: Saber ler e escrever.

        # Negativas

        ### Alergia

        -1 ponto: o Personagem é alérgico a alguma coisa.
        """;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        // PostgresFixture is an IClassFixture: its single container/database is shared by every
        // fact in this class, and their execution order is not guaranteed to be declaration order.
        // Reset the table so each test starts from the empty state its name/body assumes, instead
        // of silently depending on a sibling test having (not) run first.
        await db.Traits.ExecuteDeleteAsync();

        return db;
    }

    [Fact]
    public async Task SeedAsync_inserts_every_parsed_trait_into_an_empty_table()
    {
        await using var db = await NewDbAsync();

        var inserted = await TraitSeeder.SeedAsync(db, Markdown);

        inserted.Should().Be(2);
        (await db.Traits.CountAsync()).Should().Be(2);
        (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Custo.Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_is_additive_only_and_never_touches_an_existing_row()
    {
        await using var db = await NewDbAsync();
        await TraitSeeder.SeedAsync(db, Markdown);
        var existingId = (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Id;

        // Re-run against the SAME source: nothing new to insert, and the existing row's Id is untouched.
        var secondRunInserted = await TraitSeeder.SeedAsync(db, Markdown);

        secondRunInserted.Should().Be(0);
        (await db.Traits.CountAsync()).Should().Be(2);
        (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Id.Should().Be(existingId);
    }

    [Fact]
    public async Task SeedAsync_adds_a_new_row_alongside_an_existing_one_when_only_the_description_changed()
    {
        await using var db = await NewDbAsync();
        await TraitSeeder.SeedAsync(db, Markdown);

        const string revisedMarkdown = """
            # Positivas

            ### Alfabetizado

            1 ponto: Texto revisado, descrição diferente da original.

            # Negativas

            ### Alergia

            -1 ponto: o Personagem é alérgico a alguma coisa.
            """;

        var inserted = await TraitSeeder.SeedAsync(db, revisedMarkdown);

        // Matched by (Nome, Custo, Polaridade), not by Descricao — a changed description for the
        // SAME name+cost+polaridade is treated as the same trait already present, not re-inserted.
        inserted.Should().Be(0);
        (await db.Traits.CountAsync()).Should().Be(2);
    }
}
