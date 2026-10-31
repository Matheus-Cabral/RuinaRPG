using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.AuditoriaDeAfinidades;
using RuinaRPG.Contracts.Rules;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.AuditoriaDeAfinidades;

public class VisibilidadeSecaoTests : SecoesDeAfinidadesTestes
{
    private static Dictionary<string, bool> Marcados(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> cut) =>
        cut.FindComponents<MudCheckBox<bool>>().ToDictionary(c => c.Instance.Label!, c => c.Find("input").HasAttribute("checked"));

    [Fact]
    public async Task As_opcoes_sao_as_Vocacoes_magicas_e_as_Classes_delas()
    {
        var cut = await RenderizarAsync<VisibilidadeSecao>();

        Opcoes<string>(cut, "Vocação ou Classe").Should().Equal(
            "Vocação: Feiticeiro", "Vocação: Adepto", "Vocação: Bruxo",
            "Classe: Aeromante", "Classe: Biomante", "Classe: Fluxomante", "Classe: Piromante", "Classe: Sábio",
            "Classe: Paladino", "Classe: Arauto", "Classe: Trovador", "Classe: Eremita",
            "Classe: Ecomante", "Classe: Hemomante", "Classe: Osteomante", "Classe: Nexomante", "Classe: Cultista");
    }

    [Fact]
    public async Task Mostra_so_Caminhos_e_Sub_Elementos_marcados_pelas_ligacoes_da_opcao_escolhida()
    {
        var cut = await RenderizarAsync<VisibilidadeSecao>();

        // Começa no Feiticeiro: só Alma ligada. Os Elementos base (Água, Ar) não aparecem.
        Marcados(cut).Should().Equal(new Dictionary<string, bool> { ["Gelo"] = false, ["Raio"] = false, ["Alma"] = true });

        await DefinirAsync(cut, cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == "Vocação ou Classe").Instance.ValueChanged, "Classe:Aeromante");

        Marcados(cut).Should().Equal(new Dictionary<string, bool> { ["Gelo"] = false, ["Raio"] = true, ["Alma"] = false });
    }

    [Fact]
    public async Task Marcar_e_desmarcar_grava_a_ligacao()
    {
        var cut = await RenderizarAsync<VisibilidadeSecao>();

        await DefinirAsync(cut, cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Gelo").Instance.ValueChanged, true);
        await DefinirAsync(cut, cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Alma").Instance.ValueChanged, false);

        Api.Escritas.Select(e => (e.Metodo, e.Caminho)).Should().Equal(("PUT", "afinidades/visibilidade"), ("PUT", "afinidades/visibilidade"));
        Api.Escritas[0].Ler<AlterarVisibilidadeDeElementoRequest>().Should().Be(new AlterarVisibilidadeDeElementoRequest("Vocacao", "Feiticeiro", 4, true));
        Api.Escritas[1].Ler<AlterarVisibilidadeDeElementoRequest>().Should().Be(new AlterarVisibilidadeDeElementoRequest("Vocacao", "Feiticeiro", 9, false));
        Marcados(cut).Should().Equal(new Dictionary<string, bool> { ["Gelo"] = true, ["Raio"] = false, ["Alma"] = false });
    }
}
