using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;

/// <summary>
/// Rede de segurança do E: as abas de cada página (títulos e ordem), Antecedentes (Personagem e NPC: Estrela,
/// Histórico, História com a galeria) e Diário (só Personagem: adicionar, editar, cancelar, excluir), como GM e
/// como jogador — o que se lê e as requisições de cada ação. A data das entradas do Diário é formatada com a
/// cultura corrente: os testes fixam pt-BR. Fixa o que a tela faz hoje; precisa passar sem mudanças depois de
/// cada passo do E. As classes aninhadas existem só para o xUnit rodá-las em paralelo.
/// </summary>
public abstract class RedeAntecedentesEDiarioTests : RedeDeSegurancaBase
{
    public static TheoryData<string, string> TiposEPapeis => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
        { "Criatura", "GM" }, { "Criatura", "Jogador" },
    };

    public static TheoryData<string, string> TiposComAntecedentes => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
    };

    public static TheoryData<string> Papeis => new() { "GM", "Jogador" };

    protected static string K(string tipo) => $"/{Prefixo(tipo)}/s1";

    protected static string Upload(string papel) => papel == "GM" ? "POST /images [file]" : "POST /images [file, campaignId=c1]";

    protected async Task<IRenderedComponent<IComponent>> AbrirAntecedentesAsync(string tipo, string papel)
    {
        var cut = await RenderFichaAsync(tipo, papel);
        LimparRequisicoes();
        await IrParaAbaAsync(cut, "Antecedentes");
        // Cada campo carrega o seu: as Estrelas do Livro de Regras, as Perícias e os Históricos, a galeria da História.
        await EsperarRequisicoesAsync(cut, todas => todas.Should().BeEquivalentTo(
            "GET /rulebook/estrelas-alkerianas", "GET /pericias", "GET /historicos", $"GET {K(tipo)}/historia/imagens"));
        LimparRequisicoes();
        return cut;
    }

    protected async Task<IRenderedComponent<IComponent>> AbrirDiarioAsync(string papel)
    {
        var cut = await RenderFichaAsync("Personagem", papel);
        LimparRequisicoes();
        await IrParaAbaAsync(cut, "Diário");
        await EsperarCalmariaAsync();
        Requisicoes.Should().BeEmpty();
        return cut;
    }

    /// <summary>A gravação da ficha inteira (autosave) e a recarga dela (vale o conjunto, como na rede do cabeçalho).</summary>
    protected async Task AfirmarSalvamentoAsync(IRenderedComponent<IComponent> cut, string tipo, string corpo)
    {
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.FirstOrDefault().Should().Be($"PUT {K(tipo)} {corpo}");
            todas.Skip(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar(tipo));
        });
    }

    protected static string Nome(string tipo) => tipo == "Personagem" ? "Aria" : "Borin";

    /// <summary>Títulos e ordem das abas de cada página.</summary>
    public sealed class Abas : RedeAntecedentesEDiarioTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Cada_pagina_tem_estas_abas_nesta_ordem(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);

            var esperadas = new List<string> { "Informações Básicas", "Atributos & Perícias", "Combate", "Magias & Habilidades", "Posses" };
            if (tipo != "Criatura")
                esperadas.Add("Antecedentes");
            if (tipo == "Personagem")
                esperadas.Add("Diário");
            cut.FindAll("div.mud-tab").Select(TextoNormalizado).Should().Equal(esperadas);
        }
    }

    /// <summary>Antecedentes (Personagem e NPC): o que se lê e o que cada campo grava.</summary>
    public sealed class Antecedentes : RedeAntecedentesEDiarioTests
    {
        [Theory]
        [MemberData(nameof(TiposComAntecedentes))]
        public async Task A_aba_mostra_estrela_historico_e_historia_nesta_ordem(string tipo, string papel)
        {
            var cut = await AbrirAntecedentesAsync(tipo, papel);

            Leitura(AbaAberta(cut)).Should().Equal(
                "Estrela e Histórico", "«»", "Estrela", "«»", "Histórico",
                "História do personagem", "Galeria de imagens", "Enviar nova imagem", "Escolher imagens já enviadas");
            AbaAberta(cut).QuerySelectorAll("input[placeholder]").Select(i => i.GetAttribute("placeholder")).Should().Equal("Escolha uma Estrela", "Escolha um Histórico");
            AbaAberta(cut).QuerySelectorAll("input[disabled]").Should().BeEmpty();
            AbaAberta(cut).QuerySelectorAll("button[disabled]").Should().BeEmpty();
        }

        // 2.1.0: um /api/rulebook inválido não derruba a aba: a mensagem vai ao alerta da página e os campos continuam.
        [Theory]
        [MemberData(nameof(TiposComAntecedentes))]
        public async Task Estrela_com_resposta_invalida_mostra_erro_e_a_aba_continua(string tipo, string papel)
        {
            Responder("/rulebook/estrelas-alkerianas", "{ isto não é um documento");
            var cut = await RenderFichaAsync(tipo, papel);

            await IrParaAbaAsync(cut, "Antecedentes");

            cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Select(TextoNormalizado)
                .Should().Equal("Não foi possível carregar as Estrelas do Livro de Regras."));
            AbaAberta(cut).QuerySelectorAll("input[placeholder]").Select(i => i.GetAttribute("placeholder"))
                .Should().Equal("Escolha uma Estrela", "Escolha um Histórico");
        }

        // O editor de texto só lê o HTML ao ser criado: ele começa com a História que veio na ficha.
        [Theory]
        [MemberData(nameof(TiposComAntecedentes))]
        public async Task O_editor_da_historia_comeca_com_a_historia_da_ficha(string tipo, string papel)
        {
            var cut = await AbrirAntecedentesAsync(tipo, papel);

            cut.WaitForAssertion(() => JSInterop.Invocations["ruinaRichText.create"].Should().ContainSingle());
            JSInterop.Invocations["ruinaRichText.create"].Single().Arguments[2]
                .Should().Be(tipo == "Personagem" ? "Nasceu no porto." : "Ferreiro do vale.");
        }

        // As opções vêm do Livro de Regras (rótulo com o subtítulo do cartão) e do catálogo de Históricos.
        [Theory]
        [MemberData(nameof(TiposComAntecedentes))]
        public async Task As_opcoes_de_estrela_e_historico_sao_as_do_livro_e_do_catalogo(string tipo, string papel)
        {
            var cut = await AbrirAntecedentesAsync(tipo, papel);

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Estrela").Closest(".mud-input-control")!.MouseDown());
            cut.WaitForElements(".mud-popover-open .mud-list-item").Select(TextoNormalizado).Should().Equal(
                "I — AEURER — Estrela da Curiosidade", "Liora", "Vaelen", "Theron", "Maetus", "Almora", "Elvani", "Sareli", "Gaelion", "Sadir");
            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Histórico").Closest(".mud-input-control")!.MouseDown());
            cut.WaitForAssertion(() => cut.FindAll(".mud-popover-open .mud-list-item").Select(TextoNormalizado).Should().Contain("Ferreiro"));
            Requisicoes.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(TiposComAntecedentes))]
        public async Task Escolher_a_estrela_salva_a_ficha(string tipo, string papel)
        {
            var cut = await AbrirAntecedentesAsync(tipo, papel);

            await EscolherOpcaoAsync(cut, () => AbaAberta(cut), "Estrela", "I — AEURER — Estrela da Curiosidade");

            await AfirmarSalvamentoAsync(cut, tipo, RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: Nome(tipo)).Replace("\"estrela\":null", "\"estrela\":\"Aeurer\""));
        }

        [Theory]
        [MemberData(nameof(TiposComAntecedentes))]
        public async Task Escolher_o_historico_salva_a_ficha(string tipo, string papel)
        {
            var cut = await AbrirAntecedentesAsync(tipo, papel);

            await EscolherOpcaoAsync(cut, () => AbaAberta(cut), "Histórico", "Ferreiro");

            await AfirmarSalvamentoAsync(cut, tipo, RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha(tipo, nome: Nome(tipo)).Replace("\"historicoId\":null", "\"historicoId\":\"h1\""));
        }

        // A História tem endpoint próprio e autosave próprio: nada da ficha é regravado nem relido.
        [Theory]
        [MemberData(nameof(TiposComAntecedentes))]
        public async Task Editar_a_historia_grava_so_a_historia(string tipo, string papel)
        {
            var cut = await AbrirAntecedentesAsync(tipo, papel);

            var editor = cut.FindComponents<RichTextEditor>().Single();
            await cut.InvokeAsync(() => editor.Instance.OnEditorChanged("<p>Partiu do porto.</p>"));

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"PUT {K(tipo)}/historia {{\"historia\":\"<p>Partiu do porto.</p>\"}}"));
        }

        // A imagem da galeria sobe na campanha da ficha quando é o jogador, e a galeria grava pelo endpoint dela.
        [Theory]
        [MemberData(nameof(TiposComAntecedentes))]
        public async Task Enviar_uma_imagem_para_a_galeria_sobe_o_arquivo_e_grava_a_galeria(string tipo, string papel)
        {
            var cut = await AbrirAntecedentesAsync(tipo, papel);

            var anexo = cut.FindComponents<ImageAttachmentField>().Single();
            await cut.InvokeAsync(() => anexo.Instance.UploadForTests(new FakeBrowserFile("mapa.png")));

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(Upload(papel), $"PUT {K(tipo)}/historia/imagens {{\"imageIds\":[\"img-nova\"]}}"));
        }
    }

    /// <summary>Diário (só Personagem): o que se lê, adicionar, editar, cancelar, excluir.</summary>
    public sealed class Diario : RedeAntecedentesEDiarioTests
    {
        private static IElement FormularioDaEntrada(IRenderedComponent<IComponent> cut) =>
            AbaAberta(cut).QuerySelectorAll("button").Single(b => TextoNormalizado(b) == "Adicionar Entrada" && b.GetAttribute("type") == "submit").Closest("form")!;

        private static IElement CartaoDaEntrada(IRenderedComponent<IComponent> cut) => AbaAberta(cut).QuerySelectorAll(".mud-card").Single();

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task A_aba_mostra_o_formulario_e_as_entradas(string papel)
        {
            FixarCulturaPtBr();
            var cut = await AbrirDiarioAsync(papel);

            Leitura(AbaAberta(cut)).Should().Equal(
                "Adicionar Entrada", "«»", "Texto", "Anexar imagens", "Enviar nova imagem", "Escolher imagens já enviadas", "Adicionar Entrada",
                "Entradas", "01/10/2026 12:00", "Chegamos à vila.", "Editar", "Excluir");
            AbaAberta(cut).QuerySelectorAll("input[disabled], textarea[disabled]").Should().BeEmpty();
            AbaAberta(cut).QuerySelectorAll("button[disabled]").Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Uma_entrada_com_imagens_mostra_as_miniaturas(string papel)
        {
            Responder("/character-sheets/s1/diary", new[] { new { id = "d1", texto = "Chegamos à vila.", createdAt = "2026-10-01T12:00:00Z", imageUrls = new[] { "/img/dois.png" } } });
            var cut = await AbrirDiarioAsync(papel);

            CartaoDaEntrada(cut).QuerySelectorAll("img").Select(i => i.GetAttribute("src")).Should().Equal("/img/dois.png");
        }

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Adicionar_uma_entrada_grava_e_rele_o_diario(string papel)
        {
            var cut = await AbrirDiarioAsync(papel);

            await cut.InvokeAsync(() => FormularioDaEntrada(cut).QuerySelector("textarea")!.Change("Encontramos o mapa."));
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => FormularioDaEntrada(cut).Submit());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                "POST /character-sheets/s1/diary {\"imageIds\":[],\"texto\":\"Encontramos o mapa.\"}", "GET /character-sheets/s1/diary"));
        }

        // A imagem sobe na campanha da ficha quando é o jogador e entra na entrada nova.
        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Adicionar_uma_entrada_com_imagem_sobe_o_arquivo_e_grava_a_entrada_com_ela(string papel)
        {
            var cut = await AbrirDiarioAsync(papel);

            await cut.InvokeAsync(() => FormularioDaEntrada(cut).QuerySelector("textarea")!.Change("Encontramos o mapa."));
            var anexo = cut.FindComponents<ImageAttachmentField>().Single();
            await cut.InvokeAsync(() => anexo.Instance.UploadForTests(new FakeBrowserFile("mapa.png")));
            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(Upload(papel)));
            LimparRequisicoes();
            await cut.InvokeAsync(() => FormularioDaEntrada(cut).Submit());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                "POST /character-sheets/s1/diary {\"imageIds\":[\"img-nova\"],\"texto\":\"Encontramos o mapa.\"}", "GET /character-sheets/s1/diary"));
        }

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Editar_mostra_o_texto_e_salvar_grava_e_rele_o_diario(string papel)
        {
            var cut = await AbrirDiarioAsync(papel);

            await cut.InvokeAsync(() => Botao(CartaoDaEntrada(cut), "Editar").Click());
            Leitura(CartaoDaEntrada(cut)).Should().Equal("«Chegamos à vila.»", "Enviar nova imagem", "Escolher imagens já enviadas", "Salvar", "Cancelar");
            await cut.InvokeAsync(() => CartaoDaEntrada(cut).QuerySelector("textarea")!.Change("Chegamos à vila ao anoitecer."));
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => Botao(CartaoDaEntrada(cut), "Salvar").Click());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                "PUT /character-sheets/s1/diary/d1 {\"imageIds\":[],\"texto\":\"Chegamos à vila ao anoitecer.\"}", "GET /character-sheets/s1/diary"));
            Botao(CartaoDaEntrada(cut), "Editar").Should().NotBeNull();
        }

        // Editar começa com as imagens da entrada que estão na galeria do papel (por URL).
        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Editar_uma_entrada_com_imagem_regrava_a_imagem(string papel)
        {
            Responder("/character-sheets/s1/diary", new[] { new { id = "d1", texto = "Chegamos à vila.", createdAt = "2026-10-01T12:00:00Z", imageUrls = new[] { "/img/dois.png" } } });
            var cut = await AbrirDiarioAsync(papel);

            await cut.InvokeAsync(() => Botao(CartaoDaEntrada(cut), "Editar").Click());
            await cut.InvokeAsync(() => Botao(CartaoDaEntrada(cut), "Salvar").Click());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(
                "PUT /character-sheets/s1/diary/d1 {\"imageIds\":[\"img2\"],\"texto\":\"Chegamos à vila.\"}", "GET /character-sheets/s1/diary"));
        }

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Cancelar_a_edicao_nao_grava_e_volta_ao_texto(string papel)
        {
            FixarCulturaPtBr();
            var cut = await AbrirDiarioAsync(papel);

            await cut.InvokeAsync(() => Botao(CartaoDaEntrada(cut), "Editar").Click());
            await cut.InvokeAsync(() => CartaoDaEntrada(cut).QuerySelector("textarea")!.Change("Outra coisa."));
            await cut.InvokeAsync(() => Botao(CartaoDaEntrada(cut), "Cancelar").Click());

            Leitura(CartaoDaEntrada(cut)).Should().Equal("01/10/2026 12:00", "Chegamos à vila.", "Editar", "Excluir");
            await EsperarCalmariaAsync();
            Requisicoes.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(Papeis))]
        public async Task Excluir_apaga_e_rele_o_diario(string papel)
        {
            var cut = await AbrirDiarioAsync(papel);

            await cut.InvokeAsync(() => Botao(CartaoDaEntrada(cut), "Excluir").Click());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal("DELETE /character-sheets/s1/diary/d1", "GET /character-sheets/s1/diary"));
        }
    }
}
