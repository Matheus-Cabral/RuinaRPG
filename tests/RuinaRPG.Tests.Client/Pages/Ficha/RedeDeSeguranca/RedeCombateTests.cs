using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;

/// <summary>
/// Rede de segurança do E: a aba Combate das três fichas, como GM e como jogador — o que se lê (recursos na
/// ordem de hoje de cada tipo, Sub-Atributos, Modificador de Dano, Armas, Maestrias, Armaduras, Escudos), os
/// controles que mudam com o papel, e as requisições de cada ação de equipamento (busca no catálogo, adicionar,
/// remover, equipar, durabilidade, atribuir slot de armadura) com a recarga que vem depois, na ordem. Fixa o que
/// a tela faz hoje; precisa passar sem mudanças depois de cada passo do E. As classes aninhadas existem só para o
/// xUnit rodá-las em paralelo.
/// </summary>
public abstract class RedeCombateTests : RedeDeSegurancaBase
{
    protected const string Aba = "Combate";

    public static TheoryData<string, string> TiposEPapeis => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
        { "Criatura", "GM" }, { "Criatura", "Jogador" },
    };

    protected async Task<IRenderedComponent<IComponent>> AbrirAbaAsync(string tipo, string papel)
    {
        var cut = await RenderFichaAsync(tipo, papel);
        LimparRequisicoes();
        await IrParaAbaAsync(cut, Aba);
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"GET /{Prefixo(tipo)}/s1/equipment-penalties"));
        LimparRequisicoes();
        return cut;
    }

    // ---------------- o que se lê ----------------

    // A ordem dos recursos é a de hoje de cada tipo (a unificação na ordem do Personagem é uma diferença decidida, Task 4).
    protected static string[] Recursos(string tipo) => tipo switch
    {
        "Personagem" => new[]
        {
            "Pontos de Sina", "[x]", "[ ]", "[ ]",
            "«20»", "/ 25", "Vitalidade", "«10»", "/ 12", "Foco (PF)", "«3»", "/ 5", "Adrenalina (PA)", "«1»", "/ 10", "Estresse",
        },
        "NPC" => new[]
        {
            "Pontos de Sina", "[ ]", "[x]", "[ ]",
            "«4»", "/ 6", "Adrenalina (PA)", "«6»", "/ 8", "Foco (PF)", "«2»", "/ 10", "Estresse", "«30»", "/ 32", "Vitalidade",
        },
        _ => new[] { "«2»", "/ 3", "Adrenalina (PA)", "«4»", "/ 4", "Arcana (PF)", "«18»", "/ 18", "Vitalidade" },
    };

    protected static IEnumerable<string> SubAtributos(string tipo)
    {
        var lista = new List<string>
        {
            "Sub-Atributos", "Iniciativa: 5", "Movimentação: 9", "Esquiva Natural: 11", "Redução Física: 2", "Redução Mágica: 1", "Defesa Natural: 13",
            "Cobertura", "Sem cobertura",
        };
        if (tipo != "Criatura")
            lista.AddRange(new[]
            {
                "Afinidade Elemental", tipo == "Personagem" ? "Fogo" : "Terra", "Afinidade",
                "Eficiência Elemental", "3", "conforme a Tabela de Afinidades", "Dano Elemental", "2", "conforme a Tabela de Afinidades",
            });
        return lista;
    }

    protected static readonly string[] ModificadorDeDano = { "Modificador de Dano", "Cortante: 1", "Perfurante: 0", "Contundente: 0", "Mágico: 0" };

    // A Criatura não tem Peso nas armas e escolhe a Origem antes do catálogo.
    protected static IEnumerable<string> Armas(string tipo)
    {
        var comPeso = tipo != "Criatura";
        var lista = new List<string> { "Armas" };
        if (!comPeso)
            lista.AddRange(new[] { "Vincular ao Catálogo", "Origem" });
        lista.AddRange(new[] { "«»", "Adicionar Arma", "Nome", "Tipo de Dano", "Alcance", "Dados", "Dano", "Crítico", "Rank" });
        if (comPeso)
            lista.Add("Peso");
        lista.AddRange(new[] { "Durabilidade", "Equipada", "Espada Longa", "Cortante", "1", "1d8", "3", "19-20", "C" });
        if (comPeso)
            lista.Add($"{1.5m}");
        lista.AddRange(new[] { "«8»", "/ 10", "Equipada", "Desequipar", "Remover" });
        return lista;
    }

    protected static IEnumerable<string> Maestrias(string tipo) => new[]
    {
        "Maestrias", "«»", "Nome", "Escolha uma Perícia", "Perícia", "Escolha um Atributo", "Atributo", "«0»", "Gasto Maestria", "Adicionar Maestria",
    }.Concat(tipo == "Personagem"
        ? new[] { "Nome", "Perícia", "Atributo", "Gasto Maestria", "Total", "«Golpe Firme»", "Atletismo", "Vigor", "«1»", "4", "Remover" }
        : new[] { "Golpe Firme — Atletismo / Vigor — Gasto 1 — Total 4", "Remover" });

    protected static readonly string[] Armaduras =
    {
        "Armaduras", "Slot", "Armadura", "Categoria", "Defesa", "RF", "RM", "Peso", "Durabilidade",
        "Capacete", "—", "—", "—", "—", "—", "—", "«»", "Atribuir",
        "Superior", "Cota de Malha", "Media", "3", "2", "1", "8", "«5»", "/ 6", "Remover",
        "Inferior", "—", "—", "—", "—", "—", "—", "«»", "Atribuir",
    };

    protected static readonly string[] Escudos =
    {
        "Escudos", "«»", "Adicionar Escudo", "Nome", "Categoria", "Bônus de Defesa", "Peso", "Durabilidade", "Equipado",
        "Broquel", "Leve", "1", "2", "«4»", "/ 4", "Equipar", "Remover",
    };

    protected static IEnumerable<string> LeituraDaAba(string tipo) =>
        Recursos(tipo).Concat(SubAtributos(tipo)).Concat(ModificadorDeDano).Concat(Armas(tipo)).Concat(Maestrias(tipo)).Concat(Armaduras).Concat(Escudos);

    // ---------------- requisições ----------------

    /// <summary>
    /// <paramref name="antes"/> (a busca no catálogo), a gravação, a recarga das abas na ordem e, quando a página
    /// sobe a versão das penalidades, o alerta delas.
    /// </summary>
    protected async Task AfirmarGravacaoAsync(IRenderedComponent<IComponent> cut, string tipo, string gravacao, bool recarregaPenalidades, params string[] antes)
    {
        var esperado = new List<string>(antes) { gravacao };
        esperado.AddRange(RecargaDasAbas(tipo));
        if (recarregaPenalidades)
            esperado.Add($"GET /{Prefixo(tipo)}/s1/equipment-penalties");
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(esperado));
    }

    /// <summary>A busca do catálogo por tipo de item: o GM busca no catálogo dele, o jogador no da campanha.</summary>
    protected static string BuscaDoCatalogo(string papel, string tipoDeItem) => papel == "GM"
        ? $"GET /items?nome=&tipo={tipoDeItem}"
        : $"GET /campaigns/c1/available-items?nome=&tipo={tipoDeItem}";

    // Os seletores de catálogo da aba, na ordem do DOM: a arma, os dois slots de armadura vazios (Capacete, Inferior), o escudo.
    protected const int SeletorDaArma = 0, SeletorDoCapacete = 1, SeletorDoEscudo = 3;

    /// <summary>Busca (texto vazio) e escolhe um item no seletor de catálogo número <paramref name="indice"/> da aba.</summary>
    protected static async Task EscolherNoCatalogoAsync(IRenderedComponent<IComponent> cut, int indice, string itemId, string nome)
    {
        var seletor = cut.FindComponents<EntityPicker>()[indice];
        await cut.InvokeAsync(() => seletor.Instance.SearchAsyncForTests(""));
        await cut.InvokeAsync(() => seletor.Instance.SelectForTests(new PickerOption(itemId, nome)));
    }

    protected static IElement FormularioDe(IRenderedComponent<IComponent> cut, string botao) => Botao(AbaAberta(cut), botao).Closest("form")!;

    /// <summary>A tabela da aba que tem a coluna <paramref name="cabecalho"/> (tem de ser uma só).</summary>
    protected static IElement TabelaCom(IRenderedComponent<IComponent> cut, string cabecalho) =>
        AbaAberta(cut).QuerySelectorAll("table").Single(t => t.QuerySelectorAll("th").Any(th => TextoNormalizado(th) == cabecalho));

    protected static IElement LinhaDaArma(IRenderedComponent<IComponent> cut) => Linha(TabelaCom(cut, "Tipo de Dano"), "Espada Longa");

    protected static IElement LinhaDoSlot(IRenderedComponent<IComponent> cut, string slot) => Linha(Tabela(AbaAberta(cut), "Slot"), slot);

    protected static IElement LinhaDoEscudo(IRenderedComponent<IComponent> cut) =>
        TabelaCom(cut, "Bônus de Defesa").QuerySelectorAll("tbody tr").Single();

    /// <summary>Abre o MudSelect de rótulo <paramref name="rotulo"/> dentro de <paramref name="raiz"/> e clica na opção.</summary>
    protected static async Task EscolherAsync(IRenderedComponent<IComponent> cut, Func<IElement> raiz, string rotulo, string opcao)
    {
        var label = raiz().QuerySelectorAll("label.mud-input-label").Single(l => TextoNormalizado(l) == rotulo);
        await cut.InvokeAsync(() => label.Closest(".mud-input-control")!.MouseDown());
        var item = cut.WaitForElements(".mud-popover-open .mud-list-item").Single(li => TextoNormalizado(li) == opcao);
        await cut.InvokeAsync(() => item.Click());
    }

    /// <summary>O que se lê e os controles que mudam com o papel.</summary>
    public sealed class Leituras : RedeCombateTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task A_aba_mostra_recursos_sub_atributos_e_equipamento_nesta_ordem(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            Leitura(AbaAberta(cut)).Should().Equal(LeituraDaAba(tipo));
        }

        // A Leitura não vê ícones nem atributos: o "+" de cadastrar item no catálogo (só NPC e Criatura, só o GM;
        // na arma, nos dois slots vazios e no escudo) e os campos desabilitados (nenhum) ficam afirmados aqui.
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task O_mais_de_cadastrar_item_aparece_so_para_o_gm_de_npc_e_criatura_e_os_seletores_sao_os_de_hoje(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            AbaAberta(cut).QuerySelectorAll("button[title='Cadastrar novo item']").Should().HaveCount(tipo != "Personagem" && papel == "GM" ? 4 : 0);
            AbaAberta(cut).QuerySelectorAll("input[disabled]").Should().BeEmpty();
            AbaAberta(cut).QuerySelectorAll("button[disabled]").Should().BeEmpty();
            // No NPC e na Criatura o CatalogoItemPicker passa ao EntityPicker o texto literal "Placeholder" (hoje é o que se vê).
            AbaAberta(cut).QuerySelectorAll("input[placeholder]").Select(i => i.GetAttribute("placeholder"))
                .Should().Equal(tipo == "Personagem"
                    ? new[] { "Escolha uma Afinidade", "Buscar arma...", "Buscar armadura...", "Buscar armadura...", "Buscar escudo..." }
                    : (tipo == "NPC" ? new[] { "Escolha uma Afinidade" } : Array.Empty<string>())
                        .Concat(new[] { "Placeholder", "Placeholder", "Placeholder", "Placeholder" }));
        }

        // Ataque natural (Ficha de Criaturas 3.a): sem item do catálogo, sem durabilidade editável.
        [Theory]
        [InlineData("GM")]
        [InlineData("Jogador")]
        public async Task A_arma_natural_da_criatura_mostra_ataque_natural_na_durabilidade(string papel)
        {
            Responder("/creature-sheets/s1/weapons", new[]
            {
                new
                {
                    id = "w2", itemId = (string?)null, nome = "Mordida", tipoDeDano = "Perfurante", alcance = (int?)null, dados = "2d6", dano = 4,
                    critico = (string?)null, rank = (string?)null, isEquipped = false, durabilidadeAtual = (int?)null, durabilidadeMaximo = (int?)null, descricao = (string?)null,
                },
            });
            var cut = await AbrirAbaAsync("Criatura", papel);

            Leitura(Linha(TabelaCom(cut, "Tipo de Dano"), "Mordida"))
                .Should().Equal("Mordida", "Perfurante", "—", "2d6", "4", "—", "—", "Ataque natural", "Equipar", "Remover");
        }
    }

    /// <summary>Armas: buscar e adicionar, remover, equipar, durabilidade.</summary>
    public sealed class ArmasDaFicha : RedeCombateTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Adicionar_uma_arma_do_catalogo_busca_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherNoCatalogoAsync(cut, SeletorDaArma, "i9", "Machado");
            await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Arma").Submit());

            var corpo = tipo == "Criatura"
                ? "{\"itemId\":\"i9\",\"manualDados\":null,\"manualDano\":null,\"manualNome\":null,\"manualTipoDeDano\":null}"
                : "{\"itemId\":\"i9\"}";
            await AfirmarGravacaoAsync(cut, tipo, $"POST /{Prefixo(tipo)}/s1/weapons {corpo}", recarregaPenalidades: true, BuscaDoCatalogo(papel, "Arma"));
        }

        [Theory]
        [InlineData("GM")]
        [InlineData("Jogador")]
        public async Task Na_criatura_um_ataque_natural_grava_os_campos_manuais(string papel)
        {
            var cut = await AbrirAbaAsync("Criatura", papel);

            await EscolherAsync(cut, () => FormularioDe(cut, "Adicionar Arma"), "Origem", "Preencher manualmente (ataque natural)");
            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Arma"), "Nome").Change("Mordida"));
            await EscolherAsync(cut, () => FormularioDe(cut, "Adicionar Arma"), "Tipo de Dano", "Perfurante");
            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Arma"), "Dados").Change("2d6"));
            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Arma"), "Dano").Change("4"));
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Arma").Submit());

            await AfirmarGravacaoAsync(cut, "Criatura",
                "POST /creature-sheets/s1/weapons {\"itemId\":null,\"manualDados\":\"2d6\",\"manualDano\":4,\"manualNome\":\"Mordida\",\"manualTipoDeDano\":\"Perfurante\"}",
                recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Remover_uma_arma_apaga_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDaArma(cut), "Remover").Click());

            await AfirmarGravacaoAsync(cut, tipo, $"DELETE /{Prefixo(tipo)}/s1/weapons/w1", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Desequipar_uma_arma_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDaArma(cut), "Desequipar").Click());

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/weapons/w1 false", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task A_durabilidade_da_arma_grava_pelo_endpoint_dela_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDaArma(cut).QuerySelector("input")!.Change("7"));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/weapons/w1/durabilidade 7", recarregaPenalidades: false);
        }
    }

    /// <summary>Armaduras e Escudos: atribuir e tirar do slot, adicionar, remover, equipar, durabilidade.</summary>
    public sealed class ArmadurasEEscudos : RedeCombateTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Atribuir_uma_armadura_ao_slot_busca_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherNoCatalogoAsync(cut, SeletorDoCapacete, "a9", "Elmo");
            await cut.InvokeAsync(() => Botao(LinhaDoSlot(cut, "Capacete"), "Atribuir").Click());

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/armor-slots/Capacete {{\"itemId\":\"a9\"}}", recarregaPenalidades: true,
                BuscaDoCatalogo(papel, "Armadura"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Tirar_a_armadura_do_slot_grava_o_slot_vazio_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDoSlot(cut, "Superior"), "Remover").Click());

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/armor-slots/Superior {{\"itemId\":null}}", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task A_durabilidade_da_armadura_grava_pelo_endpoint_dela_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDoSlot(cut, "Superior").QuerySelector("input")!.Change("4"));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/armor-slots/Superior/durabilidade 4", recarregaPenalidades: false);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Adicionar_um_escudo_do_catalogo_busca_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherNoCatalogoAsync(cut, SeletorDoEscudo, "i8", "Pavês");
            await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Escudo").Submit());

            await AfirmarGravacaoAsync(cut, tipo, $"POST /{Prefixo(tipo)}/s1/shields {{\"itemId\":\"i8\"}}", recarregaPenalidades: true, BuscaDoCatalogo(papel, "Escudo"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Remover_um_escudo_apaga_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDoEscudo(cut), "Remover").Click());

            await AfirmarGravacaoAsync(cut, tipo, $"DELETE /{Prefixo(tipo)}/s1/shields/sh1", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Equipar_um_escudo_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDoEscudo(cut), "Equipar").Click());

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/shields/sh1 true", recarregaPenalidades: true);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task A_durabilidade_do_escudo_grava_pelo_endpoint_dela_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDoEscudo(cut).QuerySelector("input")!.Change("3"));

            await AfirmarGravacaoAsync(cut, tipo, $"PUT /{Prefixo(tipo)}/s1/shields/sh1/durabilidade 3", recarregaPenalidades: false);
        }
    }
}
