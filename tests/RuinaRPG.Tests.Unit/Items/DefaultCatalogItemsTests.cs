using FluentAssertions;
using RuinaRPG.Infrastructure.Items;

namespace RuinaRPG.Tests.Unit.Items;

public class DefaultCatalogItemsTests
{
    [Fact]
    public void Build_includes_the_two_simple_Tonico_items_used_by_Equipagem_kits()
    {
        var items = DefaultCatalogItems.Build(Guid.NewGuid());

        items.OfType<ItemGeral>().Should().Contain(i => i.Nome == "Tônico de Vida simples" && i.Subcategoria == "Poções e Tônicos");
        items.OfType<ItemGeral>().Should().Contain(i => i.Nome == "Tônico de Foco simples" && i.Subcategoria == "Poções e Tônicos");
    }
}
