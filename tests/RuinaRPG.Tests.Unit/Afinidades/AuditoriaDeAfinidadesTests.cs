using RuinaRPG.Domain.Afinidades;

namespace RuinaRPG.Tests.Unit.Afinidades;

/// <summary>O que o Auditor pode gravar no catálogo de Afinidades (Auditoria de Regras R0018–R0021).</summary>
public class AuditoriaDeAfinidadesTests
{
    private const int Terra = 0, Agua = 1, Fogo = 2, Ar = 3, Gelo = 4, Raio = 7, Alma = 9;

    private static readonly CatalogoDeAfinidades Catalogo = new(
        [
            new(Terra, "Terra", TipoDeElemento.Base, null), new(Agua, "Água", TipoDeElemento.Base, null),
            new(Fogo, "Fogo", TipoDeElemento.Base, null), new(Ar, "Ar", TipoDeElemento.Base, null),
            new(Gelo, "Gelo", TipoDeElemento.SubElemento, null), new(Raio, "Raio", TipoDeElemento.SubElemento, null),
            new(Alma, "Alma", TipoDeElemento.Caminho, null),
        ],
        [new(Agua, 1, Ar, 1, Gelo), new(Fogo, 1, Ar, 1, Raio)],
        new Dictionary<int, int> { [0] = 3 },
        []);

    // ── Elementos ──

    [Fact]
    public void Um_elemento_novo_valido_e_aceito() =>
        Assert.Null(AuditoriaDeAfinidades.MotivoParaRecusarElemento(Catalogo, null, "Luz", TipoDeElemento.SubElemento, 5));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void O_nome_e_obrigatorio(string nome) =>
        Assert.Equal("O nome do elemento é obrigatório.",
            AuditoriaDeAfinidades.MotivoParaRecusarElemento(Catalogo, null, nome, TipoDeElemento.Base, null));

    [Theory]
    [InlineData("Gelo")]
    [InlineData(" gelo ")]
    public void O_nome_e_unico_sem_diferenciar_caixa(string nome) =>
        Assert.Equal("Já existe um elemento chamado Gelo.",
            AuditoriaDeAfinidades.MotivoParaRecusarElemento(Catalogo, null, nome, TipoDeElemento.SubElemento, null));

    [Fact]
    public void Editar_um_elemento_mantendo_o_proprio_nome_e_aceito() =>
        Assert.Null(AuditoriaDeAfinidades.MotivoParaRecusarElemento(Catalogo, Gelo, "Gelo", TipoDeElemento.SubElemento, 4));

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void O_teto_e_vazio_ou_pelo_menos_1(int teto) =>
        Assert.Equal("O teto deve ser vazio (sem limite) ou um inteiro maior ou igual a 1.",
            AuditoriaDeAfinidades.MotivoParaRecusarElemento(Catalogo, null, "Luz", TipoDeElemento.SubElemento, teto));

    [Fact]
    public void O_Sub_Elemento_gerado_por_um_cruzamento_nao_muda_de_tipo() =>
        Assert.Equal("Gelo é gerado por um cruzamento: continua Sub-Elemento enquanto o cruzamento existir.",
            AuditoriaDeAfinidades.MotivoParaRecusarElemento(Catalogo, Gelo, "Gelo", TipoDeElemento.Base, null));

    [Fact]
    public void Um_Elemento_base_pode_virar_Caminho() =>
        Assert.Null(AuditoriaDeAfinidades.MotivoParaRecusarElemento(Catalogo, Terra, "Terra", TipoDeElemento.Caminho, null));

    // ── Remoção ──

    [Theory]
    [InlineData(0, 0, 0, null)]
    [InlineData(3, 2, 1, "Este elemento é usado por 3 ficha(s), 2 Condição(ões) e 1 Passiva(s).")]
    [InlineData(2, 0, 0, "Este elemento é usado por 2 ficha(s).")]
    [InlineData(0, 1, 0, "Este elemento é usado por 1 Condição(ões).")]
    [InlineData(0, 0, 4, "Este elemento é usado por 4 Passiva(s).")]
    [InlineData(1, 0, 2, "Este elemento é usado por 1 ficha(s) e 2 Passiva(s).")]
    [InlineData(0, 5, 1, "Este elemento é usado por 5 Condição(ões) e 1 Passiva(s).")]
    public void A_remocao_e_recusada_com_as_contagens_nao_zero(int fichas, int condicoes, int passivas, string? esperado) =>
        Assert.Equal(esperado, AuditoriaDeAfinidades.MotivoParaRecusarRemocao(fichas, condicoes, passivas));

    // ── Cruzamentos ──

    [Fact]
    public void Ordenado_poe_o_menor_id_primeiro_e_leva_o_minimo_junto()
    {
        Assert.Equal(new Cruzamento(Terra, 2, Alma, 1, Gelo), new Cruzamento(Alma, 1, Terra, 2, Gelo).Ordenado());
        Assert.Equal(new Cruzamento(Terra, 2, Alma, 1, Gelo), new Cruzamento(Terra, 2, Alma, 1, Gelo).Ordenado());
    }

    [Fact]
    public void Um_cruzamento_novo_valido_e_aceito() =>
        Assert.Null(AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(Terra, 2, Alma, 1, Raio), null));

    [Fact]
    public void Os_dois_elementos_sao_diferentes() =>
        Assert.Equal("Os dois elementos do cruzamento devem ser diferentes.",
            AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(Terra, 1, Terra, 1, Raio), null));

    [Theory]
    [InlineData(77, Terra, Raio)]
    [InlineData(Terra, 77, Raio)]
    [InlineData(Terra, Alma, 77)]
    public void Todo_elemento_existe_no_catalogo(int a, int b, int sub) =>
        Assert.Equal("Elemento inexistente.",
            AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(a, 1, b, 1, sub), null));

    [Theory]
    [InlineData(Fogo)]
    [InlineData(Alma)]
    public void O_resultado_e_um_Sub_Elemento(int sub) =>
        Assert.Equal($"{Catalogo.NomeDe(sub)} não é um Sub-Elemento.",
            AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(Terra, 1, Agua, 1, sub), null));

    [Fact]
    public void O_Sub_Elemento_nao_e_um_dos_dois_elementos() =>
        Assert.Equal("O Sub-Elemento não pode ser um dos dois elementos do cruzamento.",
            AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(Gelo, 1, Terra, 1, Gelo), null));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void Os_minimos_sao_pelo_menos_1(int minimoA, int minimoB) =>
        Assert.Equal("Os mínimos do cruzamento devem ser inteiros maiores ou iguais a 1.",
            AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(Terra, minimoA, Alma, minimoB, Raio), null));

    [Theory]
    [InlineData(Agua, Ar)]
    [InlineData(Ar, Agua)] // o par é simétrico
    public void O_par_e_unico(int a, int b) =>
        Assert.Equal("Já existe um cruzamento de Água e Ar.",
            AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(a, 1, b, 1, Raio), null));

    [Fact]
    public void Editar_um_cruzamento_mantendo_o_par_e_aceito() =>
        Assert.Null(AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(Ar, 3, Agua, 2, Gelo), new(Agua, 1, Ar, 1, Gelo)));

    [Fact]
    public void Editar_um_cruzamento_para_o_par_de_outro_e_recusado() =>
        Assert.Equal("Já existe um cruzamento de Fogo e Ar.",
            AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(Catalogo, new(Fogo, 1, Ar, 1, Gelo), new(Agua, 1, Ar, 1, Gelo)));

    // ── Visibilidade ──

    [Theory]
    [InlineData(OrigemDaVisibilidade.Vocacao, "Feiticeiro", Alma)]
    [InlineData(OrigemDaVisibilidade.Vocacao, "Bruxo", Gelo)]
    [InlineData(OrigemDaVisibilidade.Classe, "Piromante", Raio)]
    [InlineData(OrigemDaVisibilidade.Classe, "Lamina Holística", Alma)]
    public void Uma_ligacao_valida_e_aceita(OrigemDaVisibilidade origem, string chave, int elemento) =>
        Assert.Null(AuditoriaDeAfinidades.MotivoParaRecusarVisibilidade(Catalogo, origem, chave, elemento));

    [Fact]
    public void O_elemento_da_ligacao_existe() =>
        Assert.Equal("Elemento inexistente.",
            AuditoriaDeAfinidades.MotivoParaRecusarVisibilidade(Catalogo, OrigemDaVisibilidade.Vocacao, "Adepto", 77));

    [Fact]
    public void Um_Elemento_base_nao_tem_ligacao() =>
        Assert.Equal("Terra é um Elemento base: aparece sempre.",
            AuditoriaDeAfinidades.MotivoParaRecusarVisibilidade(Catalogo, OrigemDaVisibilidade.Vocacao, "Adepto", Terra));

    [Theory]
    [InlineData("Campeao")]
    [InlineData("Cacador")]
    [InlineData("Paladino")]
    [InlineData("")]
    public void So_as_Vocacoes_magicas_tem_ligacao(string chave) =>
        Assert.Equal("Só as Vocações mágicas (Feiticeiro, Adepto e Bruxo) têm elementos ligados.",
            AuditoriaDeAfinidades.MotivoParaRecusarVisibilidade(Catalogo, OrigemDaVisibilidade.Vocacao, chave, Alma));

    [Theory]
    [InlineData("Clérigo")]
    [InlineData("Feiticeiro")]
    public void A_Classe_e_uma_da_tabela_atual(string chave) =>
        Assert.Equal($"Classe desconhecida: {chave}.",
            AuditoriaDeAfinidades.MotivoParaRecusarVisibilidade(Catalogo, OrigemDaVisibilidade.Classe, chave, Alma));

    // ── Total por Círculo ──

    [Theory]
    [InlineData(0, null)]
    [InlineData(12, null)]
    [InlineData(-1, "O total deve ser um inteiro maior ou igual a zero.")]
    public void O_total_por_Circulo_nao_e_negativo(int total, string? esperado) =>
        Assert.Equal(esperado, AuditoriaDeAfinidades.MotivoParaRecusarTotal(total));
}
