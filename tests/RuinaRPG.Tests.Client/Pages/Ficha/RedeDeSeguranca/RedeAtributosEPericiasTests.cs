using System.Net;
using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;

/// <summary>
/// Rede de segurança do E: a aba Atributos &amp; Perícias das três fichas, como GM e como jogador — o que se lê
/// (Progressão do nível, Atributos e o orçamento, Perícias e o orçamento de cada tipo, Afinidades) e as
/// requisições de cada edição (atributo, gasto e atributo da perícia, adicionar/remover afinidade), com a
/// recarga que vem depois, na ordem. Fixa o que a tela faz hoje; precisa passar sem mudanças depois de cada
/// passo do E. As classes aninhadas existem só para o xUnit rodá-las em paralelo.
/// </summary>
public abstract class RedeAtributosEPericiasTests : RedeDeSegurancaBase
{
    protected const string Aba = "Atributos & Perícias";

    public static TheoryData<string, string> TiposEPapeis => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
        { "Criatura", "GM" }, { "Criatura", "Jogador" },
    };

    public static TheoryData<string, string> TiposComAfinidades => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
    };

    /// <summary>Abrir a aba pede só o que os componentes dela carregam sozinhos (sem ordem entre eles).</summary>
    protected static string[] AberturaDaAba(string tipo) => tipo == "Criatura"
        ? new[] { "GET /creature-sheets/s1/equipment-penalties" }
        : new[] { $"GET /{Prefixo(tipo)}/s1/equipment-penalties", "GET /tabela-de-niveis" };

    protected async Task<IRenderedComponent<IComponent>> AbrirAbaAsync(string tipo, string papel)
    {
        var cut = await RenderFichaAsync(tipo, papel);
        LimparRequisicoes();
        await IrParaAbaAsync(cut, Aba);
        await EsperarRequisicoesAsync(cut, todas => todas.Should().BeEquivalentTo(AberturaDaAba(tipo)));
        LimparRequisicoes();
        return cut;
    }

    // ---------------- o que se lê ----------------

    protected static readonly string[] Atributos =
    {
        "Atributos", "Pontos de Atributo: 2 / 10",
        "Atributo", "Gasto", "Bônus", "Proeficiência", "Total",
        "Vigor", "«2»", "«1»", "[ ]", "3",
    };

    protected static readonly string[] TabelaDePericias =
    {
        "Perícia", "Gasto", "Modificador", "Atributo", "Total",
        "Atletismo", "«2»", "1", "Vigor", "3",
    };

    protected static string[] Afinidades(string vocacaoArcana) => new[]
    {
        "Afinidades", vocacaoArcana, "«0»", "Afinidade Adicional",
        "Essência Básica 1", "Essência Básica 2", "Sub-Elemento", "Experiência",
        "Fogo", "«2»", "«»", "«»", "—", "«»", "«1»", "Remover",
        "«»", "«»", "«»", "«»", "—", "«»", "«»", "Adicionar",
    };

    protected static IEnumerable<string> LeituraDaAba(string tipo) => tipo switch
    {
        // Nível 3 → 3 × 2 Pontos de Perícia na Tabela de Níveis da rede.
        "Personagem" => new[] { "Progressão do nível", "Ver saldos e limites do nível", "Pontos de Perícia", "6" }
            .Concat(Atributos)
            .Concat(new[]
            {
                "Perícias", "Pontos de Perícia: 2 / 8", "Pontos ganhos por Acerto Crítico: 1",
                "«»", "Adicionar/subtrair pontos de Acerto Crítico", "Aplicar",
            })
            .Concat(TabelaDePericias)
            .Concat(Afinidades("Vocação Arcana: 2 / 6")),
        "NPC" => new[] { "Progressão do nível", "Ver saldos e limites do nível", "Pontos de Perícia", "8" }
            .Concat(Atributos)
            .Concat(new[] { "Perícias", "Pontos de Perícia Gastos: 2" })
            .Concat(TabelaDePericias)
            .Concat(Afinidades("Vocação Arcana: 2 / 0")),
        _ => Atributos
            .Concat(new[] { "Perícias", "Pontos de Perícia Gastos: 2" })
            .Concat(TabelaDePericias),
    };

    // ---------------- requisições ----------------

    /// <summary>A gravação, a recarga das abas na ordem e, quando a página sobe a versão das penalidades, o alerta delas.</summary>
    protected async Task AfirmarGravacaoAsync(IRenderedComponent<IComponent> cut, string tipo, string gravacao, bool recarregaPenalidades)
    {
        var esperado = new List<string> { gravacao };
        esperado.AddRange(RecargaDasAbas(tipo));
        if (recarregaPenalidades)
            esperado.Add($"GET /{Prefixo(tipo)}/s1/equipment-penalties");
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(esperado));
    }

    protected static IElement LinhaDoAtributo(IRenderedComponent<IComponent> cut) => Linha(Tabela(AbaAberta(cut), "Atributo"), "Vigor");

    protected static IElement LinhaDaPericia(IRenderedComponent<IComponent> cut) => Linha(Tabela(AbaAberta(cut), "Perícia"), "Atletismo");

    protected static IElement NovaAfinidade(IRenderedComponent<IComponent> cut) =>
        Tabela(AbaAberta(cut), "Essência Básica 1").QuerySelectorAll("tbody tr").Last();

    /// <summary>Abre o MudSelect de <paramref name="controle"/> e clica na opção <paramref name="opcao"/>.</summary>
    protected static async Task EscolherAsync(IRenderedComponent<IComponent> cut, Func<IElement> controle, string opcao)
    {
        await cut.InvokeAsync(() => controle().QuerySelector(".mud-input-control")!.MouseDown());
        var item = cut.WaitForElements(".mud-popover-open .mud-list-item").Single(li => TextoNormalizado(li) == opcao);
        await cut.InvokeAsync(() => item.Click());
    }

    /// <summary>O que se lê, os controles que mudam com o papel e o que abrir a aba pede.</summary>
    public sealed class Leituras : RedeAtributosEPericiasTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task A_aba_mostra_progressao_atributos_pericias_e_afinidades_nesta_ordem(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Equal(LeituraDaAba(tipo)), TimeSpan.FromSeconds(5));
        }

        // A Leitura não vê atributos: o Modificador em negrito ("Inclui bônus de Histórico") e os campos desabilitados
        // ficam afirmados aqui. GM e jogador editam a aba igual; só a Essência 2 da linha nova (sem Essência 1) fica desabilitada.
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task O_modificador_do_historico_e_os_campos_desabilitados_sao_os_de_hoje(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            var negrito = LinhaDaPericia(cut).QuerySelectorAll("b[title='Inclui bônus de Histórico']").Select(TextoNormalizado);
            negrito.Should().Equal(tipo == "Criatura" ? Array.Empty<string>() : new[] { "1" });

            var desabilitados = AbaAberta(cut).QuerySelectorAll("input[disabled]").Count();
            desabilitados.Should().Be(tipo == "Criatura" ? 0 : 1);
            if (tipo != "Criatura")
                NovaAfinidade(cut).QuerySelectorAll("input[disabled]").Should().HaveCount(1);
            AbaAberta(cut).QuerySelectorAll("button[disabled]").Should().BeEmpty();
        }
    }

    /// <summary>Atributos e Perícias: o PUT de cada edição e a recarga.</summary>
    public sealed class AtributosEPericias : RedeAtributosEPericiasTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Editar_o_gasto_do_atributo_grava_o_atributo_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDoAtributo(cut).QuerySelectorAll("input")[0].Change("4"));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/attributes/Vigor {{\"bonus\":1,\"gasto\":4,\"temMaestria\":false}}", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Editar_o_bonus_do_atributo_grava_o_atributo_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDoAtributo(cut).QuerySelectorAll("input")[1].Change("3"));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/attributes/Vigor {{\"bonus\":3,\"gasto\":2,\"temMaestria\":false}}", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Marcar_a_proeficiencia_grava_o_atributo_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDoAtributo(cut).QuerySelector("input[type=checkbox]")!.Change(true));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/attributes/Vigor {{\"bonus\":1,\"gasto\":2,\"temMaestria\":true}}", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Editar_o_gasto_da_pericia_grava_a_pericia_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDaPericia(cut).QuerySelectorAll("input")[0].Change("5"));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/skills/Atletismo {{\"atributoEscolhido\":\"Vigor\",\"gasto\":5}}", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Tirar_o_atributo_da_pericia_grava_a_pericia_sem_ele_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherAsync(cut, () => LinhaDaPericia(cut).QuerySelectorAll("td")[3], "—");

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/skills/Atletismo {{\"atributoEscolhido\":null,\"gasto\":2}}", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Escolher_o_atributo_da_pericia_grava_a_pericia_com_ele_e_recarrega(string tipo, string papel)
        {
            // A perícia começa sem atributo; as opções são os atributos da ficha (só Vigor na rede).
            Responder("/skills", new[] { new { pericia = "Atletismo", gasto = 2, modificador = 1, atributoEscolhido = (string?)null, total = 3, contaHistorico = true, nome = "Atletismo", descricao = "Correr e saltar." } });
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherAsync(cut, () => LinhaDaPericia(cut).QuerySelectorAll("td")[3], "Vigor");

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/skills/Atletismo {{\"atributoEscolhido\":\"Vigor\",\"gasto\":2}}", recarregaPenalidades: true);
        }

        // Diferença A6 do inventário (a decidir): Personagem e NPC mostram a mensagem do servidor; a Criatura não mostra nada.
        [Theory]
        [InlineData("Personagem")]
        [InlineData("NPC")]
        [InlineData("Criatura")]
        public async Task Um_atributo_recusado_mostra_a_mensagem_do_servidor_menos_na_criatura(string tipo)
        {
            ResponderErro("/attributes/Vigor", HttpStatusCode.BadRequest, "Acima do orçamento.");
            var cut = await AbrirAbaAsync(tipo, "GM");

            await cut.InvokeAsync(() => LinhaDoAtributo(cut).QuerySelectorAll("input")[0].Change("40"));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/attributes/Vigor {{\"bonus\":1,\"gasto\":40,\"temMaestria\":false}}", recarregaPenalidades: true);
            var alertas = cut.FindAll(".mud-alert").Select(TextoNormalizado);
            alertas.Should().Equal(tipo == "Criatura" ? Array.Empty<string>() : new[] { "Acima do orçamento." });
        }

        // Idem para a perícia (A6).
        [Theory]
        [InlineData("Personagem")]
        [InlineData("NPC")]
        [InlineData("Criatura")]
        public async Task Uma_pericia_recusada_mostra_a_mensagem_do_servidor_menos_na_criatura(string tipo)
        {
            ResponderErro("/skills/Atletismo", HttpStatusCode.BadRequest, "Perícia acima do máximo.");
            var cut = await AbrirAbaAsync(tipo, "GM");

            await cut.InvokeAsync(() => LinhaDaPericia(cut).QuerySelectorAll("input")[0].Change("40"));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/skills/Atletismo {{\"atributoEscolhido\":\"Vigor\",\"gasto\":40}}", recarregaPenalidades: true);
            var alertas = cut.FindAll(".mud-alert").Select(TextoNormalizado);
            alertas.Should().Equal(tipo == "Criatura" ? Array.Empty<string>() : new[] { "Perícia acima do máximo." });
        }
    }

    /// <summary>Afinidades (Personagem e NPC): adicionar e remover.</summary>
    public sealed class AfinidadesDaFicha : RedeAtributosEPericiasTests
    {
        [Theory]
        [MemberData(nameof(TiposComAfinidades))]
        public async Task Adicionar_uma_afinidade_grava_a_linha_nova_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherAsync(cut, () => NovaAfinidade(cut).QuerySelectorAll("td")[0], "Terra");
            await cut.InvokeAsync(() => NovaAfinidade(cut).QuerySelectorAll("td")[0].QuerySelectorAll("input")[1].Change("3"));
            await cut.InvokeAsync(() => NovaAfinidade(cut).QuerySelectorAll("td")[3].QuerySelector("input")!.Change("2"));
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => Botao(NovaAfinidade(cut), "Adicionar").Click());

            await AfirmarGravacaoAsync(cut, tipo,
                $"POST /{Prefixo(tipo)}/s1/affinities {{\"elemento\":\"Terra\",\"elementoValor\":3,\"experiencia\":2,\"segundaEssencia\":null,\"segundaEssenciaValor\":null,\"subElementoValor\":null}}",
                recarregaPenalidades: false);
        }

        [Theory]
        [MemberData(nameof(TiposComAfinidades))]
        public async Task Remover_uma_afinidade_apaga_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(Tabela(AbaAberta(cut), "Essência Básica 1").QuerySelectorAll("tbody tr")[0], "Remover").Click());

            await AfirmarGravacaoAsync(cut, tipo, $"DELETE /{Prefixo(tipo)}/s1/affinities/afi1", recarregaPenalidades: false);
        }
    }
}
