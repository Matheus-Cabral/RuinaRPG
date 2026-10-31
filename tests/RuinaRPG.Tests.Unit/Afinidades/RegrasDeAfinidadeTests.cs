using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.Afinidades;

public class RegrasDeAfinidadeTests
{
    private const int Terra = 0, Agua = 1, Fogo = 2, Ar = 3, Gelo = 4, Raio = 7, Alma = 9, Prever = 8, Mundano = 18;

    private static CatalogoDeAfinidades Catalogo(int? tetoAr = null) => new(
        [
            new(Terra, "Terra", TipoDeElemento.Base, null), new(Agua, "Água", TipoDeElemento.Base, null),
            new(Fogo, "Fogo", TipoDeElemento.Base, null), new(Ar, "Ar", TipoDeElemento.Base, tetoAr),
            new(Gelo, "Gelo", TipoDeElemento.SubElemento, null), new(Raio, "Raio", TipoDeElemento.SubElemento, null),
            new(Prever, "Prever", TipoDeElemento.SubElemento, null), new(Alma, "Alma", TipoDeElemento.Caminho, null),
            new(Mundano, "Mundano", TipoDeElemento.Caminho, null),
        ],
        [new(Ar, 1, Agua, 1, Gelo), new(Ar, 1, Fogo, 1, Raio), new(Ar, 1, Alma, 1, Prever)],
        new Dictionary<int, int> { [1] = 3, [2] = 5 },
        [("Feiticeiro", null, Alma), (null, "Clérigo", Mundano), ("Feiticeiro", null, Gelo)]);

    private static Dictionary<int, int> P(params (int Id, int Valor)[] itens) => itens.ToDictionary(i => i.Id, i => i.Valor);

    [Fact]
    public void Gasto_e_a_soma_dos_pontos() =>
        Assert.Equal(6, RegrasDeAfinidade.Gasto(P((Ar, 2), (Agua, 1), (Gelo, 3))));

    [Theory]
    [InlineData(Vocacao.Feiticeiro, 2, 2, 7)]
    [InlineData(Vocacao.Bruxo, 1, 0, 3)]
    [InlineData(Vocacao.Campeao, 2, 1, 1)]
    [InlineData(Vocacao.Cacador, 2, 0, 0)]
    [InlineData(null, 2, 4, 4)]
    public void Maximo_conforme_a_vocacao(Vocacao? v, int circulo, int adicional, int esperado) =>
        Assert.Equal(esperado, RegrasDeAfinidade.Maximo(Catalogo(), v, circulo, adicional));

    [Fact]
    public void Circulo_sem_total_cadastrado_conta_zero() =>
        Assert.Equal(1, RegrasDeAfinidade.Maximo(Catalogo(), Vocacao.Adepto, 9, 1));

    [Theory]
    [InlineData(Vocacao.Campeao, 0, false, false)]
    [InlineData(Vocacao.Cacador, 1, false, true)]
    [InlineData(Vocacao.Campeao, 0, true, true)]
    [InlineData(Vocacao.Feiticeiro, 0, false, true)]
    public void Secao_visivel(Vocacao v, int adicional, bool coracao, bool esperado) =>
        Assert.Equal(esperado, RegrasDeAfinidade.SecaoVisivel(v, adicional, coracao));

    private static List<int> Ids(IEnumerable<ElementoNaFicha> e) => e.Select(x => x.Elemento.Id).ToList();

    [Fact]
    public void Visibilidade_por_vocacao()
    {
        var ids = Ids(RegrasDeAfinidade.Elementos(Catalogo(), Vocacao.Feiticeiro, "Outra", P()));
        Assert.Equal([Terra, Agua, Fogo, Ar, Gelo, Alma], ids);
    }

    [Fact]
    public void Visibilidade_por_classe()
    {
        var ids = Ids(RegrasDeAfinidade.Elementos(Catalogo(), Vocacao.Adepto, "Clérigo", P()));
        Assert.Equal([Terra, Agua, Fogo, Ar, Mundano], ids);
    }

    [Fact]
    public void Visibilidade_por_vocacao_ou_classe_juntas()
    {
        var ids = Ids(RegrasDeAfinidade.Elementos(Catalogo(), Vocacao.Feiticeiro, "Clérigo", P()));
        Assert.Contains(Mundano, ids);
        Assert.Contains(Alma, ids);
    }

    [Fact]
    public void Marcial_ve_tudo() =>
        Assert.Equal(9, RegrasDeAfinidade.Elementos(Catalogo(), Vocacao.Campeao, null, P()).Count);

    [Fact]
    public void Sem_vocacao_so_ve_os_base() =>
        Assert.Equal(4, RegrasDeAfinidade.Elementos(Catalogo(), null, null, P()).Count);

    [Fact]
    public void Sub_elemento_com_requisito_nao_cumprido_vem_bloqueado_com_dica()
    {
        var gelo = RegrasDeAfinidade.Elementos(Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 1))).Single(e => e.Elemento.Id == Gelo);
        Assert.True(gelo.Bloqueado);
        Assert.Equal("Precisa de 1 em Ar e 1 em Água", gelo.Motivo);
    }

    [Fact]
    public void Sub_elemento_com_requisito_cumprido_nao_e_bloqueado()
    {
        var gelo = RegrasDeAfinidade.Elementos(Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 1), (Agua, 1))).Single(e => e.Elemento.Id == Gelo);
        Assert.False(gelo.Bloqueado);
        Assert.Null(gelo.Motivo);
    }

    [Fact]
    public void Elemento_fora_da_visibilidade_mas_com_pontos_continua_aparecendo_bloqueado()
    {
        var mundano = RegrasDeAfinidade.Elementos(Catalogo(), Vocacao.Feiticeiro, null, P((Mundano, 2))).Single(e => e.Elemento.Id == Mundano);
        Assert.True(mundano.Bloqueado);
        Assert.Equal(2, mundano.Pontos);
    }

    [Fact]
    public void Ponto_acima_do_teto_vem_marcado()
    {
        var ar = RegrasDeAfinidade.Elementos(Catalogo(tetoAr: 2), Vocacao.Feiticeiro, null, P((Ar, 3))).Single(e => e.Elemento.Id == Ar);
        Assert.True(ar.Bloqueado);
    }

    [Fact]
    public void Baixar_Ar_com_Gelo_recusa_com_o_texto()
    {
        var motivo = RegrasDeAfinidade.MotivoParaRecusarMudanca(
            Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 1), (Agua, 1), (Gelo, 2)), Ar, 0, 10);
        Assert.Equal("Gelo depende de Ar: zere Gelo antes.", motivo);
    }

    [Fact]
    public void Zerado_o_Gelo_Ar_pode_baixar() =>
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(
            Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 1), (Agua, 1)), Ar, 0, 10));

    [Fact]
    public void Subir_sub_elemento_bloqueado_e_recusado() =>
        Assert.Equal("Precisa de 1 em Ar e 1 em Água", RegrasDeAfinidade.MotivoParaRecusarMudanca(
            Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 1)), Gelo, 1, 10));

    [Fact]
    public void Subir_elemento_invisivel_e_recusado() =>
        Assert.NotNull(RegrasDeAfinidade.MotivoParaRecusarMudanca(
            Catalogo(), Vocacao.Feiticeiro, null, P(), Mundano, 1, 10));

    [Fact]
    public void Subir_acima_do_teto_e_recusado()
    {
        Assert.NotNull(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(tetoAr: 2), Vocacao.Feiticeiro, null, P((Ar, 2)), Ar, 3, 10));
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(tetoAr: 2), Vocacao.Feiticeiro, null, P((Ar, 1)), Ar, 2, 10));
    }

    [Fact]
    public void Subir_acima_do_maximo_e_recusado()
    {
        Assert.NotNull(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 2), (Fogo, 1)), Ar, 3, 3));
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 2), (Fogo, 1)), Fogo, 1, 3));
    }

    [Fact]
    public void Sem_teto_nao_ha_limite_por_elemento() =>
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(), Vocacao.Feiticeiro, null, P(), Terra, 50, 50));

    [Fact]
    public void Ficha_inconsistente_aceita_descer_mas_nao_subir()
    {
        // Gelo 3 com Ar 0 (requisito antigo menor ou Ar tirado): só tirar pontos.
        var pontos = P((Gelo, 3), (Ar, 0), (Agua, 0));
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(), Vocacao.Feiticeiro, null, pontos, Gelo, 2, 10));
        Assert.NotNull(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(), Vocacao.Feiticeiro, null, pontos, Gelo, 4, 10));
    }

    [Fact]
    public void Descer_com_o_total_acima_do_maximo_e_aceito() =>
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 5), (Fogo, 5)), Ar, 4, 3));

    private static CatalogoDeAfinidades CatalogoComDoisCruzamentos() => Catalogo() with
    {
        Cruzamentos = [new(Ar, 1, Agua, 1, Gelo), new(Fogo, 1, Terra, 1, Gelo)]
    };

    [Fact]
    public void Baixar_Ar_e_permitido_se_outro_cruzamento_do_sub_elemento_segue_cumprido() =>
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(
            CatalogoComDoisCruzamentos(), Vocacao.Feiticeiro, null,
            P((Ar, 2), (Agua, 1), (Fogo, 1), (Terra, 1), (Gelo, 2)), Ar, 0, 10));

    [Fact]
    public void Baixar_e_recusado_se_nenhum_cruzamento_sobra()
    {
        var motivo = RegrasDeAfinidade.MotivoParaRecusarMudanca(
            CatalogoComDoisCruzamentos(), Vocacao.Feiticeiro, null,
            P((Ar, 2), (Agua, 1), (Gelo, 2)), Ar, 0, 10);
        Assert.Equal("Gelo depende de Ar: zere Gelo antes.", motivo);
    }

    [Fact]
    public void Sub_elemento_ja_quebrado_nao_impede_baixar_o_elemento() =>
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(
            Catalogo(), Vocacao.Feiticeiro, null, P((Ar, 2), (Agua, 0), (Gelo, 2)), Ar, 0, 10));

    [Theory]
    [InlineData(99, 1, null, "Elemento inexistente.")]
    [InlineData(Fogo, -1, null, "O valor não pode ser negativo.")]
    [InlineData(Fogo, 1, -1, "A Experiência não pode ser negativa.")]
    public void Valor_invalido_e_recusado_para_qualquer_um(int elemento, int pontos, int? experiencia, string motivo) =>
        Assert.Equal(motivo, RegrasDeAfinidade.MotivoDeValorInvalido(Catalogo(), elemento, pontos, experiencia));

    [Fact]
    public void Valor_valido_mesmo_fora_das_regras_da_ficha_nao_e_invalido() =>
        Assert.Null(RegrasDeAfinidade.MotivoDeValorInvalido(Catalogo(tetoAr: 1), Ar, 5, 0));

    // Uma linha guardada (pontos ou só Experiência) sempre aparece: nenhuma linha fica escondida da lista.
    [Fact]
    public void Elemento_escondido_com_linha_sem_pontos_aparece_bloqueado()
    {
        var mundano = RegrasDeAfinidade.Elementos(Catalogo(), Vocacao.Feiticeiro, null, P((Mundano, 0))).Single(e => e.Elemento.Id == Mundano);
        Assert.True(mundano.Bloqueado);
        Assert.Equal(0, mundano.Pontos);
    }

    [Fact]
    public void Escrever_num_elemento_escondido_sem_linha_e_recusado_mesmo_sem_mudar_os_pontos() =>
        Assert.Equal("Mundano não está disponível para esta Vocação/Classe.",
            RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(), Vocacao.Feiticeiro, null, P(), Mundano, 0, 10));

    [Fact]
    public void Elemento_escondido_que_ja_tem_linha_aceita_manter_os_pontos() =>
        Assert.Null(RegrasDeAfinidade.MotivoParaRecusarMudanca(Catalogo(), Vocacao.Feiticeiro, null, P((Mundano, 0)), Mundano, 0, 10));
}
