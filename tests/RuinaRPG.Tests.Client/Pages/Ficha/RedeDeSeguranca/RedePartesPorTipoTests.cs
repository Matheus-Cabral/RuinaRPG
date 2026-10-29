using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;

/// <summary>
/// Rede de segurança do E: as partes que só alguns tipos têm e que as outras redes não cobrem — o diálogo de
/// Característica Racial (Personagem e NPC, cada um com o seu texto), os pontos de Acerto Crítico (Personagem),
/// os Espólios (Criatura) e as Maestrias (adicionar e remover nos três; editar na linha nos três). O
/// Nível e a Experiência editados à mão (NPC e Criatura) e os ajustes de XP e PI (Personagem) estão em
/// <see cref="RedeCabecalhoEInformacoesBasicasTests"/>. Fixa o que a tela faz hoje; precisa passar sem mudanças
/// depois de cada passo do E. As classes aninhadas existem só para o xUnit rodá-las em paralelo.
/// </summary>
public abstract class RedePartesPorTipoTests : RedeDeSegurancaBase
{
    public static TheoryData<string, string> TiposEPapeis => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
        { "Criatura", "GM" }, { "Criatura", "Jogador" },
    };

    public static TheoryData<string, string> TiposComLinhagem => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
    };

    public static TheoryData<string> Papeis => new() { "GM", "Jogador" };

    protected static string K(string tipo) => $"/{Prefixo(tipo)}/s1";

    protected async Task<IRenderedComponent<IComponent>> AbrirAbaAsync(string tipo, string papel, string aba, params string[] abertura)
    {
        var cut = await RenderFichaAsync(tipo, papel);
        LimparRequisicoes();
        await IrParaAbaAsync(cut, aba);
        if (abertura.Length == 0)
        {
            await EsperarCalmariaAsync();
            Requisicoes.Should().BeEmpty();
        }
        else
        {
            await EsperarRequisicoesAsync(cut, todas => todas.Should().BeEquivalentTo(abertura));
        }
        LimparRequisicoes();
        return cut;
    }

    protected static IElement FormularioDe(IRenderedComponent<IComponent> cut, string botao) => Botao(AbaAberta(cut), botao).Closest("form")!;

    /// <summary>Diálogo de Característica Racial: abre sozinho na carga quando a Variante tem escolha pendente.</summary>
    public sealed class CaracteristicaRacial : RedePartesPorTipoTests
    {
        private static object Opcao(string nome, bool requerEspecificacao = false) =>
            new { traitNome = nome, especificacao = (string?)null, requerEspecificacao };

        private static readonly object ComObrigatoriaFixa = new
        {
            pending = true,
            gratuita = new[] { Opcao("Ágil"), Opcao("Robusto") },
            obrigatoria = new[] { Opcao("Visão no Escuro") },
        };

        private static readonly object ComObrigatoriaAEscolher = new
        {
            pending = true,
            gratuita = new[] { Opcao("Ágil") },
            obrigatoria = new[] { Opcao("Visão no Escuro"), Opcao("Ofício", requerEspecificacao: true) },
        };

        private static readonly object SemPendencia = new { pending = false, gratuita = Array.Empty<object>(), obrigatoria = Array.Empty<object>() };

        // Diferença A1 (decidida: fica por tipo): o Personagem fala com o jogador, o NPC fala do NPC.
        private static string Titulo(string tipo) => tipo == "Personagem" ? "Escolha suas Características Raciais" : "Escolha a Característica Racial do NPC";

        private static string Automatica(string tipo) => tipo == "Personagem" ? "Você também recebe automaticamente:" : "O NPC também recebe automaticamente:";

        private static async Task MarcarAsync(IRenderedComponent<IComponent> cut, string opcao)
        {
            var radio = Dialogo(cut).QuerySelectorAll("label").First(l => TextoNormalizado(l) == opcao).QuerySelector("input")!;
            await cut.InvokeAsync(() => radio.Click());
        }

        [Theory]
        [MemberData(nameof(TiposComLinhagem))]
        public async Task O_dialogo_mostra_as_opcoes_e_o_texto_de_cada_tipo(string tipo, string papel)
        {
            Responder("/racial-traits/pending", ComObrigatoriaFixa);
            var cut = await RenderFichaAsync(tipo, papel);

            Leitura(Dialogo(cut)).Should().Equal(
                Titulo(tipo),
                "Toda Variante concede uma Característica Gratuita, a escolher entre as opções abaixo — algumas Variantes também exigem uma Característica Obrigatória.",
                "Característica Gratuita", "[ ]", "Ágil", "[ ]", "Robusto",
                Automatica(tipo), "Visão no Escuro", ".",
                "Decidir depois", "Confirmar");
            Botao(Dialogo(cut), "Confirmar").HasAttribute("disabled").Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(TiposComLinhagem))]
        public async Task Confirmar_a_gratuita_grava_a_escolha_e_rele_a_pendencia_e_as_caracteristicas(string tipo, string papel)
        {
            Responder("/racial-traits/pending", ComObrigatoriaFixa);
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await MarcarAsync(cut, "Robusto");
            Botao(Dialogo(cut), "Confirmar").HasAttribute("disabled").Should().BeFalse();
            Requisicoes.Should().BeEmpty();
            Responder("/racial-traits/pending", SemPendencia);
            await cut.InvokeAsync(() => Botao(Dialogo(cut), "Confirmar").Click());

            // A obrigatória fixa não vai no pedido: o servidor a concede sozinho.
            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                $"POST {K(tipo)}/racial-traits/resolve {{\"gratuitaEspecificacao\":null,\"gratuitaTraitNome\":\"Robusto\",\"obrigatoriaEspecificacao\":null,\"obrigatoriaTraitNome\":null}}",
                $"GET {K(tipo)}/racial-traits/pending",
                $"GET {K(tipo)}/traits"));
            cut.FindAll(".mud-dialog").Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(TiposComLinhagem))]
        public async Task Uma_obrigatoria_a_escolher_com_especificacao_vai_no_pedido(string tipo, string papel)
        {
            Responder("/racial-traits/pending", ComObrigatoriaAEscolher);
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await MarcarAsync(cut, "Ágil");
            await MarcarAsync(cut, "Ofício");
            Leitura(Dialogo(cut)).Skip(2).Should().Equal(
                "Característica Gratuita", "[x]", "Ágil",
                "Característica Obrigatória", "[ ]", "Visão no Escuro", "[x]", "Ofício", "«»", "Especificação",
                "Decidir depois", "Confirmar");
            await cut.InvokeAsync(() => Campo(Dialogo(cut), "Especificação").Change("Ferreiro"));
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => Botao(Dialogo(cut), "Confirmar").Click());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                $"POST {K(tipo)}/racial-traits/resolve {{\"gratuitaEspecificacao\":null,\"gratuitaTraitNome\":\"Ágil\",\"obrigatoriaEspecificacao\":\"Ferreiro\",\"obrigatoriaTraitNome\":\"Ofício\"}}",
                $"GET {K(tipo)}/racial-traits/pending",
                $"GET {K(tipo)}/traits"));
        }

        [Theory]
        [MemberData(nameof(TiposComLinhagem))]
        public async Task Decidir_depois_fecha_sem_gravar(string tipo, string papel)
        {
            Responder("/racial-traits/pending", ComObrigatoriaFixa);
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await cut.InvokeAsync(() => Botao(Dialogo(cut), "Decidir depois").Click());

            cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Should().BeEmpty());
            await EsperarCalmariaAsync();
            Requisicoes.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Sem_escolha_pendente_nao_ha_dialogo(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);

            cut.FindAll(".mud-dialog").Should().BeEmpty();
        }
    }

    /// <summary>Pontos de Acerto Crítico (só Personagem): o ajuste salva a ficha e relê as abas 2 e 3.</summary>
    public sealed class AcertoCritico : RedePartesPorTipoTests
    {
        private const string Delta = "Adicionar/subtrair pontos de Acerto Crítico";

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Aplicar_soma_os_pontos_salva_a_ficha_e_rele_as_abas(string papel)
        {
            var cut = await AbrirAbaAsync("Personagem", papel, "Atributos & Perícias", "GET /character-sheets/s1/equipment-penalties", "GET /tabela-de-niveis");

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), Delta).Change("2"));
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => Botao(AbaAberta(cut), "Aplicar").Click());

            var put = $"PUT /character-sheets/s1 {RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha("Personagem", nome: "Aria").Replace("\"pontosDePericiaBonusCritico\":1", "\"pontosDePericiaBonusCritico\":3")}";
            // O salvamento relê a ficha (e sobe a versão das penalidades); depois o ajuste relê as abas 2 e 3 de novo, na ordem.
            await EsperarRequisicoesAsync(cut, todas =>
            {
                todas.FirstOrDefault().Should().Be(put);
                todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar("Personagem")
                    .Concat(RecargaDasAbas("Personagem")).Append("GET /character-sheets/s1/equipment-penalties"));
                todas.Where(r => r != "GET /character-sheets/s1/equipment-penalties").TakeLast(RecargaDasAbas("Personagem").Length)
                    .Should().Equal(RecargaDasAbas("Personagem"));
            });
            Campo(AbaAberta(cut), Delta).GetAttribute("value").Should().BeNullOrEmpty();
        }

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Aplicar_sem_valor_nao_faz_nada(string papel)
        {
            var cut = await AbrirAbaAsync("Personagem", papel, "Atributos & Perícias", "GET /character-sheets/s1/equipment-penalties", "GET /tabela-de-niveis");

            await cut.InvokeAsync(() => Botao(AbaAberta(cut), "Aplicar").Click());

            await EsperarCalmariaAsync();
            Requisicoes.Should().BeEmpty();
        }
    }

    /// <summary>Espólios (só Criatura): buscar em todo o catálogo, adicionar, quantidade, remover.</summary>
    public sealed class Espolios : RedePartesPorTipoTests
    {
        // O Espólio aceita qualquer item: a busca não filtra por Tipo.
        private static string Busca(string papel) => papel == "GM" ? "GET /items?nome=" : "GET /campaigns/c1/available-items?nome=";

        private static IElement LinhaDoEspolio(IRenderedComponent<IComponent> cut) =>
            Linha(AbaAberta(cut).QuerySelectorAll("table").Single(t => t.QuerySelectorAll("th").Any(th => TextoNormalizado(th) == "DT")), "Presa de Lobo");

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Adicionar_um_espolio_busca_em_todo_o_catalogo_grava_e_rele(string papel)
        {
            var cut = await AbrirAbaAsync("Criatura", papel, "Posses");

            await EscolherNoSeletorAsync(cut, 0, "i6b", "Couro de Lobo");
            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Espólio"), "Quantidade").Change("3"));
            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Espólio"), "DT").Change("14"));
            Requisicoes.Should().Equal(Busca(papel));
            await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Espólio").Submit());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                Busca(papel), "POST /creature-sheets/s1/spoils {\"dt\":14,\"itemId\":\"i6b\",\"qtd\":3}", "GET /creature-sheets/s1/spoils"));
        }

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task A_quantidade_grava_pelo_endpoint_dela_e_rele(string papel)
        {
            var cut = await AbrirAbaAsync("Criatura", papel, "Posses");

            await cut.InvokeAsync(() => LinhaDoEspolio(cut).QuerySelector("input")!.Change("4"));

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal("PUT /creature-sheets/s1/spoils/sp1/qtd 4", "GET /creature-sheets/s1/spoils"));
        }

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Remover_um_espolio_apaga_e_rele(string papel)
        {
            var cut = await AbrirAbaAsync("Criatura", papel, "Posses");

            await cut.InvokeAsync(() => Botao(LinhaDoEspolio(cut), "Remover").Click());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal("DELETE /creature-sheets/s1/spoils/sp1", "GET /creature-sheets/s1/spoils"));
        }
    }

    /// <summary>Maestrias (aba Combate): adicionar e remover nos três tipos; editar na linha só no Personagem.</summary>
    public sealed class Maestrias : RedePartesPorTipoTests
    {
        private Task<IRenderedComponent<IComponent>> AbrirCombateAsync(string tipo, string papel) =>
            AbrirAbaAsync(tipo, papel, "Combate", $"GET {K(tipo)}/equipment-penalties");

        private static IElement LinhaDaMaestria(IRenderedComponent<IComponent> cut) =>
            AbaAberta(cut).QuerySelectorAll("table").Single(t => t.QuerySelectorAll("th").Any(th => TextoNormalizado(th) == "Gasto Maestria")).QuerySelectorAll("tbody tr").Single();

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Adicionar_uma_maestria_grava_e_rele(string tipo, string papel)
        {
            var cut = await AbrirCombateAsync(tipo, papel);

            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Maestria"), "Nome").Change("Defesa Firme"));
            await EscolherOpcaoAsync(cut, () => FormularioDe(cut, "Adicionar Maestria"), "Perícia", "Atletismo");
            await EscolherOpcaoAsync(cut, () => FormularioDe(cut, "Adicionar Maestria"), "Atributo", "Vigor");
            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Maestria"), "Gasto Maestria").Change("2"));
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Maestria").Submit());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                $"POST {K(tipo)}/masteries {{\"atributo\":\"Vigor\",\"gastoMaestria\":2,\"nome\":\"Defesa Firme\",\"pericia\":\"Atletismo\"}}",
                $"GET {K(tipo)}/masteries"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Remover_uma_maestria_apaga_e_rele(string tipo, string papel)
        {
            var cut = await AbrirCombateAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDaMaestria(cut), "Remover").Click());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"DELETE {K(tipo)}/masteries/ma1", $"GET {K(tipo)}/masteries"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Editar_o_nome_na_linha_grava_a_maestria(string tipo, string papel)
        {
            var cut = await AbrirCombateAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDaMaestria(cut).QuerySelectorAll("input")[0].Change("Golpe Certeiro"));

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                $"PUT {K(tipo)}/masteries/ma1 {{\"atributo\":\"Vigor\",\"gastoMaestria\":1,\"nome\":\"Golpe Certeiro\",\"pericia\":\"Atletismo\"}}",
                $"GET {K(tipo)}/masteries"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Editar_o_gasto_na_linha_grava_a_maestria(string tipo, string papel)
        {
            var cut = await AbrirCombateAsync(tipo, papel);

            var gasto = LinhaDaMaestria(cut).QuerySelectorAll("td")[3].QuerySelector("input")!;
            await cut.InvokeAsync(() => gasto.Change("3"));

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                $"PUT {K(tipo)}/masteries/ma1 {{\"atributo\":\"Vigor\",\"gastoMaestria\":3,\"nome\":\"Golpe Firme\",\"pericia\":\"Atletismo\"}}",
                $"GET {K(tipo)}/masteries"));
        }

        // Um erro do servidor ao editar a Maestria aparece no alerta da página.
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Uma_edicao_recusada_mostra_a_mensagem_do_servidor(string tipo, string papel)
        {
            var cut = await AbrirCombateAsync(tipo, papel);
            ResponderErro("/masteries/ma1", System.Net.HttpStatusCode.BadRequest, "Gasto acima do limite.");

            var gasto = LinhaDaMaestria(cut).QuerySelectorAll("td")[3].QuerySelector("input")!;
            await cut.InvokeAsync(() => gasto.Change("30"));

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                $"PUT {K(tipo)}/masteries/ma1 {{\"atributo\":\"Vigor\",\"gastoMaestria\":30,\"nome\":\"Golpe Firme\",\"pericia\":\"Atletismo\"}}",
                $"GET {K(tipo)}/masteries"));
            cut.FindAll(".mud-alert").Select(TextoNormalizado).Should().Equal("Gasto acima do limite.");
        }
    }
}
