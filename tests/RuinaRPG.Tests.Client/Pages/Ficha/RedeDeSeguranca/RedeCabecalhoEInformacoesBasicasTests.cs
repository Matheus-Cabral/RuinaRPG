using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;

/// <summary>
/// Rede de segurança do E: o cabeçalho (trilha, título, aviso de Novo Nível, Condições ativas, indicador de
/// salvamento) e a aba Informações Básicas das três fichas, como GM e como jogador, e as requisições da carga,
/// de um campo salvo sozinho, da troca de imagem e da troca de Linhagem/Variante. Fixa o que a tela mostra
/// hoje; precisa passar sem mudanças depois de cada passo do E. As classes aninhadas existem só para o xUnit
/// rodá-las em paralelo (cada uma espera debounce e recargas da página inteira).
/// </summary>
public abstract class RedeCabecalhoEInformacoesBasicasTests : RedeDeSegurancaBase
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

    protected static readonly object[] UmaCondicao =
    {
        new { nome = "Atordoado", descricao = "Tonto.", icone = "Atordoado", turnosRestantes = 2, linhas = new[] { "Vigor −2" } },
    };

    protected static readonly string[] LeituraDaCondicao = { "Condições ativas", "Atordoado", "— 2 turnos", "Tonto.", "Vigor −2" };

    // 2.1.0 (A.1): o jogador dono de um NPC/Criatura concedido vê o caminho pela campanha, como no Personagem; o GM, o de hoje.
    protected static string[] Trilha(string tipo, string papel) => (tipo, papel) switch
    {
        ("Personagem", "Jogador") => new[] { "Painel", "/", "Minhas Campanhas", "/", "Crônicas de Alker", "/", "Aria" },
        ("Personagem", _) => new[] { "Painel", "/", "Campanhas", "/", "Crônicas de Alker", "/", "Aria" },
        ("NPC", "Jogador") => new[] { "Painel", "/", "Minhas Campanhas", "/", "Crônicas de Alker", "/", "Borin" },
        ("Criatura", "Jogador") => new[] { "Painel", "/", "Minhas Campanhas", "/", "Crônicas de Alker", "/", "Lobo Cinzento" },
        ("NPC", _) => new[] { "Painel", "/", "NPCs do GM", "/", "Borin" },
        _ => new[] { "Painel", "/", "Bestiário do GM", "/", "Lobo Cinzento" },
    };

    protected static string[] LinksDaTrilha(string tipo, string papel) => (tipo, papel) switch
    {
        ("Personagem", "Jogador") => new[] { "Painel → painel", "Minhas Campanhas → minhas-campanhas", "Crônicas de Alker → campanhas/c1/jogador", "Aria → #" },
        ("Personagem", _) => new[] { "Painel → painel", "Campanhas → campanhas", "Crônicas de Alker → campanhas/c1", "Aria → #" },
        ("NPC", "Jogador") => new[] { "Painel → painel", "Minhas Campanhas → minhas-campanhas", "Crônicas de Alker → campanhas/c1/jogador", "Borin → #" },
        ("Criatura", "Jogador") => new[] { "Painel → painel", "Minhas Campanhas → minhas-campanhas", "Crônicas de Alker → campanhas/c1/jogador", "Lobo Cinzento → #" },
        ("NPC", _) => new[] { "Painel → painel", "NPCs do GM → npcs", "Borin → #" },
        _ => new[] { "Painel → painel", "Bestiário do GM → bestiario", "Lobo Cinzento → #" },
    };

    // 2.1.0 (cemitério): o GM tem a caixa "Morto" logo depois do título na Ficha de Personagem.
    protected static string[] CaixaMorto(string tipo, string papel) =>
        tipo == "Personagem" && papel == "GM" ? new[] { "[ ]", "Morto" } : Array.Empty<string>();

    protected static string Titulo(string tipo) => tipo switch
    {
        "Personagem" => "Ficha de Personagem",
        "NPC" => "Ficha de NPC",
        _ => "Ficha de Criatura",
    };

    /// <summary>O que se lê antes das abas (trilha, título, avisos).</summary>
    protected static List<string> LeituraDoCabecalho(IRenderedComponent<IComponent> cut) =>
        Leitura(cut.Find(".ficha-condicoes-raiz")).TakeWhile(t => t != "Informações Básicas").ToList();

    // ---------------- Informações Básicas ----------------

    protected static readonly string[] Caracteristicas =
    {
        "Características", "«»", "«»", "Especificação (se exigida pela característica)", "Adicionar Característica",
        "Positivas: 2 / 5", "Negativas: 1 / 3",
        "Positivas (Total: 2)", "Corajoso (2) — Não foge.", "Remover",
        "Negativas (Total: 1)", "Teimoso (-1) — Não cede. (com nobres)", "Remover",
    };

    protected static readonly string[] AmbaresPersonagem =
    {
        "Âmbares Absorvidos", "«1»", "Rank F", "«2»", "Rank E", "«0»", "Rank D", "«0»", "Rank C", "«0»", "Rank B", "«0»", "Rank A", "«0»", "Rank S",
    };

    protected static readonly string[] AmbaresNpc =
    {
        "Âmbares Absorvidos", "«0»", "Rank F", "«1»", "Rank E", "«1»", "Rank D", "«0»", "Rank C", "«0»", "Rank B", "«0»", "Rank A", "«0»", "Rank S",
    };

    protected static IEnumerable<string> LeituraDeInformacoesBasicas(string tipo, string papel) => tipo switch
    {
        "Personagem" => new[]
        {
            "Identidade", "«Aria»", "Nome", "Humano", "Linhagem", "Sinir (Sol)", "Variante", "Feiticeiro", "Vocação", "Piromante", "Classe",
            "«Uma casa»", "Propriedade",
            "Nível e Progressão", "Nível: 3", "Círculo: 1", "[x]", "Possui Coração de Mana?", "«120»", "Experiência Atual",
            "Faltam 30 XP para o próximo nível", "«»", "Adicionar/subtrair XP", "Aplicar", "VIS Atual: 7",
            "«12»", "Pontos de Ignição Atual", "«»", "Adicionar/subtrair PI (bônus manual)", "Aplicar",
        }.Concat(AmbaresPersonagem).Concat(Caracteristicas),
        "NPC" => new[]
        {
            "Identidade", "«Borin»", "Nome", "Nephrytes", "Linhagem", "Koroanos (Sol)", "Variante", "Campeão", "Vocação", "Duelista", "Classe",
            "«Uma forja»", "Propriedade",
            "Nível e Progressão", "«4»", "Nível", "Grau: 2", "«200»", "Experiência Atual",
        }.Concat(papel == "GM" ? new[] { "Abate: 50", "Assistência: 25" } : Array.Empty<string>())
            .Concat(new[] { "VIS Atual: 8", "«9»", "Pontos de Ignição Atual" }).Concat(AmbaresNpc).Concat(Caracteristicas),
        _ => new[]
        {
            "Identidade", "«Lobo Cinzento»", "Nome", "«Lobo»", "Raça", "Físico", "Arquétipo", "«Caçador»", "Sub Arquétipo", "Ar", "Afinidade", "D", "Rank",
            "Nível e Progressão", "«2»", "Nível", "«60»", "Experiência Atual", "Abate: 40", "Assistência: 20", "«5»", "Pontos de Ignição",
        }.Concat(papel == "GM"
            // Só o Auditor de Regras (aqui, o GM) cria característica de criatura ali mesmo.
            ? Caracteristicas.Take(2).Append("Nova característica de criatura").Concat(Caracteristicas.Skip(2))
            : Caracteristicas),
    };

    // ---------------- requisições e salvamentos ----------------

    protected internal static string CorpoDaFicha(string tipo, string nome = "Novo", string? imageId = "img1", string? linhagem = null, string? variante = null)
    {
        var img = imageId is null ? "null" : $"\"{imageId}\"";
        return tipo switch
        {
            "Personagem" =>
                "{\"adrenalinaAtual\":3,\"adrenalinaAtualCarregada\":3,\"afinidade\":\"Fogo\",\"afinidadeAdicional\":0,\"arcaRolada\":null,\"ciclos\":4,"
                + "\"cobertura\":\"Nenhuma\",\"eapAtual\":7,\"equipmentKitId\":null,\"estrela\":null,\"estresseAtual\":1,\"estresseAtualCarregado\":1,"
                + "\"experienciaAtual\":120,\"focoAtual\":10,\"focoAtualCarregado\":10,\"historicoId\":null,"
                + $"\"imageId\":{img},\"linhagem\":{Texto(linhagem, "Humano")},\"nome\":\"{nome}\","
                + "\"nucleosRankA\":0,\"nucleosRankB\":0,\"nucleosRankC\":0,\"nucleosRankD\":0,\"nucleosRankE\":2,\"nucleosRankF\":1,\"nucleosRankS\":0,"
                + "\"pontosDeIgnicaoAtual\":12,\"pontosDeIgnicaoBonusManual\":2,\"pontosDePericiaBonusCritico\":1,\"possuiCoracaoDeMana\":true,"
                + $"\"propriedade\":\"Uma casa\",\"sinaAtual\":1,\"subVocacao\":\"Piromante\",\"variante\":{Texto(variante, "Sinir")},"
                + "\"vitalidadeAtual\":20,\"vitalidadeAtualCarregada\":20,\"vocacao\":\"Feiticeiro\"}",
            "NPC" =>
                "{\"adrenalinaAtual\":4,\"adrenalinaAtualCarregada\":4,\"afinidade\":\"Terra\",\"afinidadeAdicional\":0,\"arcaRolada\":null,\"ciclos\":2,"
                + "\"cobertura\":\"Nenhuma\",\"eapAtual\":8,\"equipmentKitId\":null,\"estrela\":null,\"estresseAtual\":2,\"estresseAtualCarregado\":2,"
                + "\"experienciaAtual\":200,\"focoAtual\":6,\"focoAtualCarregado\":6,\"historicoId\":null,"
                + $"\"imageId\":{img},\"linhagem\":{Texto(linhagem, "Nephrytes")},\"nivel\":4,\"nome\":\"{nome}\","
                + "\"nucleosRankA\":0,\"nucleosRankB\":0,\"nucleosRankC\":0,\"nucleosRankD\":1,\"nucleosRankE\":1,\"nucleosRankF\":0,\"nucleosRankS\":0,"
                + "\"pontosDeIgnicaoAtual\":9,\"pontosDeIgnicaoTotal\":30,\"possuiCoracaoDeMana\":false,"
                + $"\"propriedade\":\"Uma forja\",\"sinaAtual\":2,\"subVocacao\":\"Duelista\",\"variante\":{Texto(variante, "Koroanos")},"
                + "\"vitalidadeAtual\":30,\"vitalidadeAtualCarregada\":30,\"vocacao\":\"Campeao\"}",
            _ =>
                "{\"adrenalinaAtual\":2,\"adrenalinaAtualCarregada\":2,\"afinidade\":\"Ar\",\"arquetipo\":\"Fisico\",\"cobertura\":\"Nenhuma\","
                + "\"experienciaAtual\":60,\"focoAtual\":4,\"focoAtualCarregado\":4,"
                + $"\"imageId\":{img},\"nivel\":2,\"nome\":\"{nome}\",\"pontosDeIgnicao\":5,\"raca\":\"Lobo\",\"rank\":\"D\",\"subArquetipo\":\"Caçador\","
                + "\"vitalidadeAtual\":18,\"vitalidadeAtualCarregada\":18}",
        };
    }

    // null = o valor da ficha; "" = o campo foi limpo (vai null).
    protected static string Texto(string? valor, string padrao) => valor switch
    {
        null => $"\"{padrao}\"",
        "" => "null",
        _ => $"\"{valor}\"",
    };

    /// <summary>O que a página relê depois de gravar a ficha pelo PUT geral (sem ordem: são em sequência hoje, mas só o conjunto importa).</summary>
    protected internal static string[] RecargaDepoisDeSalvar(string tipo)
    {
        var k = $"/{Prefixo(tipo)}/s1";
        var lista = new List<string> { $"GET {k}" };
        if (tipo != "Criatura")
            lista.Add($"GET {k}/racial-traits/pending");
        lista.AddRange(new[] { $"GET {k}/level-up-notice", $"GET {k}/attributes", $"GET {k}/attributes/budget", $"GET {k}/skills" });
        if (tipo == "Personagem")
            lista.Add($"GET {k}/skills/budget");
        if (tipo != "Criatura")
            lista.Add($"GET {k}/affinities");
        lista.AddRange(new[] { $"GET {k}/weapons", $"GET {k}/armor-slots", $"GET {k}/shields", $"GET {k}/sub-attributes", $"GET {k}/modificador-de-dano", $"GET {k}/traits" });
        if (tipo != "Criatura")
            lista.Add($"GET {k}/racial-ability");
        return lista.ToArray();
    }

    protected async Task AfirmarSalvamentoAsync(IRenderedComponent<IComponent> cut, string tipo, string corpo, params string[] antes)
    {
        var put = $"PUT /{Prefixo(tipo)}/s1 {corpo}";
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.Take(antes.Length).Should().Equal(antes);
            todas.Skip(antes.Length).FirstOrDefault().Should().Be(put);
            todas.Skip(antes.Length + 1).Should().BeEquivalentTo(RecargaDepoisDeSalvar(tipo));
        });
    }

    protected async Task AbrirEdicaoDaImagemAsync(IRenderedComponent<IComponent> cut)
    {
        await cut.InvokeAsync(() => AbaAberta(cut).QuerySelector(".mud-avatar")!.Click());
        var editar = cut.WaitForElement("button[title='Editar imagem']");
        await cut.InvokeAsync(() => editar.Click());
    }

    protected async Task IrParaRemoverImagemAsync(IRenderedComponent<IComponent> cut)
    {
        await cut.InvokeAsync(() => AbaAberta(cut).QuerySelector(".mud-avatar")!.Click());
        var remover = cut.WaitForElement("button[title='Remover imagem']");
        await cut.InvokeAsync(() => remover.Click());
    }

    protected async Task EscolherAsync(IRenderedComponent<IComponent> cut, string rotulo, string opcao)
    {
        var label = AbaAberta(cut).QuerySelectorAll("label.mud-input-label").Single(l => TextoNormalizado(l) == rotulo);
        await cut.InvokeAsync(() => label.Closest(".mud-input-control")!.MouseDown());
        var item = cut.WaitForElements(".mud-popover-open .mud-list-item").Single(li => TextoNormalizado(li) == opcao);
        await cut.InvokeAsync(() => item.Click());
    }

    // ---------------- Nível e Progressão: o que cada tipo salva por conta própria ----------------

    public static TheoryData<string, string> TiposComNivelAMao => new()
    {
        { "NPC", "GM" }, { "NPC", "Jogador" },
        { "Criatura", "GM" }, { "Criatura", "Jogador" },
    };

    protected static string[] RecargaDepoisDoNivel(string tipo) =>
        RecargaDepoisDeSalvar(tipo).Where(r => !r.EndsWith("/racial-traits/pending")).ToArray();

    protected async Task AfirmarEndpointProprioAsync(IRenderedComponent<IComponent> cut, string put, string[] recarga)
    {
        await EsperarRequisicoesAsync(cut, todas =>
        {
            todas.FirstOrDefault().Should().Be(put);
            todas.Skip(1).Should().BeEquivalentTo(recarga);
        });
    }

    protected static string SemCoracao() =>
        "{\"id\":\"s1\",\"ownerId\":null,\"campaignId\":null,\"imageUrl\":\"/img/um.png\",\"imageId\":\"img1\",\"nome\":\"Borin\",\"linhagem\":\"Nephrytes\",\"variante\":\"Koroanos\","
        + "\"vocacao\":\"Feiticeiro\",\"subVocacao\":\"Piromante\",\"afinidade\":\"Terra\",\"propriedade\":\"Uma forja\",\"nivel\":4,\"circulo\":1,\"grau\":0,\"possuiCoracaoDeMana\":false,"
        + "\"experienciaAtual\":200,\"eapAtual\":8,\"nucleosRankF\":0,\"nucleosRankE\":1,\"nucleosRankD\":1,\"nucleosRankC\":0,\"nucleosRankB\":0,\"nucleosRankA\":0,\"nucleosRankS\":0,"
        + "\"pontosDeIgnicaoAtual\":9,\"pontosDeIgnicaoTotal\":30,\"vitalidadeAtual\":30,\"focoAtual\":6,\"adrenalinaAtual\":4,\"estresseAtual\":2,\"cobertura\":\"Nenhuma\",\"ciclos\":2,"
        + "\"graduacao\":1,\"graduacaoLabel\":\"Círculo\",\"vitalidadeMaximo\":32,\"focoMaximo\":8,\"adrenalinaMaximo\":6,\"estresseMaximo\":10,\"sinaAtual\":2,\"afinidadeAdicional\":0,"
        + "\"vocacaoArcanaGasta\":0,\"vocacaoArcanaMaxima\":0,\"abate\":50,\"assistencia\":25}";

    /// <summary>Trilha, título, Condições ativas, aviso de Novo Nível e indicador de salvamento.</summary>
    [Collection("Tempo real")]
    public sealed class Cabecalho : RedeCabecalhoEInformacoesBasicasTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task O_cabecalho_mostra_trilha_titulo_condicoes_e_aviso_de_novo_nivel(string tipo, string papel)
        {
            Responder("/level-up-notice", new { bonusTexts = new[] { "+1 Vigor", "+2 Pontos de Perícia" } });
            Responder("/condicoes-ativas", UmaCondicao);

            var cut = await RenderFichaAsync(tipo, papel);

            var esperado = new List<string>(Trilha(tipo, papel)) { Titulo(tipo) };
            esperado.AddRange(CaixaMorto(tipo, papel));
            if (MostraCondicoes(tipo, papel))
                esperado.AddRange(LeituraDaCondicao);
            esperado.AddRange(new[] { "Novo Nível!", "+1 Vigor", "+2 Pontos de Perícia", "Fechar" });
            LeituraDoCabecalho(cut).Should().Equal(esperado);

            cut.FindAll(".mud-breadcrumb-item")
                .Select(li => li.QuerySelector("a") is { } a ? $"{TextoNormalizado(li)} → {a.GetAttribute("href")}" : TextoNormalizado(li))
                .Should().Equal(LinksDaTrilha(tipo, papel));
            TextoNormalizado(cut.Find("h1")).Should().Be(Titulo(tipo));
        }

        public static TheoryData<string> NpcECriatura => new() { "NPC", "Criatura" };

        [Theory]
        [MemberData(nameof(NpcECriatura))]
        public async Task Jogador_ve_a_trilha_pela_campanha(string tipo)
        {
            var cut = await RenderFichaAsync(tipo, "Jogador");

            var links = cut.FindAll(".mud-breadcrumb-item a").Select(a => a.GetAttribute("href")).ToList();
            links.Should().Equal("painel", "minhas-campanhas", "campanhas/c1/jogador", "#");
            links.Should().NotContain(new[] { "npcs", "bestiario" });
        }

        [Theory]
        [MemberData(nameof(NpcECriatura))]
        public async Task GM_ve_a_trilha_de_hoje(string tipo)
        {
            var cut = await RenderFichaAsync(tipo, "GM");

            cut.FindAll(".mud-breadcrumb-item a").Select(a => a.GetAttribute("href"))
                .Should().Equal("painel", tipo == "NPC" ? "npcs" : "bestiario", "#");
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Sem_avisos_o_cabecalho_e_so_trilha_e_titulo(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);

            LeituraDoCabecalho(cut).Should().Equal(new List<string>(Trilha(tipo, papel)) { Titulo(tipo) }.Concat(CaixaMorto(tipo, papel)));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Fechar_o_aviso_de_novo_nivel_o_dispensa_no_servidor(string tipo, string papel)
        {
            Responder("/level-up-notice", new { bonusTexts = new[] { "+1 Vigor" } });
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            var fechar = cut.FindAll("button").Single(b => TextoNormalizado(b) == "Fechar" && b.Closest(".mud-alert") is not null);
            await cut.InvokeAsync(() => fechar.Click());

            await EsperarRequisicoesAsync(cut, todas => todas.Should().Equal($"POST /{Prefixo(tipo)}/s1/dismiss-level-up-notice"));
            LeituraDoCabecalho(cut).Should().NotContain("Novo Nível!");
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task O_indicador_de_salvamento_aparece_so_depois_de_salvar(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            TextoNormalizado(AbaAberta(cut)).Should().NotContain("Salvo às").And.NotContain("Salvando");

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Nome").Change("Novo"));

            cut.WaitForAssertion(() => TextoNormalizado(AbaAberta(cut)).Should().MatchRegex(@"^Salvo às \d{2}:\d{2} Identidade"), TimeSpan.FromSeconds(3));
        }
    }

    /// <summary>O que a aba mostra e o que a página pede ao abrir.</summary>
    [Collection("Tempo real")]
    public sealed class InformacoesBasicas : RedeCabecalhoEInformacoesBasicasTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Informacoes_basicas_mostra_os_campos_e_valores_nesta_ordem(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);

            Leitura(AbaAberta(cut)).Should().Equal(LeituraDeInformacoesBasicas(tipo, papel));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task O_avatar_mostra_a_imagem_da_ficha_e_sem_ela_o_icone(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            AbaAberta(cut).QuerySelector(".mud-avatar img")!.GetAttribute("src").Should().Be("/img/um.png");

            await IrParaRemoverImagemAsync(cut);

            cut.WaitForAssertion(() => AbaAberta(cut).QuerySelector(".mud-avatar img").Should().BeNull());
            AbaAberta(cut).QuerySelector(".mud-avatar svg").Should().NotBeNull();
        }

        // As três primeiras são em sequência (a ficha diz a campanha, que escolhe a galeria de imagens); o resto
        // a página pede em paralelo, então a rede fixa o conjunto (cada uma exatamente uma vez), não a ordem.
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task A_carga_da_pagina_faz_estas_requisicoes(string tipo, string papel)
        {
            await RenderFichaAsync(tipo, papel);

            Requisicoes.Take(3).Should().Equal(InicioDaCarga(tipo, papel));
            Requisicoes.Skip(3).Should().BeEquivalentTo(CargaInicial(tipo, papel));
        }
    }

    /// <summary>Um campo salvo sozinho, a troca de imagem e a troca de Linhagem/Variante.</summary>
    public sealed class Salvamentos : RedeCabecalhoEInformacoesBasicasTests
    {
        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Editar_o_nome_salva_a_ficha_inteira_uma_vez_e_recarrega(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Nome").Change("Novo"));

            await AfirmarSalvamentoAsync(cut, tipo, CorpoDaFicha(tipo));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Escolher_uma_imagem_ja_enviada_salva_a_ficha_com_ela(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();
            await AbrirEdicaoDaImagemAsync(cut);

            var galeria = cut.FindAll("button").Single(b => TextoNormalizado(b) == "Escolher imagem já enviada");
            await cut.InvokeAsync(() => galeria.Click());
            var miniatura = cut.WaitForElements(".mud-dialog img[src='/img/dois.png']").Single().ParentElement!;
            await cut.InvokeAsync(() => miniatura.Click());

            await AfirmarSalvamentoAsync(cut, tipo, CorpoDaFicha(tipo, nome: tipo switch { "Personagem" => "Aria", "NPC" => "Borin", _ => "Lobo Cinzento" }, imageId: "img2"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Enviar_uma_imagem_nova_sobe_o_arquivo_e_salva_a_ficha_com_ela(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();
            await AbrirEdicaoDaImagemAsync(cut);

            cut.WaitForState(() => cut.FindComponents<InputFile>().Count > 0);
            var arquivo = cut.FindComponents<InputFile>().Single();
            await cut.InvokeAsync(() => arquivo.UploadFiles(InputFileContent.CreateFromText("conteudo", "foto.png")));

            // O jogador sobe a imagem na campanha da ficha.
            var upload = papel == "GM" ? "POST /images [file]" : "POST /images [file, campaignId=c1]";
            await AfirmarSalvamentoAsync(cut, tipo, CorpoDaFicha(tipo, nome: tipo switch { "Personagem" => "Aria", "NPC" => "Borin", _ => "Lobo Cinzento" }, imageId: "img-nova"), upload);
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Remover_a_imagem_salva_a_ficha_sem_ela(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await IrParaRemoverImagemAsync(cut);

            await AfirmarSalvamentoAsync(cut, tipo, CorpoDaFicha(tipo, nome: tipo switch { "Personagem" => "Aria", "NPC" => "Borin", _ => "Lobo Cinzento" }, imageId: null));
        }

        [Theory]
        [MemberData(nameof(TiposComLinhagem))]
        public async Task Trocar_a_linhagem_limpa_a_variante_e_salva_uma_vez(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await EscolherAsync(cut, "Linhagem", "Phylauc");

            await AfirmarSalvamentoAsync(cut, tipo, CorpoDaFicha(tipo, nome: tipo == "Personagem" ? "Aria" : "Borin", linhagem: "Phylauc", variante: ""));
        }

        [Theory]
        [MemberData(nameof(TiposComLinhagem))]
        public async Task Trocar_a_variante_salva_a_ficha(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await EscolherAsync(cut, "Variante", tipo == "Personagem" ? "Laonir (Lua)" : "Yavos (Lua)");

            await AfirmarSalvamentoAsync(cut, tipo, CorpoDaFicha(tipo, nome: tipo == "Personagem" ? "Aria" : "Borin", variante: tipo == "Personagem" ? "Laonir" : "Yavos"));
        }
    }

    /// <summary>Nível, Experiência, XP/PI e Coração de Mana: o que cada tipo salva e por qual endpoint.</summary>
    public sealed class NivelEProgressao : RedeCabecalhoEInformacoesBasicasTests
    {
        [Theory]
        [MemberData(nameof(TiposComNivelAMao))]
        public async Task O_nivel_editado_a_mao_salva_pelo_endpoint_dele(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Nível").Change("5"));

            await AfirmarEndpointProprioAsync(cut, $"PUT /{Prefixo(tipo)}/s1/nivel 5", RecargaDepoisDoNivel(tipo));
        }

        [Theory]
        [MemberData(nameof(TiposComNivelAMao))]
        public async Task A_experiencia_editada_a_mao_salva_pelo_endpoint_dela(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Experiência Atual").Change("75"));

            await AfirmarEndpointProprioAsync(cut, $"PUT /{Prefixo(tipo)}/s1/experiencia-atual 75", RecargaDepoisDoNivel(tipo));
        }

        [Theory]
        [InlineData("Personagem", "GM")]
        [InlineData("Personagem", "Jogador")]
        public async Task No_personagem_a_experiencia_vai_no_salvamento_geral(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Experiência Atual").Change("75"));

            await AfirmarSalvamentoAsync(cut, tipo, CorpoDaFicha(tipo, nome: "Aria").Replace("\"experienciaAtual\":120", "\"experienciaAtual\":75"));
        }

        [Theory]
        [InlineData("GM")]
        [InlineData("Jogador")]
        public async Task Adicionar_xp_soma_a_experiencia_e_salva_na_hora(string papel)
        {
            var cut = await RenderFichaAsync("Personagem", papel);
            LimparRequisicoes();

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Adicionar/subtrair XP").Change("15"));
            var aplicar = AbaAberta(cut).QuerySelectorAll("button").Where(b => TextoNormalizado(b) == "Aplicar").ToList();
            await cut.InvokeAsync(() => aplicar[0].Click());

            await AfirmarSalvamentoAsync(cut, "Personagem", CorpoDaFicha("Personagem", nome: "Aria").Replace("\"experienciaAtual\":120", "\"experienciaAtual\":135"));
        }

        [Theory]
        [InlineData("GM")]
        [InlineData("Jogador")]
        public async Task Adicionar_pi_de_bonus_manual_salva_na_hora(string papel)
        {
            var cut = await RenderFichaAsync("Personagem", papel);
            LimparRequisicoes();

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), "Adicionar/subtrair PI (bônus manual)").Change("3"));
            var aplicar = AbaAberta(cut).QuerySelectorAll("button").Where(b => TextoNormalizado(b) == "Aplicar").ToList();
            await cut.InvokeAsync(() => aplicar[1].Click());

            await AfirmarSalvamentoAsync(cut, "Personagem", CorpoDaFicha("Personagem", nome: "Aria").Replace("\"pontosDeIgnicaoBonusManual\":2", "\"pontosDeIgnicaoBonusManual\":5"));
        }

        [Theory]
        [MemberData(nameof(TiposEPapeis))]
        public async Task Os_pontos_de_ignicao_salvam_pelo_endpoint_deles(string tipo, string papel)
        {
            var cut = await RenderFichaAsync(tipo, papel);
            LimparRequisicoes();

            await cut.InvokeAsync(() => Campo(AbaAberta(cut), tipo == "Criatura" ? "Pontos de Ignição" : "Pontos de Ignição Atual").Change("20"));

            await AfirmarEndpointProprioAsync(cut, $"PUT /{Prefixo(tipo)}/s1/pontos-de-ignicao 20", new[] { $"GET /{Prefixo(tipo)}/s1" });
        }

        [Theory]
        [InlineData("Personagem")]
        [InlineData("NPC")]
        public async Task Marcar_coracao_de_mana_salva_a_ficha(string tipo)
        {
            // O NPC padrão é Campeão (sem Círculo); como Feiticeiro ele também mostra o Coração de Mana.
            if (tipo == "NPC")
                Responder("/npc-sheets/s1", SemCoracao());
            var cut = await RenderFichaAsync(tipo);
            LimparRequisicoes();

            var caixa = AbaAberta(cut).QuerySelectorAll("label").Single(l => TextoNormalizado(l) == "Possui Coração de Mana?").QuerySelector("input")!;
            await cut.InvokeAsync(() => caixa.Change(tipo == "NPC"));

            var esperado = tipo == "NPC"
                ? CorpoDaFicha("NPC", nome: "Borin").Replace("\"possuiCoracaoDeMana\":false", "\"possuiCoracaoDeMana\":true").Replace("\"vocacao\":\"Campeao\"", "\"vocacao\":\"Feiticeiro\"").Replace("\"subVocacao\":\"Duelista\"", "\"subVocacao\":\"Piromante\"")
                : CorpoDaFicha("Personagem", nome: "Aria").Replace("\"possuiCoracaoDeMana\":true", "\"possuiCoracaoDeMana\":false");
            await AfirmarSalvamentoAsync(cut, tipo, esperado);
        }
    }
}
