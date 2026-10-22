using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Shared;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;

/// <summary>
/// Rede de segurança do E: a aba Magias &amp; Habilidades das três fichas, como GM e como jogador — o que se lê
/// (Habilidade Racial, Magias e Habilidades, Habilidades Passivas, Runas na forma de hoje de cada tipo) e as
/// requisições de cada ação (adicionar do zero e do Banco, editar, remover; Passivas pelo endereço da ficha;
/// Runas do zero, do Banco, com imagem, remover; Arca do Humano) com a recarga que vem depois, na ordem. Fixa o
/// que a tela faz hoje; precisa passar sem mudanças depois de cada passo do E. As classes aninhadas existem só
/// para o xUnit rodá-las em paralelo.
/// </summary>
public abstract class RedeMagiasTests : RedeDeSegurancaBase
{
    protected const string Aba = "Magias & Habilidades";

    // O catálogo de Efeitos é do AddEfeitoForm: cada instância o pede ao aparecer, e de novo a cada vez que
    // recebe parâmetros antes de a primeira resposta chegar. Quantas vezes depende de quantas renderizações a
    // página faz, o que não é comportamento: a rede tira essa requisição das sequências e afirma à parte que ela
    // aconteceu quando um formulário de Efeitos aparece.
    protected const string CatalogoDeEfeitos = "GET /efeitos";

    public static TheoryData<string, string> TiposEPapeis => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
        { "Criatura", "GM" }, { "Criatura", "Jogador" },
    };

    public static TheoryData<string, string> TiposComRunas => new()
    {
        { "Personagem", "GM" }, { "Personagem", "Jogador" },
        { "NPC", "GM" }, { "NPC", "Jogador" },
    };

    protected static string K(string tipo) => $"/{Prefixo(tipo)}/s1";

    protected List<string> SemEfeitos(IEnumerable<string> todas) => todas.Where(r => r != CatalogoDeEfeitos).ToList();

    protected async Task<IRenderedComponent<IComponent>> AbrirAbaAsync(string tipo, string papel)
    {
        var cut = await RenderFichaAsync(tipo, papel);
        LimparRequisicoes();
        await IrParaAbaAsync(cut, Aba);
        // A seção de Passivas lê as entradas da ficha e as Passivas do Banco pelo endereço da ficha.
        await EsperarRequisicoesAsync(cut, todas =>
        {
            SemEfeitos(todas).Should().Equal($"GET {K(tipo)}/spell-abilities", $"GET {K(tipo)}/spell-abilities/passivas-disponiveis");
            todas.Should().Contain(CatalogoDeEfeitos);
        });
        LimparRequisicoes();
        return cut;
    }

    // ---------------- o que se lê ----------------

    protected static string[] HabilidadeRacial(string tipo) => tipo switch
    {
        // Só o Humano rola a Arca (a Aria é Humana; o Borin, Nephrytes).
        "Personagem" => new[] { "Habilidade Racial", "Adaptável", "Aprende rápido.", "«»", "Número rolado (1d20)" },
        "NPC" => new[] { "Habilidade Racial", "Adaptável", "Aprende rápido." },
        _ => Array.Empty<string>(),
    };

    protected static readonly string[] MagiasEHabilidades =
    {
        "Magias e Habilidades", "Montar do zero", "Origem", "«»", "Nome", "Magia", "Tipo", "«0»", "Grau", "«»", "Descrição",
        "Efeitos", "«»", "Efeito", "Nome do Efeito", "Quantidade", "Custo em PI", "Adicionar Magia/Habilidade",
        // Só a montada na ficha (Magia/Habilidade) tem Editar.
        "Bola de Fogo (Magia, Grau 1) — Gasto em PI 2, Custo 3 — Queima. — Efeitos:", "Editar", "Remover",
        "Golpe Duplo (Habilidade, Grau 2) — Gasto em PI 0, Custo 1 — Dois golpes. — Efeitos:", "Remover",
    };

    protected static readonly string[] Passivas =
    {
        "Habilidades Passivas", "«»", "Passiva do Banco", "Adicionar Passiva", "Pele de Ferro", "(—) — Dura.", "Remover",
    };

    protected static readonly string[] FormularioDeRuna =
    {
        "Runas", "Montar do zero", "Origem", "«»", "Nome", "«»", "Descrição", "«0»", "Grau", "«»", "Disciplina", "Sem tipo", "Tipo",
        "Enviar nova imagem", "Escolher imagem já enviada", "Adicionar Runa",
    };

    // A tabela do Personagem para todos os tipos que têm Runas (diferença decidida pelo usuário; a Criatura não tem Runas).
    protected static string[] Runas(string tipo) => tipo == "Criatura"
        ? Array.Empty<string>()
        : FormularioDeRuna.Concat(new[] { "Nome", "Descrição", "Grau", "Tipo", "Disciplina", "Runa do Vento", "Sopra.", "1", "Runa Arcana", "Emissão", "Remover" }).ToArray();

    protected static IEnumerable<string> LeituraDaAba(string tipo) =>
        HabilidadeRacial(tipo).Concat(MagiasEHabilidades).Concat(Passivas).Concat(Runas(tipo));

    // ---------------- requisições ----------------

    protected static string BancoDeMagias(string papel) => papel == "GM" ? "GET /spell-ability-bank" : "GET /campaigns/c1/available-spell-abilities";

    protected static string BancoDeRunas(string papel) => papel == "GM" ? "GET /rune-bank" : "GET /campaigns/c1/available-runes";

    /// <summary>Depois de mexer numa Magia/Habilidade: a lista, o Banco do papel e a ficha (o saldo de PI), nessa ordem.</summary>
    protected static string[] RecargaDasMagias(string tipo, string papel) =>
        new[] { $"GET {K(tipo)}/spell-abilities", BancoDeMagias(papel), $"GET {K(tipo)}" };

    protected static IElement FormularioDe(IRenderedComponent<IComponent> cut, string botao) => Botao(AbaAberta(cut), botao).Closest("form")!;

    protected static IElement FormularioDeMagia(IRenderedComponent<IComponent> cut) => FormularioDe(cut, "Adicionar Magia/Habilidade");

    protected static IElement FormularioDeRunaDa(IRenderedComponent<IComponent> cut) => FormularioDe(cut, "Adicionar Runa");

    /// <summary>A linha da lista de Magias e Habilidades que começa com <paramref name="nome"/>.</summary>
    protected static IElement LinhaDaMagia(IRenderedComponent<IComponent> cut, string nome) =>
        FormularioDeMagia(cut).ParentElement!.QuerySelectorAll("table tbody tr").Single(tr => TextoNormalizado(tr.QuerySelector("td")!).StartsWith(nome + " (", StringComparison.Ordinal));

    /// <summary>A seção de Passivas: o primeiro ancestral de "Adicionar Passiva" que também tem a tabela delas.</summary>
    protected static IElement SecaoDePassivas(IRenderedComponent<IComponent> cut)
    {
        var e = Botao(AbaAberta(cut), "Adicionar Passiva");
        while (e.QuerySelector("table") is null)
            e = e.ParentElement!;
        return e;
    }

    protected static IElement LinhaDaRuna(IRenderedComponent<IComponent> cut) =>
        FormularioDeRunaDa(cut).ParentElement!.QuerySelectorAll("table tbody tr").Single();

    /// <summary>O que se lê e os controles que mudam com o papel.</summary>
    public sealed class Leituras : RedeMagiasTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task A_aba_mostra_habilidade_racial_magias_passivas_e_runas_nesta_ordem(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            cut.WaitForAssertion(() => Leitura(AbaAberta(cut)).Should().Equal(LeituraDaAba(tipo)), TimeSpan.FromSeconds(5));
        }

        // A Leitura não vê atributos: GM e jogador têm a aba igual; o único controle desabilitado é "Adicionar
        // Passiva", até escolher uma Passiva do Banco.
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task So_adicionar_passiva_fica_desabilitado_ate_escolher(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            AbaAberta(cut).QuerySelectorAll("input[disabled]").Should().BeEmpty();
            AbaAberta(cut).QuerySelectorAll("button[disabled]").Select(TextoNormalizado).Should().Equal("Adicionar Passiva");
            AbaAberta(cut).QuerySelectorAll("button[title]").Select(b => b.GetAttribute("title"))
                .Should().Equal(tipo == "Criatura" ? Array.Empty<string>() : new[] { "Disciplina da Runa", "Tipo da Runa" });
        }

        // As opções do Banco de Magias (o do GM, ou o da campanha para o jogador): sem as Passivas, que têm seção própria.
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task O_banco_de_magias_lista_as_entradas_do_papel(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherOpcaoAsync(cut, () => FormularioDeMagia(cut), "Origem", "Escolher do Banco de Magias");
            var label = FormularioDeMagia(cut).QuerySelectorAll("label.mud-input-label").Single(l => TextoNormalizado(l) == "Entrada do Banco");
            await cut.InvokeAsync(() => label.Closest(".mud-input-control")!.MouseDown());

            cut.WaitForElements(".mud-popover-open .mud-list-item").Select(TextoNormalizado).Should().Equal("Escolha uma entrada", "Raio (Magia, Grau 2)");
            Requisicoes.Should().BeEmpty();
        }
    }

    /// <summary>Magias e Habilidades: adicionar do zero e do Banco, editar, remover.</summary>
    public sealed class MagiasDaFicha : RedeMagiasTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Adicionar_do_zero_grava_os_campos_e_os_efeitos_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Campo(FormularioDeMagia(cut), "Nome").Change("Cura"));
            await EscolherOpcaoAsync(cut, () => FormularioDeMagia(cut), "Tipo", "Habilidade");
            await cut.InvokeAsync(() => Campo(FormularioDeMagia(cut), "Grau").Change("1"));
            await cut.InvokeAsync(() => FormularioDeMagia(cut).QuerySelector("textarea")!.Change("Fecha feridas."));
            await EscolherOpcaoAsync(cut, () => FormularioDeMagia(cut), "Efeito", "Luz");
            await cut.InvokeAsync(() => Botao(FormularioDeMagia(cut), "Adicionar Efeito").Click());
            SemEfeitos(Requisicoes).Should().BeEmpty();
            await cut.InvokeAsync(() => FormularioDeMagia(cut).Submit());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(new[]
            {
                $"POST {K(tipo)}/spell-abilities {{\"descricao\":\"Fecha feridas.\",\"efeitos\":[{{\"custoPI\":2,\"efeitoNome\":\"Luz\",\"quantidade\":null}}],\"grau\":1,\"nome\":\"Cura\",\"sourceBankEntryId\":null,\"tipo\":\"Habilidade\"}}",
            }.Concat(RecargaDasMagias(tipo, papel))));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Adicionar_do_banco_grava_so_a_entrada_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherOpcaoAsync(cut, () => FormularioDeMagia(cut), "Origem", "Escolher do Banco de Magias");
            await EscolherOpcaoAsync(cut, () => FormularioDeMagia(cut), "Entrada do Banco", "Raio (Magia, Grau 2)");
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => FormularioDeMagia(cut).Submit());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(new[]
            {
                $"POST {K(tipo)}/spell-abilities {{\"descricao\":null,\"efeitos\":null,\"grau\":null,\"nome\":null,\"sourceBankEntryId\":\"b1\",\"tipo\":null}}",
            }.Concat(RecargaDasMagias(tipo, papel))));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Editar_abre_o_dialogo_com_a_entrada_e_salvar_grava_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDaMagia(cut, "Bola de Fogo"), "Editar").Click());
            var dialogo = Dialogo(cut);
            TextoNormalizado(dialogo.QuerySelector(".mud-dialog-title")!).Should().Be("Editar Magia");
            Leitura(dialogo.QuerySelector(".mud-dialog-content")!).Should().Equal(
                "«Bola de Fogo»", "Nome", "Magia", "Tipo", "«1»", "Grau", "«Queima.»", "Descrição",
                "Efeitos", "«»", "Efeito", "Nome do Efeito", "Quantidade", "Custo em PI");
            await cut.InvokeAsync(() => Campo(Dialogo(cut), "Nome").Change("Bola de Fogo Maior"));
            SemEfeitos(Requisicoes).Should().BeEmpty();
            await cut.InvokeAsync(() => Botao(Dialogo(cut), "Salvar").Click());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(new[]
            {
                $"PUT {K(tipo)}/spell-abilities/m1 {{\"descricao\":\"Queima.\",\"efeitos\":[],\"grau\":1,\"nome\":\"Bola de Fogo Maior\",\"tipo\":\"Magia\"}}",
            }.Concat(RecargaDasMagias(tipo, papel))));
            cut.FindAll(".mud-dialog").Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Cancelar_a_edicao_nao_grava_nada(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDaMagia(cut, "Bola de Fogo"), "Editar").Click());
            await cut.InvokeAsync(() => Botao(Dialogo(cut), "Cancelar").Click());

            cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Should().BeEmpty());
            await EsperarCalmariaAsync();
            SemEfeitos(Requisicoes).Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Remover_apaga_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDaMagia(cut, "Golpe Duplo"), "Remover").Click());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(new[] { $"DELETE {K(tipo)}/spell-abilities/m2" }.Concat(RecargaDasMagias(tipo, papel))));
        }

        // Um erro do servidor ao adicionar aparece no alerta da página.
        [Theory]
        [InlineData("Personagem")]
        [InlineData("NPC")]
        [InlineData("Criatura")]
        public async Task Uma_magia_recusada_mostra_a_mensagem_do_servidor(string tipo)
        {
            var cut = await AbrirAbaAsync(tipo, "GM");
            ResponderErro($"^{K(tipo)}/spell-abilities$", System.Net.HttpStatusCode.BadRequest, "PI insuficiente.");

            await EscolherOpcaoAsync(cut, () => FormularioDeMagia(cut), "Origem", "Escolher do Banco de Magias");
            await EscolherOpcaoAsync(cut, () => FormularioDeMagia(cut), "Entrada do Banco", "Raio (Magia, Grau 2)");
            await cut.InvokeAsync(() => FormularioDeMagia(cut).Submit());

            cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Select(TextoNormalizado).Should().Equal("PI insuficiente."));
            SemEfeitos(Requisicoes).Should().Equal($"POST {K(tipo)}/spell-abilities {{\"descricao\":null,\"efeitos\":null,\"grau\":null,\"nome\":null,\"sourceBankEntryId\":\"b1\",\"tipo\":null}}");
        }
    }

    /// <summary>Habilidades Passivas: pelo endereço de Magias e Habilidades da ficha.</summary>
    public sealed class PassivasDaFicha : RedeMagiasTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Adicionar_uma_passiva_do_banco_grava_pelo_endereco_da_ficha_e_recarrega_a_secao(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherOpcaoAsync(cut, () => SecaoDePassivas(cut), "Passiva do Banco", "Couraça");
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => Botao(SecaoDePassivas(cut), "Adicionar Passiva").Click());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(
                $"POST {K(tipo)}/spell-abilities {{\"sourceBankEntryId\":\"pb1\"}}",
                $"GET {K(tipo)}/spell-abilities",
                $"GET {K(tipo)}/spell-abilities/passivas-disponiveis"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Remover_uma_passiva_apaga_e_recarrega_a_secao(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(SecaoDePassivas(cut), "Remover").Click());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(
                $"DELETE {K(tipo)}/spell-abilities/p1",
                $"GET {K(tipo)}/spell-abilities",
                $"GET {K(tipo)}/spell-abilities/passivas-disponiveis"));
        }
    }

    /// <summary>Runas (Personagem e NPC): do zero, do Banco, com imagem, validação, remover.</summary>
    public sealed class RunasDaFicha : RedeMagiasTests
    {
        private static string[] RecargaDasRunas(string tipo, string papel) => new[] { $"GET {K(tipo)}/runes", BancoDeRunas(papel) };

        [Theory]
        [MemberData(nameof(TiposComRunas))]
        public async Task Adicionar_do_zero_grava_os_campos_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Campo(FormularioDeRunaDa(cut), "Nome").Change("Runa do Gelo"));
            await cut.InvokeAsync(() => FormularioDeRunaDa(cut).QuerySelector("textarea")!.Change("Congela."));
            await cut.InvokeAsync(() => Campo(FormularioDeRunaDa(cut), "Grau").Change("2"));
            await EscolherOpcaoAsync(cut, () => FormularioDeRunaDa(cut), "Disciplina", "Emissão");
            await EscolherOpcaoAsync(cut, () => FormularioDeRunaDa(cut), "Tipo", "Runa Negra");
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => FormularioDeRunaDa(cut).Submit());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(new[]
            {
                $"POST {K(tipo)}/runes {{\"descricao\":\"Congela.\",\"disciplina\":\"Emissao\",\"grau\":2,\"imageId\":null,\"nome\":\"Runa do Gelo\",\"sourceBankEntryId\":null,\"tipo\":\"Negra\"}}",
            }.Concat(RecargaDasRunas(tipo, papel))));
        }

        // A imagem da Runa sobe como as outras imagens da ficha: sem campanha para o GM, com a da ficha (c1) para o
        // jogador. (Antes do E ela subia com o texto literal campaignId=CampaignId; corrigido por decisão do usuário.)
        [Theory]
        [MemberData(nameof(TiposComRunas))]
        public async Task Enviar_uma_imagem_sobe_o_arquivo_e_a_runa_grava_com_ela(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Campo(FormularioDeRunaDa(cut), "Nome").Change("Runa do Gelo"));
            await EscolherOpcaoAsync(cut, () => FormularioDeRunaDa(cut), "Disciplina", "Emissão");
            var anexo = cut.FindComponents<ImageAttachmentField>().Single();
            await cut.InvokeAsync(() => anexo.Instance.UploadForTests(new FakeBrowserFile("runa.png")));
            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal(papel == "GM" ? "POST /images [file]" : "POST /images [file, campaignId=c1]"));
            LimparRequisicoes();
            await cut.InvokeAsync(() => FormularioDeRunaDa(cut).Submit());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(new[]
            {
                $"POST {K(tipo)}/runes {{\"descricao\":\"\",\"disciplina\":\"Emissao\",\"grau\":0,\"imageId\":\"img-nova\",\"nome\":\"Runa do Gelo\",\"sourceBankEntryId\":null,\"tipo\":null}}",
            }.Concat(RecargaDasRunas(tipo, papel))));
        }

        [Theory]
        [MemberData(nameof(TiposComRunas))]
        public async Task Adicionar_do_banco_grava_so_a_entrada_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await EscolherOpcaoAsync(cut, () => FormularioDeRunaDa(cut), "Origem", "Escolher do Banco de Runas");
            await EscolherOpcaoAsync(cut, () => FormularioDeRunaDa(cut), "Entrada do Banco", "Runa da Terra (Grau 2, Adição, Runa Negra)");
            Requisicoes.Should().BeEmpty();
            await cut.InvokeAsync(() => FormularioDeRunaDa(cut).Submit());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(new[]
            {
                $"POST {K(tipo)}/runes {{\"descricao\":null,\"disciplina\":null,\"grau\":null,\"imageId\":null,\"nome\":null,\"sourceBankEntryId\":\"rb1\",\"tipo\":null}}",
            }.Concat(RecargaDasRunas(tipo, papel))));
        }

        // Sem Nome a página não envia nada e diz o que falta.
        [Theory]
        [MemberData(nameof(TiposComRunas))]
        public async Task Sem_nome_nao_grava_e_mostra_o_que_falta(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => FormularioDeRunaDa(cut).Submit());

            cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Select(TextoNormalizado).Should().Equal("Informe o nome da runa."));
            await EsperarCalmariaAsync();
            Requisicoes.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(TiposComRunas))]
        public async Task Remover_apaga_e_recarrega(string tipo, string papel)
        {
            var cut = await AbrirAbaAsync(tipo, papel);

            await cut.InvokeAsync(() => Botao(LinhaDaRuna(cut), "Remover").Click());

            await EsperarRequisicoesAsync(cut, todas => SemEfeitos(todas).Should().Equal(new[] { $"DELETE {K(tipo)}/runes/r1" }.Concat(RecargaDasRunas(tipo, papel))));
        }
    }

    /// <summary>A Arca do Humano (só a Aria é Humana na rede): salva a ficha e relê a Habilidade Racial.</summary>
    public sealed class ArcaDoHumano : RedeMagiasTests
    {
        [Theory]
        [InlineData("GM")]
        [InlineData("Jogador")]
        public async Task O_numero_rolado_salva_a_ficha_e_rele_a_habilidade_racial(string papel)
        {
            var cut = await AbrirAbaAsync("Personagem", papel);

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Número rolado (1d20)").Change("7"));

            var put = $"PUT /character-sheets/s1 {RedeCabecalhoEInformacoesBasicasTests.CorpoDaFicha("Personagem", nome: "Aria").Replace("\"arcaRolada\":null", "\"arcaRolada\":7")}";
            await EsperarRequisicoesAsync(cut, todas =>
            {
                var sem = SemEfeitos(todas);
                sem.FirstOrDefault().Should().Be(put);
                sem.LastOrDefault().Should().Be("GET /character-sheets/s1/racial-ability");
                sem.Skip(1).SkipLast(1).Should().BeEquivalentTo(RedeCabecalhoEInformacoesBasicasTests.RecargaDepoisDeSalvar("Personagem"));
            });
        }
    }
}
