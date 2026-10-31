using Bunit;
using System.Net;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.AuditoriaDeAfinidades;
using RuinaRPG.Contracts.Rules;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.AuditoriaDeAfinidades;

public class TotaisPorCirculoSecaoTests : SecoesDeAfinidadesTestes
{
    [Fact]
    public async Task Lista_o_total_de_cada_Circulo_em_ordem()
    {
        var cut = await RenderizarAsync<TotaisPorCirculoSecao>();

        cut.FindAll("tbody tr td:first-child").Select(td => td.TextContent.Trim()).Should().Equal("0", "1", "2");
        Coluna(cut, 2).Should().Equal("3", "6", "9");
    }

    [Fact]
    public async Task Editar_um_total_grava_o_Circulo_e_relê()
    {
        var cut = await RenderizarAsync<TotaisPorCirculoSecao>();

        await DefinirAsync(cut, cut.FindComponents<MudNumericField<int>>()[2].Instance.ValueChanged, 12);

        var put = Api.Escritas.Should().ContainSingle().Subject;
        (put.Metodo, put.Caminho).Should().Be(("PUT", "afinidades/totais-por-circulo/2"));
        put.Ler<SalvarTotalDeAfinidadePorCirculoRequest>().Should().Be(new SalvarTotalDeAfinidadePorCirculoRequest(12));
        Coluna(cut, 2)[2].Should().Be("12");
    }

    [Fact]
    public async Task Uma_recusa_mostra_a_mensagem_e_volta_o_valor()
    {
        var cut = await RenderizarAsync<TotaisPorCirculoSecao>();
        Api.Recusa = (HttpStatusCode.BadRequest, "O total deve ser um inteiro maior ou igual a zero.");

        await DefinirAsync(cut, cut.FindComponents<MudNumericField<int>>()[1].Instance.ValueChanged, -1);

        cut.Markup.Should().Contain("O total deve ser um inteiro maior ou igual a zero.");
        Coluna(cut, 2)[1].Should().Be("6");
    }

    [Fact]
    public async Task Um_403_mostra_o_aviso_de_sem_permissao()
    {
        var cut = await RenderizarAsync<TotaisPorCirculoSecao>();
        Api.Recusa = (HttpStatusCode.Forbidden, "");

        await DefinirAsync(cut, cut.FindComponents<MudNumericField<int>>()[0].Instance.ValueChanged, 4);

        cut.Markup.Should().Contain("sem permissão para editar");
    }
}
