using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Campaigns;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Campanha R0006/R0009: a lista de anexos da campanha — grupos por tipo em painéis recolhíveis,
/// linhas em colunas fixas, imagens em galeria e filtros próprios de cada grupo.
/// </summary>
public class AnexosAgrupadosTests : MudBunitContext
{
    private static AnexoView Anexo(string tipo, string? nome, AttachmentFacets? facets = null, bool? publico = null, string? imageUrl = null) =>
        new($"{tipo}-{nome}", tipo, nome, imageUrl, facets, publico);

    private static readonly AnexoView[] Variados =
    [
        Anexo("Item", "Espada", new AttachmentFacets(ItemTipo: "Arma", Subcategoria: "Espadas"), publico: true),
        Anexo("Item", "Corda", new AttachmentFacets(ItemTipo: "ItemGeral", Subcategoria: "Equipamentos de Aventura"), publico: false),
        Anexo("SpellAbilityBankEntry", "Bola de Fogo", new AttachmentFacets(EntradaTipo: "Magia", Grau: 2), publico: false),
        Anexo("RuneBankEntry", "Runa do Fogo", new AttachmentFacets(Grau: 1, Disciplina: "Adicao"), publico: false),
        Anexo("Image", "mapa.png", publico: true, imageUrl: "/images/mapa.png"),
        Anexo("NpcSheet", "Ferreiro", publico: false),
    ];

    private IRenderedComponent<AnexosAgrupados> RenderLista(IReadOnlyList<AnexoView> anexos, bool filtrarPorVisibilidade = true) =>
        Render<AnexosAgrupados>(p => p
            .Add(x => x.Anexos, anexos)
            .Add(x => x.FiltrarPorVisibilidade, filtrarPorVisibilidade)
            .Add(x => x.Visibilidade, a => $"<span class=\"teste-visibilidade\">vis:{a.Id}</span>")
            .Add(x => x.Acoes, a => $"<span class=\"teste-acao\">acao:{a.Id}</span>"));

    private static IRenderedComponent<MudExpansionPanel> Grupo(IRenderedComponent<AnexosAgrupados> cut, string tituloComeca) =>
        cut.FindComponents<MudExpansionPanel>().Single(p => p.Instance.Text!.StartsWith(tituloComeca));

    private static List<string> Rotulos<T>(IRenderedComponent<MudExpansionPanel> grupo) =>
        grupo.FindComponents<MudSelect<T>>().Select(s => s.Instance.Label!).ToList();

    [Fact]
    public void Without_attachments_it_says_so_instead_of_rendering_an_empty_list()
    {
        var cut = RenderLista([]);

        cut.Markup.Should().Contain("Nenhum anexo");
        cut.FindComponents<MudExpansionPanel>().Should().BeEmpty();
    }

    [Fact]
    public void Each_group_is_a_panel_titled_with_its_name_and_count()
    {
        var cut = RenderLista(Variados);

        cut.FindComponents<MudExpansionPanel>().Select(p => p.Instance.Text).Should()
            .Equal("Itens (2)", "Magias/Habilidades (1)", "Runas (1)", "Imagens (1)", "NPCs (1)");
    }

    [Fact]
    public void A_row_shows_the_name_the_visibility_controls_and_the_actions_each_in_its_own_column()
    {
        var cut = RenderLista(Variados);

        var linha = Grupo(cut, "Itens").FindAll(".anexo-linha").First();
        linha.QuerySelector(".anexo-nome")!.TextContent.Should().Contain("Corda");
        linha.QuerySelector(".anexo-visibilidade .teste-visibilidade")!.TextContent.Should().Be("vis:Item-Corda");
        linha.QuerySelector(".anexo-acoes .teste-acao")!.TextContent.Should().Be("acao:Item-Corda");
    }

    [Fact]
    public void A_row_without_an_image_still_has_the_thumbnail_column_so_names_line_up()
    {
        var cut = RenderLista(Variados);

        Grupo(cut, "Itens").FindAll(".anexo-linha").Should().OnlyContain(l => l.QuerySelector(".anexo-miniatura") != null);
    }

    [Fact]
    public void A_row_shows_the_details_of_its_tipo_under_the_name()
    {
        var cut = RenderLista(Variados);

        Grupo(cut, "Itens").Find(".anexo-linha .anexo-detalhe").TextContent.Should().Be("Item Geral · Equipamentos de Aventura");
        Grupo(cut, "Magias").Find(".anexo-linha .anexo-detalhe").TextContent.Should().Be("Magia · Grau 2");
        Grupo(cut, "Runas").Find(".anexo-linha .anexo-detalhe").TextContent.Should().Be("Grau 1 · Adição");
    }

    [Fact]
    public void An_attachment_without_a_public_name_is_labelled_as_such()
    {
        var cut = RenderLista([Anexo("NpcSheet", null)], filtrarPorVisibilidade: false);

        Grupo(cut, "NPCs").Find(".anexo-nome").TextContent.Should().Contain("(sem nome público)");
    }

    [Fact]
    public void Images_are_a_gallery_of_clickable_thumbnails_not_rows()
    {
        var cut = RenderLista(Variados);

        var imagens = Grupo(cut, "Imagens");
        imagens.FindAll(".anexo-linha").Should().BeEmpty();
        imagens.FindAll(".anexos-galeria .anexo-cartao").Should().ContainSingle();
        imagens.FindComponents<ClickableImage>().Should().ContainSingle(i => i.Instance.Src == "/images/mapa.png");
        imagens.Find(".anexo-cartao .teste-visibilidade").TextContent.Should().Be("vis:Image-mapa.png");
        imagens.Find(".anexo-cartao .teste-acao").TextContent.Should().Be("acao:Image-mapa.png");
    }

    [Fact]
    public void Every_group_but_images_has_a_name_search()
    {
        var cut = RenderLista(Variados);

        foreach (var titulo in new[] { "Itens", "Magias", "Runas", "NPCs" })
            Grupo(cut, titulo).FindComponents<MudTextField<string>>().Should().ContainSingle(t => t.Instance.Label == "Buscar por nome");
        Grupo(cut, "Imagens").FindComponents<MudTextField<string>>().Should().BeEmpty();
    }

    [Fact]
    public void Each_group_offers_the_filters_of_its_own_tipo()
    {
        var cut = RenderLista(Variados, filtrarPorVisibilidade: false);

        Rotulos<string>(Grupo(cut, "Itens")).Should().Equal("Tipo de item", "Subcategoria");
        Rotulos<string>(Grupo(cut, "Magias")).Should().Equal("Tipo");
        Rotulos<int?>(Grupo(cut, "Magias")).Should().Equal("Grau");
        Rotulos<int?>(Grupo(cut, "Runas")).Should().Equal("Grau");
        Rotulos<string>(Grupo(cut, "Runas")).Should().Equal("Disciplina");
        Rotulos<string>(Grupo(cut, "NPCs")).Should().BeEmpty();
        Rotulos<string>(Grupo(cut, "Imagens")).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_visibility_filter_exists_in_every_group_only_when_the_screen_distinguishes_public_from_private(bool filtrarPorVisibilidade)
    {
        var cut = RenderLista(Variados, filtrarPorVisibilidade);

        foreach (var grupo in cut.FindComponents<MudExpansionPanel>())
            grupo.FindComponents<MudSelect<bool?>>().Any(s => s.Instance.Label == "Visibilidade").Should().Be(filtrarPorVisibilidade);
    }

    [Fact]
    public async Task Filtering_a_group_narrows_only_that_group_and_the_title_says_how_many_of_the_total_are_shown()
    {
        var cut = RenderLista(Variados);

        var busca = Grupo(cut, "Itens").FindComponents<MudTextField<string>>().Single();
        await cut.InvokeAsync(() => busca.Instance.ValueChanged.InvokeAsync("esp"));

        Grupo(cut, "Itens (1 de 2)").FindAll(".anexo-nome").Select(n => n.TextContent.Trim()).Should().ContainSingle().Which.Should().StartWith("Espada");
        Grupo(cut, "Magias/Habilidades (1)").FindAll(".anexo-linha").Should().ContainSingle();
    }

    [Fact]
    public async Task A_tipo_specific_filter_narrows_its_group()
    {
        var cut = RenderLista(Variados);

        var tipoDeItem = Grupo(cut, "Itens").FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == "Tipo de item");
        await cut.InvokeAsync(() => tipoDeItem.Instance.ValueChanged.InvokeAsync("Arma"));

        Grupo(cut, "Itens (1 de 2)").FindAll(".anexo-linha").Should().ContainSingle();
    }

    [Fact]
    public async Task A_filter_that_matches_nothing_keeps_the_group_and_its_filters_visible()
    {
        var cut = RenderLista(Variados);

        var busca = Grupo(cut, "Itens").FindComponents<MudTextField<string>>().Single();
        await cut.InvokeAsync(() => busca.Instance.ValueChanged.InvokeAsync("zzz"));

        var grupo = Grupo(cut, "Itens (0 de 2)");
        grupo.FindAll(".anexo-linha").Should().BeEmpty();
        grupo.Markup.Should().Contain("Nenhum anexo com esses filtros");
        grupo.FindComponents<MudTextField<string>>().Should().ContainSingle();
    }
}
