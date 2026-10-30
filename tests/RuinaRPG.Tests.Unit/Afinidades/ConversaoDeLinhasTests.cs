using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.Afinidades;

public class ConversaoDeLinhasTests
{
    [Fact]
    public void Ficha_real_do_Review_Focus_1()
    {
        var linhas = new[]
        {
            new LinhaDeAfinidade(Elemento.Ar, 2, SubElemento.Gelo, 3, EssenciaBasica.Agua, 1, 5),
            new LinhaDeAfinidade(Elemento.Ar, 1, SubElemento.Raio, 1, EssenciaBasica.Fogo, 2),
        };

        var r = ConversaoDeLinhas.Converter(linhas);

        Assert.Equal(5, r.Count);
        Assert.Equal((2, (int?)null), r[3]); // Ar
        Assert.Equal((1, (int?)null), r[1]); // Água
        Assert.Equal((2, (int?)null), r[2]); // Fogo
        Assert.Equal((3, (int?)5), r[4]);    // Gelo
        Assert.Equal((1, (int?)null), r[7]); // Raio
        Assert.Equal(9, r.Values.Sum(v => v.Pontos));
        var antes = VocacaoArcanaCalculator.Gasto(linhas);
        Assert.Equal(antes, r.Values.Sum(v => v.Pontos));
    }

    [Fact]
    public void Experiencia_sem_sub_elemento_vai_para_a_essencia_1()
    {
        var r = ConversaoDeLinhas.Converter([new LinhaDeAfinidade(Elemento.Fogo, 1, null, null, null, null, 4)]);
        Assert.Equal((1, (int?)4), r[2]);
    }

    [Fact]
    public void Experiencia_duplicada_fica_a_maior()
    {
        var r = ConversaoDeLinhas.Converter(
        [
            new LinhaDeAfinidade(Elemento.Ar, 1, SubElemento.Gelo, 1, null, null, 3),
            new LinhaDeAfinidade(Elemento.Agua, 1, SubElemento.Gelo, 1, null, null, 7),
            new LinhaDeAfinidade(Elemento.Terra, 1, SubElemento.Flora, 1, null, null, 6),
            new LinhaDeAfinidade(Elemento.Terra, 1, SubElemento.Flora, 1, null, null, 2),
        ]);
        Assert.Equal(7, r[4].Experiencia);
        Assert.Equal(6, r[5].Experiencia);
    }

    [Fact]
    public void Linha_vazia_e_ignorada()
    {
        Assert.Empty(ConversaoDeLinhas.Converter([new LinhaDeAfinidade(null, null, null, null)]));
        Assert.Empty(ConversaoDeLinhas.Converter([]));
    }

    [Fact]
    public void Caminhos_como_segunda_essencia_usam_os_ids_do_catalogo()
    {
        var r = ConversaoDeLinhas.Converter(
        [
            new LinhaDeAfinidade(Elemento.Ar, 1, SubElemento.Prever, 1, EssenciaBasica.Alma, 2),
            new LinhaDeAfinidade(Elemento.Fogo, 1, SubElemento.Curar, 1, EssenciaBasica.Vida, 3),
            new LinhaDeAfinidade(Elemento.Terra, 1, SubElemento.Invocacao, 1, EssenciaBasica.Mundano, 4),
        ]);
        Assert.Equal(2, r[9].Pontos);
        Assert.Equal(3, r[14].Pontos);
        Assert.Equal(4, r[18].Pontos);
        Assert.Equal(1, r[8].Pontos);
        Assert.Equal(1, r[13].Pontos);
        Assert.Equal(1, r[17].Pontos);
    }

    [Fact]
    public void Sub_elementos_legados_Alma_e_Vida_mapeiam_para_o_catalogo()
    {
        var r = ConversaoDeLinhas.Converter(
        [
            new LinhaDeAfinidade(null, null, SubElemento.Alma, 2),
            new LinhaDeAfinidade(null, null, SubElemento.Vida, 3),
        ]);
        Assert.Equal(2, r[9].Pontos);
        Assert.Equal(3, r[14].Pontos);
    }

    [Fact]
    public void Valor_do_elemento_e_o_maior_entre_linhas()
    {
        var r = ConversaoDeLinhas.Converter(
        [
            new LinhaDeAfinidade(Elemento.Ar, 1, null, null, EssenciaBasica.Agua, 3),
            new LinhaDeAfinidade(Elemento.Agua, 2, null, null, EssenciaBasica.Ar, 2),
        ]);
        Assert.Equal(2, r[3].Pontos);
        Assert.Equal(3, r[1].Pontos);
    }
}
