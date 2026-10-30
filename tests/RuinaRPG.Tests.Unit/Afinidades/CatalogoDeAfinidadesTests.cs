using RuinaRPG.Domain.Afinidades;

namespace RuinaRPG.Tests.Unit.Afinidades;

public class CatalogoDeAfinidadesTests
{
    private static readonly CatalogoDeAfinidades Catalogo = new(
        [
            new(2, "Fogo", TipoDeElemento.Base, null), new(9, "Alma", TipoDeElemento.Caminho, null),
            new(4, "Gelo", TipoDeElemento.SubElemento, 3), new(19, "Luz", TipoDeElemento.SubElemento, null),
        ],
        [], new Dictionary<int, int>(), []);

    [Fact]
    public void Afinidades_sao_os_elementos_que_nao_sao_Caminho_na_ordem_do_catalogo() =>
        Assert.Equal([2, 4, 19], Catalogo.Afinidades.Select(e => e.Id));

    [Fact]
    public void NomeDe_le_o_nome_do_catalogo() => Assert.Equal("Luz", Catalogo.NomeDe(19));

    [Fact]
    public void NomeDe_um_id_fora_do_catalogo_e_o_proprio_id() => Assert.Equal("77", Catalogo.NomeDe(77));

    [Theory]
    [InlineData("2", true)]
    [InlineData("19", true)]
    [InlineData("9", false)]   // Caminho
    [InlineData("77", false)]  // fora do catálogo
    [InlineData("Fogo", false)] // nome antigo que a conversão não casou
    public void EhAfinidade_aceita_so_o_id_em_texto_de_um_elemento_que_nao_e_Caminho(string valor, bool esperado) =>
        Assert.Equal(esperado, Catalogo.EhAfinidade(valor));

    [Theory]
    [InlineData("4", "Gelo")]
    [InlineData("agua", "agua")]
    public void NomeDoAlvo_le_o_nome_pelo_id_em_texto_e_mantem_um_valor_que_nao_casa(string valor, string esperado) =>
        Assert.Equal(esperado, Catalogo.NomeDoAlvo(valor));

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(2, null, null)]
    [InlineData(19, 2, null)]                                   // elemento criado pelo Auditor
    [InlineData(9, 9, null)]                                    // Caminho já salvo, reenviado igual
    [InlineData(9, 2, "Alma é um Caminho, não uma Afinidade.")]
    [InlineData(77, null, "Afinidade inválida.")]
    [InlineData(77, 77, null)]                                  // valor salvo reenviado igual nunca é recusado
    public void MotivoParaRecusarAfinidade_so_recusa_uma_escolha_nova_fora_do_catalogo_ou_Caminho(int? nova, int? atual, string? esperado) =>
        Assert.Equal(esperado, Catalogo.MotivoParaRecusarAfinidade(nova, atual));
}
