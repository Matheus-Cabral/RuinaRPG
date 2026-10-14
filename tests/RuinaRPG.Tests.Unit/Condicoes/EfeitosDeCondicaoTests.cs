using RuinaRPG.Domain.Condicoes;

namespace RuinaRPG.Tests.Unit.Condicoes;

public class EfeitosDeCondicaoTests
{
    private static ModificadorDeCondicao Fixo(AlvoDeCondicao alvo, string? especifico, int valor) =>
        new(alvo, especifico, TipoDeValorDeCondicao.Fixo, valor, null);

    private static ModificadorDeCondicao Pct(AlvoDeCondicao alvo, string? especifico, int valor) =>
        new(alvo, especifico, TipoDeValorDeCondicao.Percentual, valor, null);

    private static ModificadorDeCondicao Atual(string recurso, int valor, MomentoDeCondicao momento, TipoDeValorDeCondicao tipo = TipoDeValorDeCondicao.Fixo) =>
        new(AlvoDeCondicao.RecursoAtual, recurso, tipo, valor, momento);

    private static CondicaoAtiva Cond(string nome, params ModificadorDeCondicao[] linhas) =>
        new(Guid.NewGuid(), nome, linhas, new Dictionary<CategoriaDesabilitavel, IReadOnlySet<Guid>>());

    private static CondicaoAtiva Desab(string nome, params (CategoriaDesabilitavel Categoria, Guid[] Itens)[] regras) =>
        new(Guid.NewGuid(), nome, [],
            regras.ToDictionary(r => r.Categoria, r => (IReadOnlySet<Guid>)r.Itens.ToHashSet()));

    private static EfeitosDeCondicao Efeitos(params CondicaoAtiva[] ativas) => new(ativas);

    [Fact]
    public void Nenhum_devolve_a_base_e_e_vazio()
    {
        Assert.True(EfeitosDeCondicao.Nenhum.Vazio);
        Assert.Equal(7, EfeitosDeCondicao.Nenhum.Ajustar(AlvoDeCondicao.Atributo, "Forca", 7));
        Assert.Equal(0, EfeitosDeCondicao.Nenhum.Delta(AlvoDeCondicao.Atributo, "Forca", 7));
        Assert.Null(EfeitosDeCondicao.Nenhum.DesabilitadoPor(CategoriaDesabilitavel.Arma, Guid.NewGuid()));
        Assert.Empty(EfeitosDeCondicao.Nenhum.Explicar());
        Assert.False(Efeitos(Cond("A", Fixo(AlvoDeCondicao.Atributo, "Forca", 1))).Vazio);
    }

    [Fact]
    public void Somente_fixo()
    {
        var e = Efeitos(Cond("A", Fixo(AlvoDeCondicao.Atributo, "Forca", -2)));
        Assert.Equal(3, e.Ajustar(AlvoDeCondicao.Atributo, "Forca", 5));
        Assert.Equal(-2, e.Delta(AlvoDeCondicao.Atributo, "Forca", 5));
    }

    [Fact]
    public void Somente_percentual()
    {
        var e = Efeitos(Cond("A", Pct(AlvoDeCondicao.Pericia, null, 50)));
        Assert.Equal(15, e.Ajustar(AlvoDeCondicao.Pericia, "luta", 10));
        Assert.Equal(5, e.Delta(AlvoDeCondicao.Pericia, "luta", 10));
    }

    [Fact]
    public void Fixo_e_percentual_o_percentual_incide_sobre_base_mais_fixos()
    {
        var e = Efeitos(Cond("A", Fixo(AlvoDeCondicao.Atributo, "Vigor", 2), Pct(AlvoDeCondicao.Atributo, "Vigor", 50)));
        Assert.Equal(12, e.Ajustar(AlvoDeCondicao.Atributo, "Vigor", 6)); // (6+2) * 1.5
    }

    [Fact]
    public void Duas_condicoes_somam()
    {
        var e = Efeitos(
            Cond("A", Fixo(AlvoDeCondicao.Atributo, "Vigor", 2), Pct(AlvoDeCondicao.Atributo, "Vigor", 10)),
            Cond("B", Fixo(AlvoDeCondicao.Atributo, "Vigor", 1), Pct(AlvoDeCondicao.Atributo, "Vigor", 20)));
        Assert.Equal(16, e.Ajustar(AlvoDeCondicao.Atributo, "Vigor", 10)); // (10+3) * 1.30 = 16.9, para baixo
    }

    [Fact]
    public void Geral_e_especifico_sao_compativeis()
    {
        var e = Efeitos(Cond("A", Fixo(AlvoDeCondicao.Pericia, null, -1), Fixo(AlvoDeCondicao.Pericia, "Luta", -2)));
        Assert.Equal(7, e.Ajustar(AlvoDeCondicao.Pericia, "luta", 10));  // ignora caixa
        Assert.Equal(9, e.Ajustar(AlvoDeCondicao.Pericia, "Furtividade", 10)); // só a geral
        Assert.Equal(9, e.Ajustar(AlvoDeCondicao.Pericia, null, 10));    // pedido sem alvo só pega linhas gerais
    }

    [Fact]
    public void Pedido_com_alvo_nulo_nao_pega_linha_especifica()
    {
        var e = Efeitos(Cond("A", Fixo(AlvoDeCondicao.Pericia, "Luta", -2)));
        Assert.Equal(10, e.Ajustar(AlvoDeCondicao.Pericia, null, 10));
    }

    [Fact]
    public void Alvo_diferente_nao_casa()
    {
        var e = Efeitos(Cond("A", Fixo(AlvoDeCondicao.Atributo, "Forca", -2)));
        Assert.Equal(5, e.Ajustar(AlvoDeCondicao.Atributo, "Vigor", 5));
        Assert.Equal(5, e.Ajustar(AlvoDeCondicao.Pericia, "Forca", 5));
    }

    [Theory]
    [InlineData(5, -50, 2)]    // floor(2.5)
    [InlineData(5, 50, 7)]     // floor(7.5)
    [InlineData(-3, -50, -2)]  // floor(-1.5)
    [InlineData(-3, 50, -5)]   // floor(-4.5)
    [InlineData(5, -100, 0)]
    [InlineData(0, -100, 0)]
    public void Arredonda_para_baixo(int baixo, int pct, int esperado)
    {
        var e = Efeitos(Cond("A", Pct(AlvoDeCondicao.Atributo, "Forca", pct)));
        Assert.Equal(esperado, e.Ajustar(AlvoDeCondicao.Atributo, "Forca", baixo));
        Assert.Equal(esperado - baixo, e.Delta(AlvoDeCondicao.Atributo, "Forca", baixo));
    }

    [Fact]
    public void Nao_trunca_para_zero_resultado_negativo_nem_limita()
    {
        var e = Efeitos(Cond("A", Fixo(AlvoDeCondicao.Atributo, "Forca", -9)));
        Assert.Equal(-4, e.Ajustar(AlvoDeCondicao.Atributo, "Forca", 5));
    }

    [Fact]
    public void Percentuais_somados_nunca_invertem_o_sinal()
    {
        var e = Efeitos(
            Cond("A", Pct(AlvoDeCondicao.Atributo, "Forca", -60)),
            Cond("B", Pct(AlvoDeCondicao.Atributo, "Forca", -60)));
        Assert.Equal(0, e.Ajustar(AlvoDeCondicao.Atributo, "Forca", 10));
        Assert.Equal(-10, e.Delta(AlvoDeCondicao.Atributo, "Forca", 10));
        Assert.Equal(0, e.Ajustar(AlvoDeCondicao.Atributo, "Forca", -4));
    }

    [Fact]
    public void O_limite_de_menos_100_vale_para_a_soma_e_nao_para_cada_linha()
    {
        // −60 −60 +50 = −70: dentro do limite, nada é cortado.
        var e = Efeitos(Cond("A",
            Pct(AlvoDeCondicao.Atributo, "Forca", -60),
            Pct(AlvoDeCondicao.Atributo, "Forca", -60),
            Pct(AlvoDeCondicao.Atributo, "Forca", 50)));
        Assert.Equal(3, e.Ajustar(AlvoDeCondicao.Atributo, "Forca", 10));
    }

    [Fact]
    public void O_limite_do_percentual_incide_sobre_base_mais_fixos()
    {
        var e = Efeitos(Cond("A",
            Fixo(AlvoDeCondicao.Atributo, "Forca", 4),
            Pct(AlvoDeCondicao.Atributo, "Forca", -100),
            Pct(AlvoDeCondicao.Atributo, "Forca", -100)));
        Assert.Equal(0, e.Ajustar(AlvoDeCondicao.Atributo, "Forca", 10));
    }

    [Fact]
    public void ValorDoAjuste_nao_e_limitado_a_menos_100()
    {
        var linha = Atual("Vitalidade", -150, MomentoDeCondicao.AoAplicar, TipoDeValorDeCondicao.Percentual);
        Assert.Equal(-30, DuracaoDeCondicao.ValorDoAjuste(linha, 20));
    }

    [Fact]
    public void Alvo_especifico_com_acento_casa_ignorando_a_caixa()
    {
        var e = Efeitos(Cond("A", Fixo(AlvoDeCondicao.SubAtributo, "Movimentação", -2)));
        Assert.Equal(6, e.Ajustar(AlvoDeCondicao.SubAtributo, "movimentação", 8));
        Assert.Equal(6, e.Ajustar(AlvoDeCondicao.SubAtributo, "MOVIMENTAÇÃO", 8));
        Assert.Equal(8, e.Ajustar(AlvoDeCondicao.SubAtributo, "Movimentacao", 8));
    }

    [Fact]
    public void Explicar_diz_que_o_percentual_de_um_valor_atual_e_sobre_o_total()
    {
        var c = Cond("Sangrando",
            Atual("Vitalidade", -10, MomentoDeCondicao.ACadaVez, TipoDeValorDeCondicao.Percentual),
            Atual("Foco", -5, MomentoDeCondicao.AoAplicar, TipoDeValorDeCondicao.Percentual),
            Atual("Adrenalina", 2, MomentoDeCondicao.ACadaVez));

        Assert.Equal(
        [
            "Vitalidade atual −10% do total a cada vez",
            "Foco atual −5% do total ao aplicar",
            "Adrenalina atual +2 a cada vez"
        ], Assert.Single(Efeitos(c).Explicar()).Linhas);
    }

    [Fact]
    public void RecursoAtual_nunca_participa_de_Ajustar()
    {
        var e = Efeitos(Cond("A", Atual("Vitalidade", -3, MomentoDeCondicao.ACadaVez)));
        Assert.Equal(10, e.Ajustar(AlvoDeCondicao.RecursoAtual, "Vitalidade", 10));
        Assert.Equal(10, e.Ajustar(AlvoDeCondicao.RecursoTotal, "Vitalidade", 10));
    }

    [Fact]
    public void AjustesDeRecursoAtual_filtra_por_momento_e_preserva_a_ordem()
    {
        var a = Cond("A", Atual("Vitalidade", -3, MomentoDeCondicao.ACadaVez), Atual("Foco", -5, MomentoDeCondicao.AoAplicar));
        var b = Cond("B", Atual("Estresse", 1, MomentoDeCondicao.ACadaVez), Fixo(AlvoDeCondicao.Atributo, "Forca", 1));
        var e = Efeitos(a, b);

        var cada = e.AjustesDeRecursoAtual(MomentoDeCondicao.ACadaVez);
        Assert.Equal(["Vitalidade", "Estresse"], cada.Select(x => x.Linha.AlvoEspecifico));
        Assert.Equal([a.Id, b.Id], cada.Select(x => x.Condicao.Id));

        var aplicar = e.AjustesDeRecursoAtual(MomentoDeCondicao.AoAplicar);
        Assert.Single(aplicar);
        Assert.Equal("Foco", aplicar[0].Linha.AlvoEspecifico);
    }

    [Fact]
    public void Desabilita_categoria_inteira_com_conjunto_vazio()
    {
        var e = Efeitos(Desab("Atordoado", (CategoriaDesabilitavel.Arma, [])));
        Assert.Equal("Atordoado", e.DesabilitadoPor(CategoriaDesabilitavel.Arma, Guid.NewGuid()));
        Assert.Null(e.DesabilitadoPor(CategoriaDesabilitavel.Escudo, Guid.NewGuid()));
    }

    [Fact]
    public void Desabilita_so_os_itens_listados()
    {
        var alvo = Guid.NewGuid();
        var e = Efeitos(Desab("Desarmado", (CategoriaDesabilitavel.Arma, [alvo])));
        Assert.Equal("Desarmado", e.DesabilitadoPor(CategoriaDesabilitavel.Arma, alvo));
        Assert.Null(e.DesabilitadoPor(CategoriaDesabilitavel.Arma, Guid.NewGuid()));
    }

    [Fact]
    public void Primeira_condicao_que_desabilita_vence()
    {
        var item = Guid.NewGuid();
        var e = Efeitos(
            Cond("Sem desabilitar", Fixo(AlvoDeCondicao.Atributo, "Forca", 1)),
            Desab("Primeira", (CategoriaDesabilitavel.Arma, [item])),
            Desab("Segunda", (CategoriaDesabilitavel.Arma, [])));
        Assert.Equal("Primeira", e.DesabilitadoPor(CategoriaDesabilitavel.Arma, item));
        Assert.Equal("Segunda", e.DesabilitadoPor(CategoriaDesabilitavel.Arma, Guid.NewGuid()));
    }

    [Fact]
    public void Explicar_produz_os_textos_em_portugues()
    {
        var c = Cond("Enfraquecido",
            Fixo(AlvoDeCondicao.Atributo, "Forca", -2),
            Fixo(AlvoDeCondicao.Atributo, "Influencia", 3),
            Pct(AlvoDeCondicao.Pericia, null, -10),
            Fixo(AlvoDeCondicao.Pericia, "luta", 1),
            Fixo(AlvoDeCondicao.RecursoTotal, "Vitalidade", 5),
            Atual("Vitalidade", -3, MomentoDeCondicao.ACadaVez),
            Atual("Foco", -5, MomentoDeCondicao.AoAplicar),
            Fixo(AlvoDeCondicao.SubAtributo, "Movimentação", -1),
            Fixo(AlvoDeCondicao.AfinidadeElemental, "Agua", 1),
            Fixo(AlvoDeCondicao.AfinidadeElemental, null, 1),
            Fixo(AlvoDeCondicao.DanoElemental, "Fogo", 2),
            Fixo(AlvoDeCondicao.Dano, "Cortante", -1),
            Fixo(AlvoDeCondicao.Dano, null, 4),
            Pct(AlvoDeCondicao.Maestria, null, 20),
            Fixo(AlvoDeCondicao.Maestria, "luta", 1));

        var explicacao = Assert.Single(Efeitos(c).Explicar(k => "Luta de Sangue"));

        Assert.Equal("Enfraquecido", explicacao.Nome);
        Assert.Equal(
        [
            "Força −2",
            "Influência +3",
            "Todas as Perícias −10%",
            "Luta de Sangue +1",
            "Vitalidade total +5",
            "Vitalidade atual −3 a cada vez",
            "Foco atual −5 ao aplicar",
            "Movimentação −1",
            "Afinidade de Água +1",
            "Todas as Afinidades +1",
            "Dano de Fogo +2",
            "Dano Cortante −1",
            "Dano +4",
            "Todas as Maestrias +20%",
            "Maestria de Luta de Sangue +1"
        ], explicacao.Linhas);
    }

    [Fact]
    public void Explicar_sem_traducao_usa_a_chave_da_pericia()
    {
        var e = Efeitos(Cond("A", Fixo(AlvoDeCondicao.Pericia, "luta", 1)));
        Assert.Equal(["luta +1"], e.Explicar()[0].Linhas);
        Assert.Equal(["Luta +1"], e.Explicar(k => "Luta")[0].Linhas);
    }

    [Fact]
    public void Explicar_inclui_as_desabilitacoes()
    {
        var e = Efeitos(Desab("Atordoado",
            (CategoriaDesabilitavel.Escudo, []),
            (CategoriaDesabilitavel.Arma, [])));
        Assert.Equal(["Desabilita: Armas, Escudos"], e.Explicar()[0].Linhas);

        var f = Efeitos(Desab("Desarmado", (CategoriaDesabilitavel.Arma, [Guid.NewGuid()]), (CategoriaDesabilitavel.Runa, [])));
        Assert.Equal(["Desabilita: Armas (itens escolhidos), Runas"], f.Explicar()[0].Linhas);
    }

    [Fact]
    public void Explicar_usa_nomes_em_portugues_para_enums()
    {
        var e = Efeitos(Desab("Tudo",
            (CategoriaDesabilitavel.Arma, []), (CategoriaDesabilitavel.Armadura, []), (CategoriaDesabilitavel.Escudo, []),
            (CategoriaDesabilitavel.Artefato, []), (CategoriaDesabilitavel.Magia, []), (CategoriaDesabilitavel.Habilidade, []),
            (CategoriaDesabilitavel.Runa, []), (CategoriaDesabilitavel.Passiva, [])));
        Assert.Equal(["Desabilita: Armas, Armaduras, Escudos, Artefatos, Magias, Habilidades, Runas, Passivas"], e.Explicar()[0].Linhas);
    }

    // ---- Duração ----

    [Theory]
    [InlineData(3, 2, 3, 2)]
    [InlineData(3, 2, 4, 1)]
    [InlineData(3, 2, 5, 0)]
    [InlineData(3, 2, 9, 0)]   // nunca abaixo de 0
    [InlineData(0, 2, 1, 2)]   // preparação conta a partir do Turno 1
    [InlineData(0, 2, 3, 0)]
    public void TurnosRestantes(int aplicada, int duracao, int atual, int esperado) =>
        Assert.Equal(esperado, DuracaoDeCondicao.TurnosRestantes(aplicada, duracao, atual));

    [Fact]
    public void TurnosRestantes_sem_prazo_e_nulo() =>
        Assert.Null(DuracaoDeCondicao.TurnosRestantes(2, null, 50));

    [Theory]
    [InlineData(3, 2, 4, false)]
    [InlineData(3, 2, 5, true)]   // limite exato
    [InlineData(3, 2, 6, true)]
    [InlineData(0, 2, 2, false)]
    [InlineData(0, 2, 3, true)]
    [InlineData(1, 1, 2, true)]
    public void Expirou(int aplicada, int duracao, int atual, bool esperado) =>
        Assert.Equal(esperado, DuracaoDeCondicao.Expirou(aplicada, duracao, atual));

    [Fact]
    public void Expirou_sem_prazo_nunca() =>
        Assert.False(DuracaoDeCondicao.Expirou(1, null, 1000));

    [Fact]
    public void ValorDoAjuste_fixo_e_percentual()
    {
        Assert.Equal(-3, DuracaoDeCondicao.ValorDoAjuste(Atual("Vitalidade", -3, MomentoDeCondicao.ACadaVez), 25));
        Assert.Equal(-3, DuracaoDeCondicao.ValorDoAjuste(Atual("Vitalidade", -10, MomentoDeCondicao.ACadaVez, TipoDeValorDeCondicao.Percentual), 25));
        Assert.Equal(2, DuracaoDeCondicao.ValorDoAjuste(Atual("Vitalidade", 10, MomentoDeCondicao.AoAplicar, TipoDeValorDeCondicao.Percentual), 25));
        Assert.Equal(-25, DuracaoDeCondicao.ValorDoAjuste(Atual("Vitalidade", -100, MomentoDeCondicao.AoAplicar, TipoDeValorDeCondicao.Percentual), 25));
    }
}
