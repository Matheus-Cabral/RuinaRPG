using FluentAssertions;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class EquipmentKitSeedDataTests
{
    // Regression guard named in the Equipagem design spec's own test-strategy section: every kit's
    // fixed-item Nome (EquipmentKitSeedData.All) must resolve against a fresh GM's default catalog
    // (DefaultCatalogItems.Build) without needing EquipmentKitGrantService's lazy auto-creation
    // fallback. A future rename in DefaultCatalogItems.cs that silently breaks a kit's Nome
    // reference (or an alias typo in EquipmentKitSeedData.cs) fails this test instead of only
    // showing up as a confusing "item não cadastrado" 400 the first time some GM applies that kit.
    //
    // No fixed item is exempted today: the 2 Tônico entries (previously the one documented lazy-
    // auto-create case) are now seeded into DefaultCatalogItems.cs too (see the "adiciona Tônico de
    // Vida/Foco simples ao catálogo padrão" commit), and Patrulheiro's "Tampa de Madeira" Escudo
    // alias already exists in the default catalog. If a future kit legitimately needs a fixed
    // Arma/Escudo/Artefato that isn't (and shouldn't be) in the default catalog, add its exact
    // Nome to s_knownMissingFromDefaultCatalog below with a comment explaining why, rather than
    // weakening this assertion.
    private static readonly HashSet<string> s_knownMissingFromDefaultCatalog = [];

    private static readonly Guid s_fakeGmId = Guid.NewGuid();

    [Fact]
    public void Every_kit_s_fixed_item_Nome_resolves_against_the_default_catalog()
    {
        var defaultCatalog = DefaultCatalogItems.Build(s_fakeGmId);

        foreach (var kit in EquipmentKitSeedData.All)
        {
            foreach (var item in kit.Items)
            {
                if (s_knownMissingFromDefaultCatalog.Contains(item.Nome))
                    continue;

                var found = item.Tipo switch
                {
                    ItemTipo.ItemGeral => defaultCatalog.OfType<ItemGeral>().Any(i => i.Nome == item.Nome),
                    ItemTipo.Arma => defaultCatalog.OfType<Arma>().Any(i => i.Nome == item.Nome),
                    ItemTipo.Escudo => defaultCatalog.OfType<Escudo>().Any(i => i.Nome == item.Nome),
                    ItemTipo.Artefato => defaultCatalog.OfType<Artefato>().Any(i => i.Nome == item.Nome),
                    _ => false,
                };

                found.Should().BeTrue(
                    $"kit \"{kit.Nome}\"'s fixed item \"{item.Nome}\" (Tipo={item.Tipo}) should resolve " +
                    "against DefaultCatalogItems.Build without needing lazy auto-creation");
            }
        }
    }

    // Task 2 ("adiciona Tônico de Vida/Foco simples ao catálogo padrão") seeded both Tônicos under
    // Subcategoria "Poções e Tônicos" in DefaultCatalogItems.cs. Every kit's own seed row for these
    // two items must carry the matching SubcategoriaHint, so an *existing* GM (who doesn't get
    // DefaultCatalogItems.Build re-run) lazily auto-creates the item under the same Subcategoria a
    // *new* GM already has it under — see EquipmentKitGrantService.ResolveOrCreateFixedItemAsync's
    // "Equipamentos de Aventura" fallback, which fires whenever SubcategoriaHint is null.
    [Fact]
    public void Every_Tonico_seed_row_carries_the_Pocoes_e_Tonicos_SubcategoriaHint()
    {
        var tonicoNomes = new[] { "Tônico de Vida simples", "Tônico de Foco simples" };

        foreach (var kit in EquipmentKitSeedData.All)
        {
            foreach (var item in kit.Items.Where(i => tonicoNomes.Contains(i.Nome)))
            {
                item.SubcategoriaHint.Should().Be("Poções e Tônicos",
                    $"kit \"{kit.Nome}\"'s \"{item.Nome}\" row should hint the same Subcategoria DefaultCatalogItems.cs already seeds it under");
            }
        }
    }
}
