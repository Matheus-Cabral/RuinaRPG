using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class SubcategoriaOptionSchemaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;

    public SubcategoriaOptionSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task SubcategoriaOption_round_trips_and_the_three_item_types_persist_Subcategoria()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();

        var option = new SubcategoriaOption { Id = Guid.NewGuid(), Tipo = ItemTipo.Arma, Facet = SubcategoriaFacet.Familia, Valor = "Varinha" };
        db.SubcategoriaOptions.Add(option);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@subcategoriatest.com", Email = "gm@subcategoriatest.com", Nickname = "SubcategoriaTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        var gmId = gm.Id;
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Teste Armadura", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Peso = 1, Preco = 0 };
        var escudo = new Escudo { Id = Guid.NewGuid(), GmId = gmId, Nome = "Teste Escudo", Subcategoria = "Equipamento inicial - Escudo - Leve - Rodela", Peso = 1, Preco = 0 };
        var artefato = new Artefato { Id = Guid.NewGuid(), GmId = gmId, Nome = "Teste Artefato", Subcategoria = "Equipamento inicial - Artefato - Passivo - Anel", Peso = 0, Preco = 0 };
        db.AddRange(armadura, escudo, artefato);

        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit Teste", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Armadura", Tipo = ItemTipo.Armadura, Qtd = 1, ArmorSlot = ArmorSlotType.Superior };
        db.EquipmentKitChoiceSlots.Add(slot);

        await db.SaveChangesAsync();

        (await db.SubcategoriaOptions.SingleAsync()).Valor.Should().Be("Varinha");
        (await db.Set<Armadura>().SingleAsync()).Subcategoria.Should().Be("Equipamento inicial - Armadura - Leve - Couro");
        (await db.Set<Escudo>().SingleAsync()).Subcategoria.Should().Be("Equipamento inicial - Escudo - Leve - Rodela");
        (await db.Set<Artefato>().SingleAsync()).Subcategoria.Should().Be("Equipamento inicial - Artefato - Passivo - Anel");
        (await db.EquipmentKitChoiceSlots.SingleAsync(s => s.Id == slot.Id)).ArmorSlot.Should().Be(ArmorSlotType.Superior);
    }
}
