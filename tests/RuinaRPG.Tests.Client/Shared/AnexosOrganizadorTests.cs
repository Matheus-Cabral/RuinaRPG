using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Campaigns;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Campanha R0006/R0009: os anexos da campanha aparecem agrupados por tipo, em ordem alfabética, e
/// cada grupo é filtrável pelos critérios do seu tipo.
/// </summary>
public class AnexosOrganizadorTests
{
    private static AnexoView Anexo(string tipo, string? nome, AttachmentFacets? facets = null, bool? publico = null, string? id = null) =>
        new(id ?? $"{tipo}-{nome}", tipo, nome, null, facets, publico);

    [Fact]
    public void Groups_come_in_a_fixed_order_regardless_of_the_order_of_the_attachments()
    {
        var grupos = AnexosOrganizador.Agrupar(new[]
        {
            Anexo("CreatureSheet", "Lobo"), Anexo("NpcSheet", "Ferreiro"), Anexo("Image", null),
            Anexo("RuneBankEntry", "Runa"), Anexo("SpellAbilityBankEntry", "Bola de Fogo"), Anexo("Item", "Corda"),
        });

        grupos.Select(g => g.Titulo).Should().Equal("Itens", "Magias/Habilidades", "Runas", "Imagens", "NPCs", "Criaturas");
    }

    [Fact]
    public void A_tipo_without_attachments_has_no_group()
    {
        var grupos = AnexosOrganizador.Agrupar(new[] { Anexo("Item", "Corda"), Anexo("NpcSheet", "Ferreiro") });

        grupos.Select(g => g.Titulo).Should().Equal("Itens", "NPCs");
    }

    [Fact]
    public void Inside_a_group_attachments_are_sorted_by_name_ignoring_case_and_accents()
    {
        var grupos = AnexosOrganizador.Agrupar(new[] { Anexo("Item", "corda"), Anexo("Item", "Água benta"), Anexo("Item", "Bússola") });

        grupos.Single().Anexos.Select(a => a.Nome).Should().Equal("Água benta", "Bússola", "corda");
    }

    [Fact]
    public void An_attachment_without_a_public_name_goes_to_the_end_of_its_group()
    {
        var grupos = AnexosOrganizador.Agrupar(new[] { Anexo("NpcSheet", null, id: "sem-nome"), Anexo("NpcSheet", "Ferreiro") });

        grupos.Single().Anexos.Select(a => a.Id).Should().Equal("NpcSheet-Ferreiro", "sem-nome");
    }

    [Fact]
    public void Images_keep_the_order_in_which_they_were_attached()
    {
        var grupos = AnexosOrganizador.Agrupar(new[] { Anexo("Image", "z.png", id: "1"), Anexo("Image", "a.png", id: "2") });

        grupos.Single().Anexos.Select(a => a.Id).Should().Equal("1", "2");
    }

    [Fact]
    public void An_unknown_tipo_gets_its_own_group_at_the_end_instead_of_disappearing()
    {
        var grupos = AnexosOrganizador.Agrupar(new[] { Anexo("Mapa", "Ilha"), Anexo("Item", "Corda") });

        grupos.Select(g => g.Titulo).Should().Equal("Itens", "Mapa");
    }

    [Fact]
    public void The_name_filter_matches_part_of_the_name_ignoring_case_and_accents()
    {
        var anexos = new[] { Anexo("Item", "Água benta"), Anexo("Item", "Corda") };

        AnexosOrganizador.Filtrar(anexos, new AnexoFiltro { Nome = "AGUA" }).Select(a => a.Nome).Should().Equal("Água benta");
    }

    [Theory]
    [InlineData(true, "Pública")]
    [InlineData(false, "Privada")]
    public void The_visibility_filter_keeps_only_public_or_only_private(bool publico, string esperado)
    {
        var anexos = new[] { Anexo("Item", "Pública", publico: true), Anexo("Item", "Privada", publico: false) };

        AnexosOrganizador.Filtrar(anexos, new AnexoFiltro { Publico = publico }).Select(a => a.Nome).Should().Equal(esperado);
    }

    [Fact]
    public void Item_filters_are_the_item_tipo_and_the_subcategoria()
    {
        var anexos = new[]
        {
            Anexo("Item", "Espada", new AttachmentFacets(ItemTipo: "Arma", Subcategoria: "Espadas")),
            Anexo("Item", "Machado", new AttachmentFacets(ItemTipo: "Arma", Subcategoria: "Machados")),
            Anexo("Item", "Corda", new AttachmentFacets(ItemTipo: "ItemGeral", Subcategoria: "Equipamentos de Aventura")),
        };

        AnexosOrganizador.Filtrar(anexos, new AnexoFiltro { ItemTipo = "Arma" }).Select(a => a.Nome).Should().Equal("Espada", "Machado");
        AnexosOrganizador.Filtrar(anexos, new AnexoFiltro { ItemTipo = "Arma", Subcategoria = "Machados" }).Select(a => a.Nome).Should().Equal("Machado");
    }

    [Fact]
    public void Bank_entry_filters_are_the_entry_tipo_and_the_grau()
    {
        var anexos = new[]
        {
            Anexo("SpellAbilityBankEntry", "Bola de Fogo", new AttachmentFacets(EntradaTipo: "Magia", Grau: 2)),
            Anexo("SpellAbilityBankEntry", "Golpe", new AttachmentFacets(EntradaTipo: "Habilidade", Grau: 2)),
            Anexo("SpellAbilityBankEntry", "Pele de Pedra", new AttachmentFacets(EntradaTipo: "Passiva")),
        };

        AnexosOrganizador.Filtrar(anexos, new AnexoFiltro { EntradaTipo = "Magia" }).Select(a => a.Nome).Should().Equal("Bola de Fogo");
        AnexosOrganizador.Filtrar(anexos, new AnexoFiltro { Grau = 2 }).Select(a => a.Nome).Should().Equal("Bola de Fogo", "Golpe");
    }

    [Fact]
    public void Rune_filters_are_the_grau_and_the_disciplina()
    {
        var anexos = new[]
        {
            Anexo("RuneBankEntry", "Fogo", new AttachmentFacets(Grau: 1, Disciplina: "Adicao")),
            Anexo("RuneBankEntry", "Gelo", new AttachmentFacets(Grau: 2, Disciplina: "Adicao")),
        };

        AnexosOrganizador.Filtrar(anexos, new AnexoFiltro { Grau = 2, Disciplina = "Adicao" }).Select(a => a.Nome).Should().Equal("Gelo");
    }

    [Fact]
    public void The_options_of_a_filter_are_the_distinct_values_present_in_the_group_sorted()
    {
        var anexos = new[]
        {
            Anexo("Item", "Machado", new AttachmentFacets(ItemTipo: "Arma", Subcategoria: "Machados")),
            Anexo("Item", "Espada", new AttachmentFacets(ItemTipo: "Arma", Subcategoria: "Espadas")),
            Anexo("Item", "Graveto", new AttachmentFacets(ItemTipo: "Arma")),
            Anexo("Item", "Sabre", new AttachmentFacets(ItemTipo: "Arma", Subcategoria: "Espadas")),
        };

        AnexosOrganizador.Opcoes(anexos, f => f.Subcategoria).Should().Equal("Espadas", "Machados");
        AnexosOrganizador.Opcoes(anexos, f => f.Grau).Should().BeEmpty();
    }

    [Fact]
    public void An_empty_filter_is_not_active_and_any_criterion_makes_it_active()
    {
        new AnexoFiltro().Ativo.Should().BeFalse();
        new AnexoFiltro { Nome = "  " }.Ativo.Should().BeFalse();
        new AnexoFiltro { Nome = "a" }.Ativo.Should().BeTrue();
        new AnexoFiltro { Publico = false }.Ativo.Should().BeTrue();
        new AnexoFiltro { Grau = 1 }.Ativo.Should().BeTrue();
    }
}
