using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Infrastructure.Afinidades;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Tests.Integration.Controllers;

namespace RuinaRPG.Tests.Integration.Afinidades;

/// <summary>
/// O catálogo único de elementos (Auditoria de Regras R0018): GET api/afinidades/catalogo e o uso dele pela Afinidade da
/// Criatura, pelos alvos das Condições e pelos requisitos das Passivas — um elemento criado aparece em todos.
/// </summary>
public class CatalogoDeElementosTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<List<ElementoDeAfinidadeResponse>> CatalogoAsync(string token) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/afinidades/catalogo", token))).Content
            .ReadFromJsonAsync<List<ElementoDeAfinidadeResponse>>())!;

    /// <summary>Um elemento como o Auditor o criaria (a página da Auditoria vem depois): o id segue a sequência.</summary>
    private async Task<int> CriarElementoAsync(string nome, TipoDeElemento tipo)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var elemento = new ElementoDeAfinidade { Nome = nome, Tipo = tipo };
        db.ElementosDeAfinidade.Add(elemento);
        await db.SaveChangesAsync();
        return elemento.Id;
    }

    private async Task RenomearElementoAsync(int id, string nome)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.ElementosDeAfinidade.FindAsync(id))!.Nome = nome;
        await db.SaveChangesAsync();
    }

    private async Task<string> CriarCriaturaAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    private Task<HttpResponseMessage> CriarCondicaoAsync(string token, string nome, string alvo, string? especifico) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/condicoes", token,
            new SalvarCondicaoRequest(nome, null, "Veneno", null, [new CondicaoModificadorDto(alvo, especifico, "Fixo", 2, null)], [])));

    [Fact]
    public async Task O_catalogo_exige_autenticacao()
    {
        var response = await Client.GetAsync("/api/afinidades/catalogo");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task O_catalogo_lista_os_19_elementos_semeados_com_id_nome_tipo_e_teto()
    {
        var token = await RegisterGmAndGetTokenAsync("CatElemGm1", "catelem1@teste.com");

        var catalogo = await CatalogoAsync(token);

        // Os testes da classe dividem o banco: um elemento criado por outro teste segue depois dos 19 (id ≥ 19).
        catalogo.Where(e => e.Id <= 18).Select(e => e.Id).Should().Equal(Enumerable.Range(0, 19));
        catalogo.Should().ContainEquivalentOf(new ElementoDeAfinidadeResponse(1, "Água", "Base", null));
        catalogo.Should().ContainEquivalentOf(new ElementoDeAfinidadeResponse(17, "Invocação", "SubElemento", null));
        catalogo.Should().ContainEquivalentOf(new ElementoDeAfinidadeResponse(18, "Mundano", "Caminho", null));
        catalogo.Select(e => e.Id).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Elemento_criado_aparece_nos_pickers()
    {
        var gm = await RegisterGmAndGetTokenAsync("CatElemGm2", "catelem2@teste.com");
        await GrantRulesAuditorAsync("catelem2@teste.com");
        var luz = await CriarElementoAsync("Luz", TipoDeElemento.SubElemento);

        // A lista que os pickers do cliente leem.
        (await CatalogoAsync(gm)).Should().ContainEquivalentOf(new ElementoDeAfinidadeResponse(luz, "Luz", "SubElemento", null));

        // Afinidade da Criatura (1.a).
        var criatura = await CriarCriaturaAsync(gm);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{criatura}", gm,
            CreatureSheetsControllerTests.ValidUpdate() with { Afinidade = luz }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var lida = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{criatura}", gm))).Content.ReadFromJsonAsync<CreatureSheetResponse>();
        lida!.Afinidade.Should().Be(luz);

        // Alvo de uma Condição (Afinidade elemental e Dano elemental).
        (await CriarCondicaoAsync(gm, "Iluminado", "AfinidadeElemental", luz.ToString())).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CriarCondicaoAsync(gm, "Radiante", "DanoElemental", luz.ToString())).StatusCode.Should().Be(HttpStatusCode.Created);

        // Requisito de uma Passiva, com o nome do catálogo no texto; renomear o elemento muda o texto.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gm,
            new CreateSpellAbilityEntryRequest("Aura de Luz", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Afinidade: luz)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        async Task<List<string>> RequisitosDaPassivaAsync() =>
            (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook/passivas", gm))).Content
                .ReadFromJsonAsync<List<PassivaDoLivroResponse>>())!.Single(p => p.Nome == "Aura de Luz").Requisitos;
        (await RequisitosDaPassivaAsync()).Should().Equal("Afinidade: Luz");

        await RenomearElementoAsync(luz, "Luz Sagrada");
        (await RequisitosDaPassivaAsync()).Should().Equal("Afinidade: Luz Sagrada");
    }

    [Theory]
    [InlineData("AfinidadeElemental", "Fogo")] // o nome antigo não vale mais: o alvo é o id do catálogo
    [InlineData("AfinidadeElemental", "9")]    // Alma é Caminho
    [InlineData("DanoElemental", "999")]       // fora do catálogo
    public async Task Uma_Condicao_so_aceita_o_id_de_um_elemento_que_nao_e_Caminho(string alvo, string especifico)
    {
        var gm = await RegisterGmAndGetTokenAsync($"CatElemGm3{especifico}", $"catelem3{especifico.ToLowerInvariant()}@teste.com");
        await GrantRulesAuditorAsync($"catelem3{especifico.ToLowerInvariant()}@teste.com");

        (await CriarCondicaoAsync(gm, $"Recusada {especifico}", alvo, especifico)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Uma_Passiva_recusa_um_elemento_fora_do_catalogo()
    {
        var gm = await RegisterGmAndGetTokenAsync("CatElemGm4", "catelem4@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gm,
            new CreateSpellAbilityEntryRequest("Aura Nenhuma", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Afinidade: 999))));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
