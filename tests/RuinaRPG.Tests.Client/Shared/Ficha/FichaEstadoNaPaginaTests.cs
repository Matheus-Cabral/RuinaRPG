using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// O <see cref="FichaEstado"/> dentro das três páginas (Review Focus do E): cada página cria um, o passa em cascata às
/// abas, re-renderiza quando ele avisa uma alteração e mostra o seu <see cref="FichaEstado.Erro"/> no alerta.
/// </summary>
public class FichaEstadoNaPaginaTests : RedeDeSegurancaBase
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    private static TipoDeFichaCliente TipoDoCliente(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private static FichaEstado Estado(IRenderedComponent<IComponent> cut) =>
        cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value!;

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_page_cascades_one_state_for_its_kind_and_sheet(string tipo)
    {
        var cut = await RenderFichaAsync(tipo, "Jogador");

        var cascatas = cut.FindComponents<CascadingValue<FichaEstado>>();
        cascatas.Should().ContainSingle();
        cascatas[0].Instance.IsFixed.Should().BeFalse();
        var estado = Estado(cut);
        estado.Tipo.Should().BeSameAs(TipoDoCliente(tipo));
        estado.SheetId.Should().Be(SheetId);
        // O que a carga da página põe no estado: o papel, a campanha (vem com a ficha) e a galeria.
        estado.IsGmCaller.Should().BeFalse();
        estado.CampaignId.Should().Be("c1");
        estado.MinhasImagens.Select(i => i.Id).Should().Equal("img1", "img2");
    }

    // Os ganchos que a página liga no estado (o que só ela sabe reler). Um gancho esquecido seria pulado em silêncio,
    // então cada página tem de ligar todos — inclusive os que vierem depois (o teste os acha por reflexão).
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_page_sets_every_state_hook(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        var estado = Estado(cut);

        var ganchos = typeof(FichaEstado).GetProperties()
            .Where(p => typeof(Delegate).IsAssignableFrom(p.PropertyType) && p.CanWrite)
            .ToList();
        ganchos.Select(g => g.Name).Should().BeEquivalentTo("RecarregarPontosDeIgnicao");
        ganchos.Where(g => g.GetValue(estado) is null).Select(g => g.Name).Should().BeEmpty();
    }

    // Review Focus (4): um erro posto no estado por uma aba filha aparece no alerta da página.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task An_error_set_on_the_state_by_a_child_shows_in_the_page_alert(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        var estado = Estado(cut);

        // Como uma aba faria depois de um POST recusado: fora de um evento da página, então avisa a alteração.
        await cut.InvokeAsync(() =>
        {
            estado.Erro = "Não foi possível adicionar a arma.";
            estado.NotificarAlteracao();
        });

        cut.WaitForAssertion(() => cut.Find(".mud-alert").TextContent.Should().Contain("Não foi possível adicionar a arma."));
    }

    // E o erro que a própria página põe (um PUT recusado) é o mesmo Erro do estado que as abas veem.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task An_error_the_page_sets_is_the_state_error(string tipo)
    {
        var cut = await RenderFichaAsync(tipo);
        ResponderErro("/attributes/Vigor", System.Net.HttpStatusCode.BadRequest, "Pontos insuficientes.");
        await IrParaAbaAsync(cut, "Atributos & Perícias");

        var vigor = cut.FindAll("tr").First(tr => TextoNormalizado(tr).StartsWith("Vigor", StringComparison.Ordinal)).QuerySelector("input")!;
        await cut.InvokeAsync(() => vigor.Change("3"));

        cut.WaitForAssertion(() => Estado(cut).Erro.Should().Be("Pontos insuficientes."));
        cut.Find(".mud-alert").TextContent.Should().Contain("Pontos insuficientes.");
        await EsperarCalmariaAsync();
    }

    // Review Focus (2): o aviso de Condições alteradas relê a ficha com as mesmas requisições de sempre e o que
    // agora vive no estado (as versões das Condições e das penalidades) re-renderiza com os dados novos.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_CondicoesAlteradas_notification_rerenders_with_fresh_data_and_the_same_requests(string tipo)
    {
        var cut = await RenderFichaAsync(tipo, "Jogador");
        await Services.GetRequiredService<AvisosDoHub>().StartAsync(notasSecretas: false);
        await IrParaAbaAsync(cut, "Combate");
        await EsperarCalmariaAsync();
        var condicoesAntes = Estado(cut).CondicoesVersao;
        var penalidadesAntes = Estado(cut).PenalidadesVersao;
        LimparRequisicoes();
        Responder("/condicoes-ativas", new[] { new { nome = "Atordoado", descricao = "Não consegue agir direito.", icone = "Atordoado", turnosRestantes = 2, linhas = new[] { "Vigor −2" } } });
        Responder("/equipment-penalties", new[] { new { itemNome = "Cota de Malha", requisitosPendentes = new[] { "Vigor 3" }, penalidade = new[] { "Esquiva −1" } } });
        Responder("/weapons", new[]
        {
            new
            {
                id = "w1", itemId = "i1", nome = "Espada Nova", tipoDeDano = "Cortante", alcance = 1, dados = "1d8", dano = 3, critico = "19-20", rank = "C",
                peso = 1.5m, isEquipped = true, durabilidadeAtual = 8, durabilidadeMaxima = 10, durabilidadeMaximo = 10, descricao = "Uma espada reta.",
            },
        });

        var k = TipoDoCliente(tipo).TipoDaNotificacao;
        await cut.InvokeAsync(() => NotificationConnection.RaiseCondicoesAlteradasAsync(new CondicoesAlteradasNotification(k, SheetId)));

        var p = $"/{Prefixo(tipo)}/{SheetId}";
        var recarga = new List<string> { $"GET {p}" };
        recarga.AddRange(RecargaDasAbas(tipo));
        // As magias e as runas vêm com o banco de onde se escolhe (o da campanha, para o jogador).
        recarga.AddRange(new[] { $"GET {p}/spell-abilities", "GET /campaigns/c1/available-spell-abilities" });
        if (tipo != "Criatura")
            recarga.AddRange(new[] { $"GET {p}/runes", "GET /campaigns/c1/available-runes" });
        recarga.Add($"GET {p}/artifacts");
        await EsperarRequisicoesAsync(cut, todas =>
        {
            // Os dois alertas releem quando a versão sobe; a recarga da ficha vem em sequência, na ordem de hoje.
            todas.Should().BeEquivalentTo(recarga.Concat(new[] { $"GET {p}/condicoes-ativas", $"GET {p}/equipment-penalties" }));
            todas.Where(r => !r.EndsWith("/condicoes-ativas") && !r.EndsWith("/equipment-penalties")).Should().Equal(recarga);
        });

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Condições ativas").And.Contain("Não consegue agir direito.");
            cut.Markup.Should().Contain("Espada Nova").And.NotContain("Espada Longa");
        });
        Estado(cut).CondicoesVersao.Should().Be(condicoesAntes + 1);
        Estado(cut).PenalidadesVersao.Should().Be(penalidadesAntes + 1);
    }
}
