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

        var result = await TraitSeeder.SeedAsync(db, Markdown);

        result.Inserted.Should().Be(2);
        result.Updated.Should().Be(0);
        (await db.Traits.CountAsync()).Should().Be(2);
        (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Custo.Should().Be(1);
        // "Alergia" is one of the curated traits that RequerEspecificacao — confirms the flag from
        // TraitSeedParser.Parse survives into the persisted row on insert, not just on re-parse.
        (await db.Traits.SingleAsync(t => t.Nome == "Alergia")).RequerEspecificacao.Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_rerun_with_unchanged_source_inserts_and_updates_nothing()
    {
        await using var db = await NewDbAsync();
        await TraitSeeder.SeedAsync(db, Markdown);
        var existingId = (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Id;

        // Re-run against the SAME source: nothing new to insert, nothing to update, and the
        // existing row's Id is untouched.
        var secondRun = await TraitSeeder.SeedAsync(db, Markdown);

        secondRun.Inserted.Should().Be(0);
        secondRun.Updated.Should().Be(0);
        (await db.Traits.CountAsync()).Should().Be(2);
        (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Id.Should().Be(existingId);
    }

    [Fact]
    public async Task SeedAsync_does_not_insert_a_new_row_when_only_the_description_changed()
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

        var result = await TraitSeeder.SeedAsync(db, revisedMarkdown);

        // Matched by (Nome, Custo, Polaridade), not by Descricao — a changed description for the
        // SAME name+cost+polaridade is treated as the same trait already present, not re-inserted
        // (and Descricao itself is not synced on update — only RequerEspecificacao is).
        result.Inserted.Should().Be(0);
        (await db.Traits.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SeedAsync_updates_RequerEspecificacao_on_an_existing_row_when_the_source_now_flags_it_differently()
    {
        await using var db = await NewDbAsync();

        // Simulate a row seeded before the RequerEspecificacao flag existed: "Alergia" matches a
        // real curated trait name, but is persisted here with the flag wrong (false).
        var legacyId = Guid.NewGuid();
        db.Traits.Add(new Trait
        {
            Id = legacyId,
            Nome = "Alergia",
            Descricao = "o Personagem é alérgico a alguma coisa.",
            Custo = -1,
            Polaridade = RuinaRPG.Domain.Enums.Polaridade.Negativa,
            RequerEspecificacao = false,
        });
        await db.SaveChangesAsync();

        var result = await TraitSeeder.SeedAsync(db, Markdown);

        result.Inserted.Should().Be(1); // only "Alfabetizado" is new
        result.Updated.Should().Be(1); // "Alergia"'s flag gets corrected in place
        (await db.Traits.CountAsync()).Should().Be(2);
        var alergia = await db.Traits.SingleAsync(t => t.Nome == "Alergia");
        alergia.Id.Should().Be(legacyId); // updated in place, not replaced
        alergia.RequerEspecificacao.Should().BeTrue();
    }
}
