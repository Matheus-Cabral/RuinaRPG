using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class DurabilidadePorRankMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DurabilidadePorRankMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        // PostgresFixture is an IClassFixture: its single container/database is shared by every
        // fact in this class. Reset the tables this class touches so each test starts from the
        // empty state its body assumes, instead of depending on sibling-test ordering.
        await db.DurabilidadesPorRank.ExecuteDeleteAsync();
        await db.Items.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();

        return db;
    }

    private static ApplicationUser NewGm(string nickname) =>
        new() { Id = Guid.NewGuid(), UserName = $"{nickname}@durabilidadetest.com", Email = $"{nickname}@durabilidadetest.com", Nickname = nickname, Role = UserRole.GM };

    [Fact]
    public async Task Migrate_creates_the_AddDurabilidadePorRank_migration()
    {
        await using var db = await NewDbAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddDurabilidadePorRank"));
    }

    [Fact]
    public async Task SeedAsync_inserts_the_eight_ranks_from_the_markdown_and_is_idempotent()
    {
        await using var db = await NewDbAsync();
        var markdown = RulesDataProvider.ReadResource("Tabela de Durabilidade por Rank.md");

        var firstSeed = await DurabilidadePorRankSeeder.SeedAsync(db, markdown);
        var secondSeed = await DurabilidadePorRankSeeder.SeedAsync(db, markdown);

        firstSeed.Should().Be(8);
        secondSeed.Should().Be(0);

        var rows = await db.DurabilidadesPorRank.ToListAsync();
        rows.Should().HaveCount(8);
        rows.Single(r => r.Rank == RankDeItem.F).Should().BeEquivalentTo(new { Durabilidade = (int?)20, Inquebravel = false });
        rows.Single(r => r.Rank == RankDeItem.E).Should().BeEquivalentTo(new { Durabilidade = (int?)45, Inquebravel = false });
        rows.Single(r => r.Rank == RankDeItem.D).Should().BeEquivalentTo(new { Durabilidade = (int?)80, Inquebravel = false });
        rows.Single(r => r.Rank == RankDeItem.C).Should().BeEquivalentTo(new { Durabilidade = (int?)125, Inquebravel = false });
        rows.Single(r => r.Rank == RankDeItem.B).Should().BeEquivalentTo(new { Durabilidade = (int?)180, Inquebravel = false });
        rows.Single(r => r.Rank == RankDeItem.A).Should().BeEquivalentTo(new { Durabilidade = (int?)245, Inquebravel = false });
        rows.Single(r => r.Rank == RankDeItem.S).Should().BeEquivalentTo(new { Durabilidade = (int?)null, Inquebravel = true });
        rows.Single(r => r.Rank == RankDeItem.SS).Should().BeEquivalentTo(new { Durabilidade = (int?)null, Inquebravel = true });
    }

    [Fact]
    public async Task SeedAsync_never_overwrites_a_row_the_Auditor_already_edited()
    {
        await using var db = await NewDbAsync();
        var markdown = RulesDataProvider.ReadResource("Tabela de Durabilidade por Rank.md");
        await DurabilidadePorRankSeeder.SeedAsync(db, markdown);

        var linhaF = await db.DurabilidadesPorRank.SingleAsync(r => r.Rank == RankDeItem.F);
        linhaF.Durabilidade = 99;
        await db.SaveChangesAsync();

        await DurabilidadePorRankSeeder.SeedAsync(db, markdown);

        var reloaded = await db.DurabilidadesPorRank.SingleAsync(r => r.Rank == RankDeItem.F);
        reloaded.Durabilidade.Should().Be(99);
    }

    [Fact]
    public async Task Arma_Armadura_and_Escudo_round_trip_their_own_Rank_column()
    {
        await using var db = await NewDbAsync();
        var gm = NewGm("GmDurabilidade");
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var arma = new Arma { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Espada de Teste", Peso = 1, Preco = 10, Rank = RankDeItem.C };
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Armadura de Teste", Peso = 1, Preco = 10, Rank = RankDeItem.SS };
        var escudo = new Escudo { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Escudo de Teste", Peso = 1, Preco = 10, Rank = RankDeItem.SS };
        db.AddRange(arma, armadura, escudo);
        await db.SaveChangesAsync();

        (await db.Set<Arma>().SingleAsync(a => a.Id == arma.Id)).Rank.Should().Be(RankDeItem.C);
        (await db.Set<Armadura>().SingleAsync(a => a.Id == armadura.Id)).Rank.Should().Be(RankDeItem.SS);
        (await db.Set<Escudo>().SingleAsync(e => e.Id == escudo.Id)).Rank.Should().Be(RankDeItem.SS);
    }
}
