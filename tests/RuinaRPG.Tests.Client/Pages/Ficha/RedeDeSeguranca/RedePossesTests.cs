using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;

/// <summary>
/// Rede de segurança do E: a aba Posses das três fichas, como GM e como jogador — o que se lê (Equipamento
/// Inicial, Inventário com Peso e Ciclos, Espólios na Criatura, Artefatos, Afeições na forma de cada tipo), os
/// controles que mudam com o papel, e as requisições de cada ação (Equipamento Inicial pelo diálogo, Ciclos,
/// item do Inventário, Artefato e Afeição: adicionar, quantidade/edição, remover) com a recarga que vem depois.
/// Os Espólios (só Criatura) estão em <see cref="RedePartesPorTipoTests"/>. Os números são formatados com a
/// cultura corrente: os testes fixam pt-BR. Fixa o que a tela faz hoje; precisa passar sem mudanças depois de
/// cada passo do E. As classes aninhadas existem só para o xUnit rodá-las em paralelo.
/// </summary>
public abstract class RedePossesTests : RedeDeSegurancaBase
{
    protected const string Aba = "Posses";

    public static TheoryData<string, string> TiposEPapeis => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
        { "Criatura", "GM" }, { "Criatura", "Jogador" },
    };

    public static TheoryData<string, string> TiposComInventario => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
    };

    protected static string K(string tipo) => $"/{Prefixo(tipo)}/s1";

    /// <summary>Abrir a aba não pede nada: tudo o que ela mostra veio na carga da página.</summary>
    protected async Task<IRenderedComponent<IComponent>> AbrirAbaAsync(string tipo, string papel)
    {
        var cut = await RenderFichaAsync(tipo, papel);
        LimparRequisicoes();
        await IrParaAbaAsync(cut, Aba);
        await EsperarCalmariaAsync();
        Requisicoes.Should().BeEmpty();
        return cut;
    }

    // ---------------- o que se lê ----------------

    protected static string[] Inventario(string tipo) => new[]
    {
        "Escolher Equipamento Inicial", "Inventário", "Peso: 12,5 / 40", tipo == "Personagem" ? "«4»" : "«2»", "Ciclos",
        "«»", "«1»", "Quantidade", "Adicionar Item",
        "Nome", "Peso", "Qtd", "Total", "Corda", "1", "«2»", "2", "Remover",
    };

    protected static readonly string[] Espolios =
    {
        "Espólios", "«»", "«1»", "Quantidade", "«0»", "DT", "Adicionar Espólio",
        "Nome", "Custo", "Qtd", "Total", "DT", "Presa de Lobo", "5", "«2»", "10", "12", "Remover",
    };

    protected static readonly string[] Artefatos =
    {
        "Artefatos", "«»", "Adicionar Artefato", "Atributo", "Amuleto", "— Alvo Vigor — Valor 1", "Remover",
    };

    // Edição na linha só no Personagem (PUT affections/{id} só existe para ele).
    protected static string[] Afeicoes(string tipo) => new[]
    {
        "Afeições", "«»", "Nome", "«0»", "Favorabilidade", "Adicionar Afeição",
    }.Concat(tipo == "Personagem"
        ? new[] { "Nome", "Favorabilidade", "«Taverneiro»", "«2»", "Remover" }
        : new[] { "Taverneiro — Favorabilidade 2", "Remover" }).ToArray();

    protected static IEnumerable<string> LeituraDaAba(string tipo) =>
        (tipo == "Criatura" ? Espolios : Inventario(tipo)).Concat(Artefatos).Concat(Afeicoes(tipo));

    // ---------------- requisições ----------------

    protected static string BuscaDoCatalogo(string papel, string tipoDeItem) => papel == "GM"
        ? $"GET /items?nome=&tipo={tipoDeItem}"
        : $"GET /campaigns/c1/available-items?nome=&tipo={tipoDeItem}";

    // Os seletores de catálogo da aba, na ordem do DOM: Inventário (ou Espólio, na Criatura) e Artefato.
    protected const int SeletorDoInventario = 0, SeletorDoArtefato = 1;

    protected static IElement FormularioDe(IRenderedComponent<IComponent> cut, string botao) => Botao(AbaAberta(cut), botao).Closest("form")!;

    /// <summary>A tabela da aba que tem a coluna <paramref name="cabecalho"/> (tem de ser uma só).</summary>
    protected static IElement TabelaCom(IRenderedComponent<IComponent> cut, string cabecalho) =>
        AbaAberta(cut).QuerySelectorAll("table").Single(t => t.QuerySelectorAll("th").Any(th => TextoNormalizado(th) == cabecalho));

    protected static IElement LinhaDoItem(IRenderedComponent<IComponent> cut) => Linha(TabelaCom(cut, "Qtd"), "Corda");

    protected static IElement LinhaDoArtefato(IRenderedComponent<IComponent> cut) =>
        AbaAberta(cut).QuerySelectorAll("tr").Single(tr => TextoNormalizado(tr).StartsWith("Amuleto", StringComparison.Ordinal));

    protected static IElement LinhaDaAfeicao(IRenderedComponent<IComponent> cut) =>
        FormularioDe(cut, "Adicionar Afeição").ParentElement!.QuerySelectorAll("table tbody tr").Single();

    /// <summary>A escrita e, em seguida, as releituras na ordem.</summary>
    protected async Task AfirmarGravacaoAsync(IRenderedComponent<IComponent> cut, string gravacao, IEnumerable<string> recarga, params string[] antes)
    {
        var esperado = new List<string>(antes) { gravacao };
        esperado.AddRange(recarga);
        await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(esperado));
    }

    /// <summary>Mexer no Inventário relê o Inventário e as abas 2 e 3 (o Peso muda a Carga).</summary>
    protected static string[] RecargaDoInventario(string tipo) => new[] { $"GET {K(tipo)}/inventory" }.Concat(RecargaDasAbas(tipo)).ToArray();

    /// <summary>Mexer num Artefato relê os Artefatos e as abas 2 e 3 (o Artefato muda Atributos e Sub-Atributos).</summary>
    protected static string[] RecargaDosArtefatos(string tipo) => new[] { $"GET {K(tipo)}/artifacts" }.Concat(RecargaDasAbas(tipo)).ToArray();

    /// <summary>O que se lê e os controles que mudam com o papel.</summary>
    public sealed class Leituras : RedePossesTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task A_aba_mostra_inventario_ou_espolios_artefatos_e_afeicoes_nesta_ordem(string tipo, string papel)
        {
            FixarCulturaPtBr();
            var cut = await AbrirAbaAsync(tipo, papel);

            cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Equal(LeituraDaAba(tipo)), TimeSpan.FromSeconds(5));
        }

        // A Leitura não vê atributos nem ícones: o "+" de cadastrar item (só NPC e Criatura, só o GM, nos dois
        // seletores), os textos de busca e os campos desabilitados (nenhum) ficam afirmados aqui.
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task O_mais_de_cadastrar_item_e_os_seletores_sao_os_de_hoje(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            AbaAberta(cut).QuerySelectorAll("button[title='Cadastrar novo item']").Should().HaveCount(tipo != "Personagem" && papel == "GM" ? 2 : 0);
            AbaAberta(cut).QuerySelectorAll("input[disabled]").Should().BeEmpty();
            AbaAberta(cut).QuerySelectorAll("button[disabled]").Should().BeEmpty();
            // O CatalogoItemPicker do NPC e da Criatura repassa o texto do campo, como o seletor do Personagem (2.1.0: era o literal "Placeholder").
            AbaAberta(cut).QuerySelectorAll("input[placeholder]").Select(i => i.GetAttribute("placeholder"))
                .Should().Equal(new[] { "Buscar item...", "Buscar artefato..." });
        }

        // Acima da carga: o aviso de Sobrecarregado. O bônus de carga da campanha só aparece no Personagem (o único
        // tipo cujo servidor o manda diferente de zero).
        [Theory]
        [InlineData("Personagem")]
        [InlineData("NPC")]
        public async Task Acima_da_carga_avisa_sobrecarregado(string tipo)
        {
            FixarCulturaPtBr();
            Responder("/sub-attributes", new
            {
                iniciativa = 5, movimentacao = 9, esquivaNatural = 11, defesaNatural = 13, reducaoFisica = 2, reducaoMagica = 1,
                pesoAtual = 45.25m, pesoMaximo = 40m, eficienciaElemental = 3, danoElemental = 2, bonusDeCargaDaCampanha = tipo == "Personagem" ? 5m : 0m,
            });
            var cut = await AbrirAbaAsync(tipo, "GM");

            Leitura(AbaAberta(cut)).Take(5).Should().Equal(tipo == "Personagem"
                ? new[] { "Escolher Equipamento Inicial", "Inventário", "Peso: 45,25 / 40", "inclui +5 da campanha", "Sobrecarregado — reduz Movimentação." }
                : new[] { "Escolher Equipamento Inicial", "Inventário", "Peso: 45,25 / 40", "Sobrecarregado — reduz Movimentação.", "«2»" });
        }

        // Com o Equipamento Inicial já escolhido, o botão some.
        [Theory]
        [InlineData("Personagem")]
        [InlineData("NPC")]
        public async Task Com_o_equipamento_inicial_escolhido_o_botao_some(string tipo)
        {
            FixarCulturaPtBr();
            var ficha = tipo == "Personagem" ? FichaComKit.Personagem : FichaComKit.Npc;
            Responder($"^{K(tipo)}$", ficha);
            var cut = await AbrirAbaAsync(tipo, "GM");

            Leitura(AbaAberta(cut)).Take(2).Should().Equal("Inventário", "Peso: 12,5 / 40");
            AbaAberta(cut).QuerySelectorAll("button").Select(TextoNormalizado).Should().NotContain("Escolher Equipamento Inicial");
        }
    }

    /// <summary>Equipamento Inicial (Personagem e NPC): o diálogo pede os kits, e escolher grava e relê a ficha e a aba.</summary>
    public sealed class EquipamentoInicial : RedePossesTests
    {
        private static readonly object Kits = new[]
        {
            new
            {
                id = "k1", nome = "Kit do Aventureiro", descricao = "Mochila e corda.", ciclos = 2,
                choiceSlots = new[] { new { slotId = "sl1", label = "Arma", options = new[] { new { itemId = "i9", nome = "Machado" } } } },
            },
        };

        [Theory]
        [MemberData(nameof(TiposComInventario))]
        public async Task O_dialogo_pede_os_kits_da_ficha_e_mostra_o_kit_escolhido(string tipo, string papel)
        {
            Responder("/equipagem/kits", Kits);
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(AbaAberta(cut), "Escolher Equipamento Inicial").Click());
            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"GET {K(tipo)}/equipagem/kits"));
            await EscolherOpcaoAsync(cut, () => Dialogo(cut), "Kit", "Kit do Aventureiro");

            TextoNormalizado(Dialogo(cut).QuerySelector(".mud-dialog-title")!).Should().Be("Escolher Equipamento Inicial");
            Leitura(Dialogo(cut).QuerySelector(".mud-dialog-content")!).Should().Equal("Kit do Aventureiro", "Kit", "Mochila e corda.", "+2 Ciclos", "«»", "Arma");
            Botao(Dialogo(cut), "Escolher Equipagem").HasAttribute("disabled").Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(TiposComInventario))]
        public async Task Escolher_o_kit_grava_e_rele_a_ficha_e_a_aba(string tipo, string papel)
        {
            Responder("/equipagem/kits", Kits);
            var cut = await AbrirAbaAsync(tipo, papel);
            await cut.InvokeAsync(() => Botao(AbaAberta(cut), "Escolher Equipamento Inicial").Click());
            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"GET {K(tipo)}/equipagem/kits"));
            await EscolherOpcaoAsync(cut, () => Dialogo(cut), "Kit", "Kit do Aventureiro");
            await EscolherOpcaoAsync(cut, () => Dialogo(cut), "Arma", "Machado");
            LimparRequisicoes();

            await cut.InvokeAsync(() => Botao(Dialogo(cut), "Escolher Equipagem").Click());

            // A ficha primeiro; depois as abas 2 e 3, o Inventário e os Artefatos em paralelo (vale o conjunto).
            await EsperarRequisicoesAsync(cut, todas =>
            {
                todas.Take(2).Should().Equal(
                    $"POST {K(tipo)}/equipagem/choose {{\"choiceSelections\":[{{\"itemId\":\"i9\",\"slotId\":\"sl1\"}}],\"kitId\":\"k1\"}}",
                    $"GET {K(tipo)}");
                todas.Skip(2).Should().BeEquivalentTo(RecargaDasAbas(tipo).Concat(new[] { $"GET {K(tipo)}/inventory", $"GET {K(tipo)}/artifacts" }));
            });
            cut.FindAll(".mud-dialog").Should().BeEmpty();
        }
    }

    /// <summary>Ciclos e Inventário (Personagem e NPC).</summary>
    public sealed class InventarioDaFicha : RedePossesTests
    {
        [Theory]
        [MemberData(nameof(TiposComInventario))]
        public async Task Os_ciclos_salvam_a_ficha_inteira(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Ciclos").Change("7"));

            var nome = tipo == "Personagem" ? "Aria" : "Borin";
            var corpo = RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: nome).Replace(tipo == "Personagem" ? "\"ciclos\":4" : "\"ciclos\":2", "\"ciclos\":7");
            await EsperarRequisicoesAsync(cut, todas =>
            {
                todas.FirstOrDefault().Should().Be($"PUT {K(tipo)} {corpo}");
                todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo));
            });
        }

        [Theory]
        [MemberData(nameof(TiposComInventario))]
        public async Task Adicionar_um_item_busca_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherNoSeletorAsync(cut, SeletorDoInventario, "i7", "Tocha");
            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Item"), "Quantidade").Change("3"));
            Requisicoes.Should().Equal(BuscaDoCatalogo(papel, "ItemGeral"));
            await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Item").Submit());

            await AfirmarGravacaoAsync(cut, $"POST {K(tipo)}/inventory {{\"itemId\":\"i7\",\"qtd\":3}}", RecargaDoInventario(tipo), BuscaDoCatalogo(papel, "ItemGeral"));
        }

        [Theory]
        [MemberData(nameof(TiposComInventario))]
        public async Task A_quantidade_grava_pelo_endpoint_dela_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => LinhaDoItem(cut).QuerySelector("input")!.Change("5"));

            await AfirmarGravacaoAsync(cut, $"PUT {K(tipo)}/inventory/inv1/qtd 5", RecargaDoInventario(tipo));
        }

        [Theory]
        [MemberData(nameof(TiposComInventario))]
        public async Task Remover_um_item_apaga_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDoItem(cut), "Remover").Click());

            await AfirmarGravacaoAsync(cut, $"DELETE {K(tipo)}/inventory/inv1", RecargaDoInventario(tipo));
        }
    }

    /// <summary>Artefatos (os três tipos).</summary>
    public sealed class ArtefatosDaFicha : RedePossesTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Adicionar_um_artefato_busca_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherNoSeletorAsync(cut, SeletorDoArtefato, "i4b", "Anel");
            Requisicoes.Should().Equal(BuscaDoCatalogo(papel, "Artefato"));
            await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Artefato").Submit());

            await AfirmarGravacaoAsync(cut, $"POST {K(tipo)}/artifacts {{\"artifactItemId\":\"i4b\"}}", RecargaDosArtefatos(tipo), BuscaDoCatalogo(papel, "Artefato"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Remover_um_artefato_apaga_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDoArtefato(cut), "Remover").Click());

            await AfirmarGravacaoAsync(cut, $"DELETE {K(tipo)}/artifacts/art1", RecargaDosArtefatos(tipo));
        }
    }

    /// <summary>Afeições: adicionar e remover nos três; editar na linha só no Personagem.</summary>
    public sealed class AfeicoesDaFicha : RedePossesTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Adicionar_uma_afeicao_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Afeição"), "Nome").Change("Ferreira"));
            await cut.InvokeAsync(() => Campo(FormularioDe(cut, "Adicionar Afeição"), "Favorabilidade").Change("3"));
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => FormularioDe(cut, "Adicionar Afeição").Submit());

            await AfirmarGravacaoAsync(cut, $"POST {K(tipo)}/affections {{\"favorabilidade\":3,\"nome\":\"Ferreira\"}}", new[] { $"GET {K(tipo)}/affections" });
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Remover_uma_afeicao_apaga_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDaAfeicao(cut), "Remover").Click());

            await AfirmarGravacaoAsync(cut, $"DELETE {K(tipo)}/affections/afe1", new[] { $"GET {K(tipo)}/affections" });
        }

        [Theory]
        [InlineData("GM")]
        [InlineData("Jogador")]
        public async Task No_personagem_editar_o_nome_na_linha_grava_a_afeicao(string papel)
        {
            var cut = await AbrirAbaAsync("Personagem", papel);

            await cut.InvokeAsync(() => LinhaDaAfeicao(cut).QuerySelectorAll("input")[0].Change("Taverneira"));

            await AfirmarGravacaoAsync(cut, "PUT /character-sheets/s1/affections/afe1 {\"favorabilidade\":2,\"nome\":\"Taverneira\"}", new[] { "GET /character-sheets/s1/affections" });
        }

        [Theory]
        [InlineData("GM")]
        [InlineData("Jogador")]
        public async Task No_personagem_editar_a_favorabilidade_na_linha_grava_a_afeicao(string papel)
        {
            var cut = await AbrirAbaAsync("Personagem", papel);

            await cut.InvokeAsync(() => LinhaDaAfeicao(cut).QuerySelectorAll("input")[1].Change("-1"));

            await AfirmarGravacaoAsync(cut, "PUT /character-sheets/s1/affections/afe1 {\"favorabilidade\":-1,\"nome\":\"Taverneiro\"}", new[] { "GET /character-sheets/s1/affections" });
        }
    }

    /// <summary>A ficha do Personagem e a do NPC (do GM) com o Equipamento Inicial já escolhido (equipmentKitId preenchido).</summary>
    protected static class FichaComKit
    {
        public static readonly object Personagem = new
        {
            id = SheetId, campaignId = "c1", campaignName = "Crônicas de Alker", ownerId = "u2", imageUrl = "/img/um.png", imageId = "img1",
            nome = "Aria", linhagem = "Humano", variante = "Sinir", vocacao = "Feiticeiro", subVocacao = "Piromante", afinidade = 2, propriedade = "Uma casa",
            nivel = 3, circulo = 1, grau = 2, possuiCoracaoDeMana = true, experienciaAtual = 120, eapAtual = 7,
            nucleosRankF = 1, nucleosRankE = 2, nucleosRankD = 0, nucleosRankC = 0, nucleosRankB = 0, nucleosRankA = 0, nucleosRankS = 0,
            pontosDeIgnicaoAtual = 12, pontosDeIgnicaoTotal = 40, vitalidadeAtual = 20, focoAtual = 10, adrenalinaAtual = 3, estresseAtual = 1,
            cobertura = "Nenhuma", ciclos = 4, graduacao = 1, graduacaoLabel = "Círculo",
            vitalidadeMaximo = 25, focoMaximo = 12, adrenalinaMaximo = 5, estresseMaximo = 10,
            xpParaProximoNivel = 30, pontosDeIgnicaoBonusManual = 2, pontosDePericiaBonusCritico = 1,
            arcaRolada = (int?)null, estrela = (string?)null, sinaAtual = 1, historicoId = (string?)null, equipmentKitId = "k1", historia = "Nasceu no porto.",
            afinidadeAdicional = 0, vocacaoArcanaGasta = 2, vocacaoArcanaMaxima = 6,
        };

        public static readonly object Npc = new
        {
            id = SheetId, ownerId = (string?)null, campaignId = (string?)null, imageUrl = "/img/um.png", imageId = "img1",
            nome = "Borin", linhagem = "Nephrytes", variante = "Koroanos", vocacao = "Campeao", subVocacao = "Duelista", afinidade = 0, propriedade = "Uma forja",
            nivel = 4, circulo = 0, grau = 2, possuiCoracaoDeMana = false, experienciaAtual = 200, eapAtual = 8,
            nucleosRankF = 0, nucleosRankE = 1, nucleosRankD = 1, nucleosRankC = 0, nucleosRankB = 0, nucleosRankA = 0, nucleosRankS = 0,
            pontosDeIgnicaoAtual = 9, pontosDeIgnicaoTotal = 30, vitalidadeAtual = 30, focoAtual = 6, adrenalinaAtual = 4, estresseAtual = 2,
            cobertura = "Nenhuma", ciclos = 2, graduacao = 2, graduacaoLabel = "Grau",
            vitalidadeMaximo = 32, focoMaximo = 8, adrenalinaMaximo = 6, estresseMaximo = 10,
            arcaRolada = (int?)null, estrela = (string?)null, sinaAtual = 2, historicoId = (string?)null, equipmentKitId = "k1", historia = "Ferreiro do vale.",
            afinidadeAdicional = 0, vocacaoArcanaGasta = 0, vocacaoArcanaMaxima = 0, abate = 50, assistencia = 25,
        };
    }
}
