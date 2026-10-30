using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Afinidades;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// Afinidades da ficha por pontos (<c>/afinidades</c>, Ficha de Personagem 2.c e R0007), que só Personagem e NPC têm.
/// As regras valem para quem edita a ficha como jogador: o dono do Personagem e o jogador de um NPC concedido
/// (Ficha de NPCs R0010). O catálogo é o semeado (ids fixos; Feiticeiro, Adepto e Bruxo veem tudo; total do
/// Círculo 0 = 3); o que um teste muda nele (requisito, teto, visibilidade) volta ao fim do teste.
/// </summary>
public abstract class AfinidadesDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    protected const int Terra = 0, Agua = 1, Fogo = 2, Ar = 3, Gelo = 4, Alma = 9, Necromancia = 16;

    /// <summary>A ficha do tipo, rastreada, para os testes mudarem Vocação, Classe, Afinidade Adicional e coração de mana.</summary>
    protected abstract Task<IFichaComAfinidades> FichaNoBancoAsync(RuinaRpgDbContext db, Guid id);

    /// <summary>Uma linha nova da tabela de pontos do tipo, para inserir direto no banco.</summary>
    protected abstract IPontosDeAfinidadeDaFicha NovosPontos();

    protected async Task DefinirAsync(FichaDeTeste f, Vocacao? vocacao, int afinidadeAdicional, bool coracaoDeMana = false, string? classe = null, int? afinidade = null)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var ficha = await FichaNoBancoAsync(db, Guid.Parse(f.SheetId));
        ficha.Vocacao = vocacao;
        ficha.SubVocacao = classe;
        ficha.AfinidadeAdicional = afinidadeAdicional;
        ficha.PossuiCoracaoDeMana = coracaoDeMana;
        ficha.Afinidade = afinidade;
        await db.SaveChangesAsync();
    }

    /// <summary>Ficha editada por um jogador (o dono do Personagem; o jogador de um NPC concedido).</summary>
    protected async Task<FichaDeTeste> FichaDeJogadorAsync(Vocacao? vocacao, int afinidadeAdicional, bool coracaoDeMana = false, string? classe = null)
    {
        var f = await FichaDeJogadorAsync();
        await DefinirAsync(f, vocacao, afinidadeAdicional, coracaoDeMana, classe);
        return f;
    }

    protected async Task InserirPontosAsync(FichaDeTeste f, params (int Elemento, int Pontos)[] pontos)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        foreach (var (elemento, valor) in pontos)
        {
            var linha = NovosPontos();
            linha.Id = Guid.NewGuid();
            linha.SheetId = Guid.Parse(f.SheetId);
            linha.ElementoId = elemento;
            linha.Pontos = valor;
            db.Add((object)linha);
        }
        await db.SaveChangesAsync();
    }

    protected Task<HttpResponseMessage> PutAsync(FichaDeTeste f, string token, int elemento, int pontos, int? experiencia = null) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"afinidades/{elemento}"), token, new AtualizarAfinidadeRequest(pontos, experiencia)));

    protected async Task PutOkAsync(FichaDeTeste f, int elemento, int pontos, int? experiencia = null)
    {
        var response = await PutAsync(f, f.Token, elemento, pontos, experiencia);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    protected async Task<AfinidadesDaFichaResponse> GetAsync(FichaDeTeste f, string? token = null)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "afinidades"), token ?? f.Token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AfinidadesDaFichaResponse>())!;
    }

    protected static async Task DeveRecusarAsync(HttpResponseMessage response, string texto)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(texto);
    }

    /// <summary>Muda o catálogo para um teste e o devolve como estava ao fim, mesmo se o teste falhar.</summary>
    protected async Task ComCatalogoAsync(Func<RuinaRpgDbContext, Task> mudar, Func<RuinaRpgDbContext, Task> desfazer, Func<Task> teste)
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await mudar(scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>());
        }
        try
        {
            await teste();
        }
        finally
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            await desfazer(scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>());
        }
    }

    [Fact]
    public async Task Uma_Vocacao_magica_ve_todos_os_elementos_com_gasto_maximo_e_o_Sub_Elemento_bloqueado_com_a_dica()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 2);
        await InserirPontosAsync(f, (Ar, 1));

        var afinidades = await GetAsync(f);

        // Feiticeiro sem coração de mana: Círculo 0 → total 3, + Afinidade Adicional 2.
        (afinidades.Gasto, afinidades.Maximo, afinidades.SecaoVisivel).Should().Be((1, 5, true));
        afinidades.Elementos.Select(e => e.Id).Should().Equal(Enumerable.Range(0, 19));
        afinidades.Elementos.Single(e => e.Id == Ar).Should().Be(new ElementoDaFichaResponse(Ar, "Ar", "Base", null, 1, null, false, null, 1));
        afinidades.Elementos.Single(e => e.Id == Alma).Should().Be(new ElementoDaFichaResponse(Alma, "Alma", "Caminho", null, 0, null, false, null, 0));
        // O par é gravado em ordem de id (Água=1, Ar=3), e a dica segue essa ordem.
        afinidades.Elementos.Single(e => e.Id == Gelo).Should().Be(
            new ElementoDaFichaResponse(Gelo, "Gelo", "SubElemento", null, 0, null, true, "Precisa de 1 em Água e 1 em Ar", 0));
    }

    [Fact]
    public async Task Os_pontos_sobem_e_descem_pelo_PUT_e_a_resposta_traz_a_lista_nova()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);

        var response = await PutAsync(f, f.Token, Fogo, 3, experiencia: 4);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var depois = (await response.Content.ReadFromJsonAsync<AfinidadesDaFichaResponse>())!;
        await PutOkAsync(f, Fogo, 2);

        depois.Gasto.Should().Be(3);
        depois.Elementos.Single(e => e.Id == Fogo).Experiencia.Should().Be(4);
        var lida = await GetAsync(f);
        lida.Gasto.Should().Be(2);
        lida.Elementos.Single(e => e.Id == Fogo).Should().Match<ElementoDaFichaResponse>(e => e.Pontos == 2 && e.Experiencia == null);
    }

    [Fact]
    public async Task Review_Focus_2_tirar_o_ultimo_ponto_de_Ar_com_Gelo_e_recusado_e_depois_de_zerar_Gelo_passa()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);
        await PutOkAsync(f, Ar, 1);
        await PutOkAsync(f, Agua, 1);
        await PutOkAsync(f, Gelo, 1);

        await DeveRecusarAsync(await PutAsync(f, f.Token, Ar, 0), "Gelo depende de Ar: zere Gelo antes.");
        (await GetAsync(f)).Elementos.Single(e => e.Id == Ar).Pontos.Should().Be(1);

        await PutOkAsync(f, Gelo, 0);
        await PutOkAsync(f, Ar, 0);
        (await GetAsync(f)).Gasto.Should().Be(1);
    }

    [Fact]
    public async Task Review_Focus_3_requisito_maior_nao_tira_pontos_marca_o_Gelo_e_so_aceita_descer()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);
        await PutOkAsync(f, Ar, 1);
        await PutOkAsync(f, Agua, 1);
        await PutOkAsync(f, Gelo, 2);

        await ComCatalogoAsync(
            db => db.CruzamentosDeAfinidade.Where(c => c.SubElementoId == Gelo).ExecuteUpdateAsync(c => c.SetProperty(x => x.MinimoB, 2)),
            db => db.CruzamentosDeAfinidade.Where(c => c.SubElementoId == Gelo).ExecuteUpdateAsync(c => c.SetProperty(x => x.MinimoB, 1)),
            async () =>
            {
                var afinidades = await GetAsync(f);
                afinidades.Gasto.Should().Be(4);
                afinidades.Elementos.Single(e => e.Id == Gelo).Should().Match<ElementoDaFichaResponse>(e =>
                    e.Pontos == 2 && e.Bloqueado && e.Motivo == "Precisa de 1 em Água e 2 em Ar");

                await DeveRecusarAsync(await PutAsync(f, f.Token, Gelo, 3), "Precisa de 1 em Água e 2 em Ar");
                await PutOkAsync(f, Gelo, 1);
                // Já inconsistente: baixar Ar também passa (o requisito não estava cumprido antes).
                await PutOkAsync(f, Ar, 0);
            });
    }

    [Fact]
    public async Task Review_Focus_5_Campeao_sem_Afinidade_Adicional_nem_coracao_de_mana_nao_mostra_a_secao()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Campeao, afinidadeAdicional: 0);

        var afinidades = await GetAsync(f);

        (afinidades.SecaoVisivel, afinidades.Maximo).Should().Be((false, 0));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public async Task Review_Focus_5_Campeao_com_Afinidade_Adicional_ou_coracao_de_mana_ve_todos_os_elementos_sem_filtro(int afinidadeAdicional, bool coracaoDeMana)
    {
        var f = await FichaDeJogadorAsync(Vocacao.Campeao, afinidadeAdicional, coracaoDeMana);

        var afinidades = await GetAsync(f);

        afinidades.SecaoVisivel.Should().BeTrue();
        afinidades.Maximo.Should().Be(afinidadeAdicional); // marcial: só a Afinidade Adicional
        afinidades.Elementos.Should().HaveCount(19);
        afinidades.Elementos.Where(e => e.Tipo != "SubElemento").Should().OnlyContain(e => !e.Bloqueado);
        afinidades.Elementos.Single(e => e.Id == Necromancia).Motivo.Should().Be("Precisa de 1 em Fogo e 1 em Mundano");
    }

    [Fact]
    public async Task Um_elemento_nao_passa_do_teto()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);

        await ComCatalogoAsync(
            db => db.ElementosDeAfinidade.Where(e => e.Id == Fogo).ExecuteUpdateAsync(e => e.SetProperty(x => x.Teto, 2)),
            db => db.ElementosDeAfinidade.Where(e => e.Id == Fogo).ExecuteUpdateAsync(e => e.SetProperty(x => x.Teto, (int?)null)),
            async () =>
            {
                await DeveRecusarAsync(await PutAsync(f, f.Token, Fogo, 3), "Fogo tem teto de 2 pontos.");
                await PutOkAsync(f, Fogo, 2);
                (await GetAsync(f)).Elementos.Single(e => e.Id == Fogo).Teto.Should().Be(2);
            });
    }

    [Fact]
    public async Task O_gasto_nao_passa_do_maximo_e_baixar_o_maximo_so_deixa_tirar_pontos()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 0);
        await PutOkAsync(f, Fogo, 3);

        await DeveRecusarAsync(await PutAsync(f, f.Token, Ar, 1), "Pontos acima do máximo de 3.");

        await DefinirAsync(f, Vocacao.Campeao, afinidadeAdicional: 1); // máximo 1, gasto 3: baixar o máximo não é bloqueado
        await DeveRecusarAsync(await PutAsync(f, f.Token, Ar, 1), "Pontos acima do máximo de 1.");
        await PutOkAsync(f, Fogo, 2);
        (await GetAsync(f)).Should().Match<AfinidadesDaFichaResponse>(a => a.Gasto == 2 && a.Maximo == 1);
    }

    [Fact]
    public async Task O_total_do_Circulo_vem_da_tabela_do_Auditor()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 1);

        await ComCatalogoAsync(
            db => db.TotaisDeAfinidadePorCirculo.Where(t => t.Circulo == 0).ExecuteUpdateAsync(t => t.SetProperty(x => x.Total, 8)),
            db => db.TotaisDeAfinidadePorCirculo.Where(t => t.Circulo == 0).ExecuteUpdateAsync(t => t.SetProperty(x => x.Total, 3)),
            async () => (await GetAsync(f)).Maximo.Should().Be(9));
    }

    [Fact]
    public async Task A_visibilidade_vem_da_Vocacao_ou_da_Classe()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10, classe: "Elementalista");
        await PutOkAsync(f, Fogo, 1);

        await ComCatalogoAsync(
            db => db.VisibilidadesDeElemento.Where(v => v.Chave == "Feiticeiro" && v.ElementoId == Necromancia).ExecuteDeleteAsync(),
            async db =>
            {
                await db.VisibilidadesDeElemento.Where(v => v.Chave == "Elementalista").ExecuteDeleteAsync();
                db.VisibilidadesDeElemento.Add(new VisibilidadeDeElemento { Origem = OrigemDaVisibilidade.Vocacao, Chave = "Feiticeiro", ElementoId = Necromancia });
                await db.SaveChangesAsync();
            },
            async () =>
            {
                (await GetAsync(f)).Elementos.Should().NotContain(e => e.Id == Necromancia);
                await DeveRecusarAsync(await PutAsync(f, f.Token, Necromancia, 1), "Necromancia não está disponível para esta Vocação/Classe.");

                await using (var scope = Factory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
                    db.VisibilidadesDeElemento.Add(new VisibilidadeDeElemento { Origem = OrigemDaVisibilidade.Classe, Chave = "Elementalista", ElementoId = Necromancia });
                    await db.SaveChangesAsync();
                }
                (await GetAsync(f)).Elementos.Should().Contain(e => e.Id == Necromancia);
            });
    }

    [Fact]
    public async Task Um_elemento_fora_da_Vocacao_com_pontos_aparece_marcado_e_so_desce()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);
        await InserirPontosAsync(f, (Alma, 2));
        await DefinirAsync(f, Vocacao.Bruxo, afinidadeAdicional: 10);

        await ComCatalogoAsync(
            db => db.VisibilidadesDeElemento.Where(v => v.Chave == "Bruxo" && v.ElementoId == Alma).ExecuteDeleteAsync(),
            async db =>
            {
                db.VisibilidadesDeElemento.Add(new VisibilidadeDeElemento { Origem = OrigemDaVisibilidade.Vocacao, Chave = "Bruxo", ElementoId = Alma });
                await db.SaveChangesAsync();
            },
            async () =>
            {
                (await GetAsync(f)).Elementos.Single(e => e.Id == Alma).Should().Match<ElementoDaFichaResponse>(e =>
                    e.Pontos == 2 && e.Bloqueado && e.Motivo == "Indisponível para esta Vocação/Classe");
                await DeveRecusarAsync(await PutAsync(f, f.Token, Alma, 3), "Alma não está disponível para esta Vocação/Classe.");
                await PutOkAsync(f, Alma, 1);
            });
    }

    [Fact]
    public async Task A_Experiencia_muda_sem_mexer_nos_pontos_mesmo_num_elemento_bloqueado()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 0);
        await InserirPontosAsync(f, (Gelo, 2)); // sem Ar nem Água: Gelo bloqueado

        var response = await PutAsync(f, f.Token, Gelo, 2, experiencia: 7);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync(f)).Elementos.Single(e => e.Id == Gelo).Should().Match<ElementoDaFichaResponse>(e => e.Pontos == 2 && e.Experiencia == 7 && e.Bloqueado);
    }

    protected async Task<int> LinhasNoBancoAsync(FichaDeTeste f, int elemento)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var id = Guid.Parse(f.SheetId);
        return await db.CharacterPontosDeAfinidade.CountAsync(p => p.SheetId == id && p.ElementoId == elemento)
            + await db.NpcPontosDeAfinidade.CountAsync(p => p.SheetId == id && p.ElementoId == elemento);
    }

    [Fact]
    public async Task O_jogador_nao_grava_nada_num_elemento_que_a_lista_esconde()
    {
        var f = await FichaDeJogadorAsync(vocacao: null, afinidadeAdicional: 10); // sem Vocação: só os 4 base

        await DeveRecusarAsync(await PutAsync(f, f.Token, Gelo, 0, experiencia: 5), "Gelo não está disponível para esta Vocação/Classe.");

        (await LinhasNoBancoAsync(f, Gelo)).Should().Be(0);
        (await GetAsync(f)).Elementos.Should().NotContain(e => e.Id == Gelo);
    }

    [Fact]
    public async Task PUTs_simultaneos_num_elemento_novo_gravam_uma_linha_so_e_nenhum_da_erro()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 100);

        var respostas = await Task.WhenAll(Enumerable.Range(1, 8).Select(i => PutAsync(f, f.Token, Fogo, i, experiencia: i)));

        respostas.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.OK);
        (await LinhasNoBancoAsync(f, Fogo)).Should().Be(1);
    }

    [Theory]
    [InlineData(Fogo, -1, null, "O valor não pode ser negativo.")]
    [InlineData(99, 1, null, "Elemento inexistente.")]
    [InlineData(Fogo, 1, -1, "A Experiência não pode ser negativa.")]
    public async Task Valores_invalidos_sao_recusados(int elemento, int pontos, int? experiencia, string texto)
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);

        await DeveRecusarAsync(await PutAsync(f, f.Token, elemento, pontos, experiencia), texto);
    }

    [Fact]
    public async Task Sem_acesso_a_leitura_e_404_e_a_escrita_o_status_negado()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);
        var intruso = await Tipo.CriarIntrusoAsync(this, f);

        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "afinidades"), intruso))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutAsync(f, intruso, Fogo, 1)).StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task O_cabecalho_da_ficha_mostra_o_mesmo_gasto_e_maximo()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 2);
        await PutOkAsync(f, Fogo, 2);
        await PutOkAsync(f, Terra, 1);

        var ficha = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token));
        var json = await ficha.Content.ReadFromJsonAsync<JsonElement>();

        (json.GetProperty("vocacaoArcanaGasta").GetInt32(), json.GetProperty("vocacaoArcanaMaxima").GetInt32()).Should().Be((3, 5));
    }

    // Ficha de Personagem 2.b: Eficiência e Dano Elemental vêm da Tabela de Afinidades com os pontos do elemento
    // escolhido como Afinidade (7 pontos → linha 7: Eficiência 4, Dano 3).
    [Theory]
    [InlineData(Gelo)] // um Sub-Elemento
    [InlineData(Agua)] // um elemento que na lista antiga só aparecia como Essência 2
    public async Task Eficiencia_e_Dano_Elemental_saem_dos_pontos_do_elemento_escolhido_como_Afinidade(int afinidade)
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);
        await InserirPontosAsync(f, (afinidade, 7), (Fogo, 2));
        await DefinirAsync(f, Vocacao.Feiticeiro, afinidadeAdicional: 10, afinidade: afinidade);

        var sub = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token));
        var body = (await sub.Content.ReadFromJsonAsync<SubAttributesResponse>())!;

        (body.EficienciaElemental, body.DanoElemental).Should().Be((4, 3));
    }

    [Fact]
    public async Task Sem_pontos_no_elemento_da_Afinidade_Eficiencia_e_Dano_Elemental_valem_0()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 10);
        await InserirPontosAsync(f, (Fogo, 7));
        await DefinirAsync(f, Vocacao.Feiticeiro, afinidadeAdicional: 10, afinidade: Gelo);

        var body = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token)))
            .Content.ReadFromJsonAsync<SubAttributesResponse>())!;

        (body.EficienciaElemental, body.DanoElemental).Should().Be((0, 0));
    }
}

public sealed class AfinidadesDaFichaTestesPersonagem(ApiFixture fixture) : AfinidadesDaFichaTestes<TipoDeTestePersonagem>(fixture)
{
    protected override async Task<IFichaComAfinidades> FichaNoBancoAsync(RuinaRpgDbContext db, Guid id) => (await db.CharacterSheets.FindAsync(id))!;

    protected override IPontosDeAfinidadeDaFicha NovosPontos() => new CharacterPontosDeAfinidade();

    [Fact]
    public async Task O_Mestre_da_campanha_tambem_segue_as_regras_no_Personagem()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 0);

        await DeveRecusarAsync(await PutAsync(f, f.GmToken, Fogo, 4), "Pontos acima do máximo de 3.");
    }
}

public sealed class AfinidadesDaFichaTestesNpc(ApiFixture fixture) : AfinidadesDaFichaTestes<TipoDeTesteNpc>(fixture)
{
    protected override async Task<IFichaComAfinidades> FichaNoBancoAsync(RuinaRpgDbContext db, Guid id) => (await db.NpcSheets.FindAsync(id))!;

    protected override IPontosDeAfinidadeDaFicha NovosPontos() => new NpcPontosDeAfinidade();

    /// <summary>Ficha de NPCs R0010: o GM não é bloqueado por nenhuma regra de 2.c; a ficha só mostra.</summary>
    [Fact]
    public async Task O_GM_do_NPC_nao_e_recusado_pelas_regras_e_a_ficha_mostra_o_que_ficou_fora_delas()
    {
        var f = await NovaFichaAsync();
        await DefinirAsync(f, Vocacao.Feiticeiro, afinidadeAdicional: 0);

        await ComCatalogoAsync(
            db => db.ElementosDeAfinidade.Where(e => e.Id == Fogo).ExecuteUpdateAsync(e => e.SetProperty(x => x.Teto, 2)),
            db => db.ElementosDeAfinidade.Where(e => e.Id == Fogo).ExecuteUpdateAsync(e => e.SetProperty(x => x.Teto, (int?)null)),
            async () =>
            {
                await PutOkAsync(f, Fogo, 5);  // acima do teto e do máximo
                await PutOkAsync(f, Gelo, 1);  // sem o requisito
                await PutOkAsync(f, Ar, 1);
                await PutOkAsync(f, Agua, 1);
                await PutOkAsync(f, Ar, 0);    // baixar o requisito de um Sub-Elemento com pontos

                var afinidades = await GetAsync(f);
                (afinidades.Gasto, afinidades.Maximo).Should().Be((7, 3));
                afinidades.Elementos.Single(e => e.Id == Fogo).Motivo.Should().Be("Acima do teto (2)");
                afinidades.Elementos.Single(e => e.Id == Gelo).Bloqueado.Should().BeTrue();
            });
    }

    /// <summary>Nenhuma linha fica escondida: o que o GM grava num elemento fora da Vocação aparece, marcado.</summary>
    [Fact]
    public async Task A_Experiencia_que_o_GM_grava_num_elemento_escondido_aparece_marcada_na_lista()
    {
        var f = await NovaFichaAsync();
        await DefinirAsync(f, vocacao: null, afinidadeAdicional: 0);

        await PutOkAsync(f, Gelo, 0, experiencia: 5);

        (await GetAsync(f)).Elementos.Single(e => e.Id == Gelo).Should().Match<ElementoDaFichaResponse>(e =>
            e.Pontos == 0 && e.Experiencia == 5 && e.Bloqueado && e.Motivo == "Indisponível para esta Vocação/Classe");
    }

    [Fact]
    public async Task O_GM_de_um_NPC_concedido_tambem_nao_e_recusado_mas_o_jogador_e()
    {
        var f = await FichaDeJogadorAsync(Vocacao.Feiticeiro, afinidadeAdicional: 0);

        await DeveRecusarAsync(await PutAsync(f, f.Token, Fogo, 4), "Pontos acima do máximo de 3.");
        (await PutAsync(f, f.GmToken, Fogo, 4)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync(f)).Gasto.Should().Be(4);
    }
}
