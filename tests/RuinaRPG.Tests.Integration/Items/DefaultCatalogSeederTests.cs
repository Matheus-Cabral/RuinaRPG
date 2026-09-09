using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Items;

public class DefaultCatalogSeederTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DefaultCatalogSeederTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        // PostgresFixture is an IClassFixture: its single container/database is shared by every
        // fact in this class. Reset both tables so each test starts from the empty state its body
        // assumes, instead of depending on sibling-test ordering.
        await db.Items.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();

        return db;
    }

    private static ApplicationUser NewGm(string nickname) =>
        new() { Id = Guid.NewGuid(), UserName = $"{nickname}@catalogseedtest.com", Email = $"{nickname}@catalogseedtest.com", Nickname = nickname, Role = UserRole.GM };

    [Fact]
    public async Task SeedForNewGmAsync_creates_one_catalog_item_per_DefaultCatalogItems_row()
    {
        await using var db = await NewDbAsync();
        var gm = NewGm("NovoGm");
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        await DefaultCatalogSeeder.SeedForNewGmAsync(db, gm.Id);

        var expectedCount = DefaultCatalogItems.Build(Guid.NewGuid()).Count;
        (await db.Items.CountAsync(i => i.GmId == gm.Id)).Should().Be(expectedCount);
    }

    [Fact]
    public async Task SeedMissingAsync_seeds_every_GM_with_zero_items_and_skips_GMs_that_already_have_some()
    {
        await using var db = await NewDbAsync();
        var gmSemItens = NewGm("GmSemItens");
        var gmComItens = NewGm("GmComItens");
        db.Users.AddRange(gmSemItens, gmComItens);
        await db.SaveChangesAsync();

        // gmComItens already has a (single, custom) item — SeedMissingAsync must leave it alone.
        db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>().Add(new RuinaRPG.Infrastructure.Items.ItemGeral
        {
            Id = Guid.NewGuid(), GmId = gmComItens.Id, Nome = "Item Próprio do GM", Peso = 1, Preco = 10,
        });
        await db.SaveChangesAsync();

        var seededCount = await DefaultCatalogSeeder.SeedMissingAsync(db);

        seededCount.Should().Be(1); // only gmSemItens
        var expectedCount = DefaultCatalogItems.Build(Guid.NewGuid()).Count;
        (await db.Items.CountAsync(i => i.GmId == gmSemItens.Id)).Should().Be(expectedCount);
        (await db.Items.CountAsync(i => i.GmId == gmComItens.Id)).Should().Be(1); // untouched
    }

    [Fact]
    public async Task SeedMissingAsync_rerun_is_idempotent_once_every_GM_has_items()
    {
        await using var db = await NewDbAsync();
        var gm = NewGm("GmIdempotente");
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var firstRun = await DefaultCatalogSeeder.SeedMissingAsync(db);
        var secondRun = await DefaultCatalogSeeder.SeedMissingAsync(db);

        firstRun.Should().Be(1);
        secondRun.Should().Be(0);
        var expectedCount = DefaultCatalogItems.Build(Guid.NewGuid()).Count;
        (await db.Items.CountAsync(i => i.GmId == gm.Id)).Should().Be(expectedCount); // not duplicated
    }

    [Fact]
    public void Build_creates_only_the_four_item_types_present_in_the_source_document_no_Artefatos()
    {
        var items = DefaultCatalogItems.Build(Guid.NewGuid());

        items.Should().NotBeEmpty();
        items.OfType<RuinaRPG.Infrastructure.Items.Artefato>().Should().BeEmpty();
        items.OfType<RuinaRPG.Infrastructure.Items.Arma>().Should().NotBeEmpty();
        items.OfType<RuinaRPG.Infrastructure.Items.Armadura>().Should().NotBeEmpty();
        items.OfType<RuinaRPG.Infrastructure.Items.Escudo>().Should().NotBeEmpty();
        items.OfType<RuinaRPG.Infrastructure.Items.ItemGeral>().Should().NotBeEmpty();
    }
}
