using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A aba Magias &amp; Habilidades compartilhada (E, Task 7) dentro das três páginas: uma por página, com o estado da
/// ficha; as Runas só onde o tipo tem Runas; a Habilidade Racial só onde há Linhagem. Review Focus (3): o campo de
/// Runa sobe a imagem com a campanha da ficha para o jogador e sem campanha para o GM, como antes.
/// </summary>
public class AbaMagiasEHabilidadesTests : RedeDeSegurancaBase
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string, string> TiposComRunasEPapeis => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" }, { "NPC", "GM" }, { "NPC", "Jogador" },
    };

    private async Task<IRenderedComponent<IComponent>> AbrirAsync(string tipo, string papel)
    {
        var cut = await RenderFichaAsync(tipo, papel);
        await IrParaAbaAsync(cut, "Magias & Habilidades");
        await EsperarCalmariaAsync();
        return cut;
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_tab_is_the_shared_component_with_the_page_state(string tipo)
    {
        var cut = await AbrirAsync(tipo, "GM");

        var aba = cut.FindComponent<AbaMagiasEHabilidades>();
        aba.Instance.Estado.Should().BeSameAs(cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value);
        cut.FindComponents<AbaMagiasEHabilidades>().Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Runas_only_where_the_kind_has_runes(string tipo)
    {
        var cut = await AbrirAsync(tipo, "GM");

        cut.FindComponents<RunasSection>().Should().HaveCount(tipo == "Criatura" ? 0 : 1);
        cut.FindComponents<RuneOrigemFields>().Should().HaveCount(tipo == "Criatura" ? 0 : 1);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Habilidade_racial_only_where_the_kind_has_a_lineage(string tipo)
    {
        var cut = await AbrirAsync(tipo, "GM");

        cut.FindComponents<HabilidadeRacialSection>().Should().HaveCount(tipo == "Criatura" ? 0 : 1);
    }

    // Review Focus (3): o jogador busca o Banco de Runas e a galeria da campanha, e a imagem nova sobe com ela.
    [Theory]
    [MemberData(nameof(TiposComRunasEPapeis))]
    public async Task The_rune_fields_get_the_campaign_only_for_a_player(string tipo, string papel)
    {
        var cut = await AbrirAsync(tipo, papel);

        var campos = cut.FindComponent<RuneOrigemFields>().Instance;
        campos.CampaignId.Should().Be(papel == "GM" ? null : "c1");
        campos.AvailableImages.Select(i => i.Id).Should().Equal("img1", "img2");
        campos.BankEntries.Select(e => e.Id).Should().Equal("rb1");
    }

    // RunasSection não tem parâmetros: só re-renderiza por um evento dela ou pela cascata da página. As ações das
    // Runas são dela (o formulário e os botões estão nela), então a lista relida aparece.
    [Theory]
    [InlineData("Personagem")]
    [InlineData("NPC")]
    public async Task Removing_a_runa_shows_the_reloaded_list(string tipo)
    {
        var cut = await AbrirAsync(tipo, "GM");
        AngleSharp.Dom.IElement Tabela() => Botao(AbaAberta(cut), "Adicionar Runa").Closest("form")!.ParentElement!.QuerySelector("table")!;
        Tabela().QuerySelectorAll("tbody tr").Should().HaveCount(1);
        LimparRequisicoes();
        Responder("/runes", Array.Empty<object>());

        await cut.InvokeAsync(() => Botao(Tabela(), "Remover").Click());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Contain(r => r.StartsWith("GET ") && r.EndsWith("/runes")));
        cut.WaitForAssertion(() => Tabela().QuerySelectorAll("tbody tr").Should().BeEmpty());
    }
}
