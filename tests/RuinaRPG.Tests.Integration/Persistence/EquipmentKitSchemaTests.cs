using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class EquipmentKitSchemaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;

    public EquipmentKitSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task EquipmentKit_with_a_fixed_item_and_a_choice_slot_round_trips()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit de Teste", Descricao = "Descrição de teste", Ciclos = 5 };
        db.EquipmentKits.Add(kit);
        db.EquipmentKitItems.Add(new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = "Mochila", Tipo = ItemTipo.ItemGeral, Qtd = 1 });
        db.EquipmentKitChoiceSlots.Add(new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Tier = Tier.F, Qtd = 1 });
        await db.SaveChangesAsync();

        var reloadedItem = await db.EquipmentKitItems.SingleAsync(i => i.KitId == kit.Id);
        reloadedItem.Nome.Should().Be("Mochila");
        var reloadedSlot = await db.EquipmentKitChoiceSlots.SingleAsync(s => s.KitId == kit.Id);
        reloadedSlot.Tier.Should().Be(Tier.F);
    }
}
