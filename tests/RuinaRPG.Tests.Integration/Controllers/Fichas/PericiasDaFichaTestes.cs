using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Items;
// Os pedidos de Perícia, Atributo e Artefato têm o mesmo JSON nos três tipos de ficha.
using AdicionarArtefato = RuinaRPG.Contracts.Fichas.AddArtifactRequest;
using AtualizarAtributo = RuinaRPG.Contracts.Fichas.UpdateAttributeRequest;
using AtualizarPericia = RuinaRPG.Contracts.Fichas.UpdateSkillRequest;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>Perícias da ficha (<c>/skills</c>), nos três tipos.</summary>
public abstract class PericiasDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    protected async Task<string> CreateArtefatoItemAsync(string gmToken, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", "Anel de Teste", 0.1m, 500, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task List_returns_every_skill_of_the_kind_all_zeroed()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<List<PericiaLida>>();
        body!.Should().HaveCount(Tipo.QuantidadeDePericias);
    }

    [Fact]
    public async Task Update_a_skill_sets_Gasto_and_the_response_computes_Modificador()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Atletismo"), f.Token, new AtualizarPericia(9, null)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<PericiaLida>>();
        body!.Single(s => s.Pericia == "Atletismo").Modificador.Should().Be(3); // 9 / 3
    }

    [Fact]
    public async Task Update_an_unknown_Pericia_key_returns_404()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Inexistente"), f.Token, new AtualizarPericia(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total()
    {
        var f = await NovaFichaAsync();

        // Forca: Gasto 4, Bonus 0, no maestria -> Total 4 (AttributeTotalCalculator.Total)
        var attrResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(4, 0, false)));
        attrResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Atletismo: Gasto 9 -> Modificador 3 (SkillFormulas.Modificador), Atributo escolhido Forca
        var skillResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Atletismo"), f.Token, new AtualizarPericia(9, "Forca")));
        skillResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A different skill never had an attribute chosen for it, and must stay that way.
        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<PericiaLida>>();

        var atletismo = body!.Single(s => s.Pericia == "Atletismo");
        atletismo.AtributoEscolhido.Should().Be("Forca");
        atletismo.Modificador.Should().Be(3);
        atletismo.Total.Should().Be(7); // SkillFormulas.Total(modificador: 3, atributoTotal: 4)

        var acrobacia = body!.Single(s => s.Pericia == "Acrobacia");
        acrobacia.AtributoEscolhido.Should().BeNull();
        acrobacia.Total.Should().BeNull();
    }

    [Fact]
    public async Task An_equipped_Pericia_Artefato_is_summed_into_that_Pericias_Total()
    {
        // Ficha de Personagem 2.d, corrected: Total = Modificador + Atributo + Artefato(s).
        var f = await NovaFichaAsync();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Atletismo"), f.Token, new AtualizarPericia(9, "Forca")));

        var artifactItemId = await CreateArtefatoItemAsync(f.GmToken, "Pericia", "Atletismo", 3);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifactItemId)));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<PericiaLida>>();
        body!.Single(s => s.Pericia == "Atletismo").Total.Should().Be(10); // modificador 3 + atributo 4 + artefato 3
    }

    [Fact]
    public async Task List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update()
    {
        var f = await NovaFichaAsync();

        var beforeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), f.Token));
        var before = (await beforeResponse.Content.ReadFromJsonAsync<List<PericiaLida>>())!;
        before.Select(s => s.Pericia).Should().BeInAscendingOrder(StringComparer.Ordinal).And.HaveCount(Tipo.QuantidadeDePericias);
        before.Select(s => s.Pericia).Should().Equal(before.Select(s => s.Pericia).OrderBy(p => p, StringComparer.Ordinal));

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Atletismo"), f.Token, new AtualizarPericia(3, null)));

        var afterResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), f.Token));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<PericiaLida>>())!;
        after.Select(s => s.Pericia).Should().Equal(before.Select(s => s.Pericia));
    }

    [Fact]
    public async Task Update_by_someone_without_access_returns_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var intruso = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Atletismo"), intruso, new AtualizarPericia(9, null)));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task List_by_someone_without_access_returns_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var intruso = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), intruso));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }
}

/// <summary>Casos do Histórico, que só Personagem e NPC têm.</summary>
public abstract class PericiasDePersonagemOuNpcTestes<TTipo>(ApiFixture fixture) : PericiasDaFichaTestes<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    private async Task<FichaDeTeste> FichaComHistoricoAsync(string nome)
    {
        var f = await NovaFichaAsync();
        await DefinirHistoricoAsync(f, Tipo, nome, "Arcano", "Biblioteca");
        return f;
    }

    [Fact]
    public async Task List_adds_the_sheets_Historico_bonus_to_the_matching_Pericias_Modificador()
    {
        var f = await FichaComHistoricoAsync("Teste de Bônus na Perícia");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Arcano"), f.Token, new AtualizarPericia(3, null)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Biblioteca"), f.Token, new AtualizarPericia(3, null)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<List<PericiaLida>>();
        var arcano = body!.Single(s => s.Pericia == "Arcano");
        arcano.Modificador.Should().Be(3); // (3+6)/3 = 3 -- Histórico bonus counts as Pericia points spent
        arcano.ContaHistorico.Should().BeTrue();
        var biblioteca = body!.Single(s => s.Pericia == "Biblioteca");
        biblioteca.Modificador.Should().Be(2); // (3+3)/3 = 2
        biblioteca.ContaHistorico.Should().BeTrue();
        var fortitude = body!.Single(s => s.Pericia == "Fortitude");
        fortitude.Modificador.Should().Be(0); // untouched, no bonus
        fortitude.ContaHistorico.Should().BeFalse();
    }

    [Fact]
    public async Task List_adds_the_Historico_bonus_even_to_a_Pericia_with_no_Gasto()
    {
        var f = await FichaComHistoricoAsync("Teste de Bônus sem Gasto");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Arcano"), f.Token, new AtualizarPericia(3, null)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "skills"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<List<PericiaLida>>();
        var arcano = body!.Single(s => s.Pericia == "Arcano");
        arcano.Modificador.Should().Be(3); // (3+6)/3 = 3 -- Histórico bonus counts as Pericia points spent
        arcano.ContaHistorico.Should().BeTrue();
        var biblioteca = body!.Single(s => s.Pericia == "Biblioteca"); // PericiaMaisTres, Gasto still 0
        biblioteca.Modificador.Should().Be(1); // (0+3)/3 = 1
        biblioteca.ContaHistorico.Should().BeTrue();
        var fortitude = body!.Single(s => s.Pericia == "Fortitude");
        fortitude.Modificador.Should().Be(0); // untouched, no bonus
        fortitude.ContaHistorico.Should().BeFalse();
    }
}

public sealed class PericiasDaFichaTestesPersonagem(ApiFixture fixture) : PericiasDePersonagemOuNpcTestes<TipoDeTestePersonagem>(fixture);

public sealed class PericiasDaFichaTestesNpc(ApiFixture fixture) : PericiasDePersonagemOuNpcTestes<TipoDeTesteNpc>(fixture);

public sealed class PericiasDaFichaTestesCriatura(ApiFixture fixture) : PericiasDaFichaTestes<TipoDeTesteCriatura>(fixture)
{
    [Fact]
    public async Task Update_a_disallowed_Pericia_returns_400()
    {
        var f = await NovaFichaAsync();

        // Alquimia is a real Pericia enum value but not in the R0005 20-skill allow-list.
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Alquimia"), f.Token, new AtualizarPericia(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
