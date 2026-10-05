using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

// Fixture própria: o teste para o banco num ponto antigo do histórico de migrations, então não pode
// dividir o banco com testes que já migraram até o fim.
public class RequisitosLegadosMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RequisitosLegadosMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task The_three_legacy_fields_become_structured_requirements_and_penalty_text()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(db.Database.GetMigrations().Single(m => m.EndsWith("_AddRequisitosDeEquipamento")));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@requisitos.com", Email = "gm@requisitos.com", Nickname = "RequisitosGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        var gmId = gm.Id;

        var armaDex = await InserirAsync(db, gmId, "Arma", new() { ["RequisitoAtributo"] = "10 Dex" });
        var armaInvertida = await InserirAsync(db, gmId, "Arma", new() { ["RequisitoAtributo"] = "For 8" });
        var armaLivre = await InserirAsync(db, gmId, "Arma", new() { ["RequisitoAtributo"] = "Dex 10 ou For 12" });
        var armaBranco = await InserirAsync(db, gmId, "Arma", new() { ["RequisitoAtributo"] = "   " });
        var armaNula = await InserirAsync(db, gmId, "Arma", new());
        var armadura = await InserirAsync(db, gmId, "Armadura", new() { ["Armadura_RequisitoVigor"] = 12, ["Armadura_Penalidade"] = "-10 Reflexo" });
        var armaduraSoPenalidade = await InserirAsync(db, gmId, "Armadura", new() { ["Armadura_Penalidade"] = "Barulhenta" });
        var escudo = await InserirAsync(db, gmId, "Escudo", new() { ["RequisitoVigor"] = 4, ["Penalidade"] = "-1 Reflexo" });

        await migrator.MigrateAsync();

        var itens = await db.Items.AsNoTracking().ToDictionaryAsync(i => i.Id);
        itens[armaDex].Requisitos!.Atributos.Should().Equal(new RequisitoDeAtributo(Atributo.Destreza, 10));
        itens[armaDex].Requisitos!.SubAtributos.Should().BeEmpty();
        itens[armaDex].Requisitos!.Pericias.Should().BeEmpty();
        itens[armaDex].PenalidadeDeRequisitos.Should().BeNull();
        itens[armaInvertida].Requisitos!.Atributos.Should().Equal(new RequisitoDeAtributo(Atributo.Forca, 8));
        itens[armaLivre].Requisitos.Should().BeNull();
        itens[armaLivre].PenalidadeDeRequisitos!.Texto.Should().Be("Requisito: Dex 10 ou For 12");
        itens[armaBranco].Requisitos.Should().BeNull();
        itens[armaBranco].PenalidadeDeRequisitos.Should().BeNull();
        itens[armaNula].Requisitos.Should().BeNull();
        itens[armadura].Requisitos!.Atributos.Should().Equal(new RequisitoDeAtributo(Atributo.Vigor, 12));
        itens[armadura].PenalidadeDeRequisitos!.Texto.Should().Be("-10 Reflexo");
        itens[armadura].PenalidadeDeRequisitos!.Atributos.Should().BeEmpty();
        itens[armadura].PenalidadeDeRequisitos!.SubAtributos.Should().BeEmpty();
        itens[armadura].PenalidadeDeRequisitos!.Pericias.Should().BeEmpty();
        itens[armaduraSoPenalidade].Requisitos.Should().BeNull();
        itens[armaduraSoPenalidade].PenalidadeDeRequisitos!.Texto.Should().Be("Barulhenta");
        itens[escudo].Requisitos!.Atributos.Should().Equal(new RequisitoDeAtributo(Atributo.Vigor, 4));
        itens[escudo].PenalidadeDeRequisitos!.Texto.Should().Be("-1 Reflexo");
    }

    private static async Task<Guid> InserirAsync(RuinaRpgDbContext db, Guid gmId, string tipo, Dictionary<string, object> extras)
    {
        var id = Guid.NewGuid();
        var colunas = new Dictionary<string, object> { ["Id"] = id, ["GmId"] = gmId, ["Tipo"] = tipo, ["Nome"] = $"{tipo} {id:N}", ["Peso"] = 0m, ["Preco"] = 0 };
        foreach (var (coluna, valor) in extras)
            colunas[coluna] = valor;
        await SchemaInsert.AtCurrentSchemaAsync(db, "Items", colunas);
        return id;
    }
}
