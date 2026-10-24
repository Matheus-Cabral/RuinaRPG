using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Fichas;
// Os pedidos de Maestria, Perícia e Atributo têm o mesmo JSON nos três tipos de ficha.
using AdicionarMaestria = RuinaRPG.Contracts.Fichas.AddMasteryRequest;
using AtualizarAtributo = RuinaRPG.Contracts.Fichas.UpdateAttributeRequest;
using AtualizarPericia = RuinaRPG.Contracts.Fichas.UpdateSkillRequest;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>Maestrias da ficha (<c>/masteries</c>), nos três tipos.</summary>
public abstract class MaestriasDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    [Fact]
    public async Task Add_a_valid_mastery_returns_201()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AdicionarMaestria("Maestria em Pontaria", "Pontaria", "Destreza", 3)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "masteries"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<MaestriaLida>>();
        body!.Should().ContainSingle(m => m.Nome == "Maestria em Pontaria" && m.Pericia == "Pontaria" && m.Atributo == "Destreza" && m.GastoMaestria == 3);
    }

    [Fact]
    public async Task List_returns_the_masteries_on_the_sheet()
    {
        var f = await NovaFichaAsync();

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AdicionarMaestria("Maestria em Furtividade", "Furtividade", "Agilidade", 2)));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "masteries"), f.Token));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<MaestriaLida>>();
        body!.Should().ContainSingle(m => m.Nome == "Maestria em Furtividade" && m.Pericia == "Furtividade" && m.Atributo == "Agilidade" && m.GastoMaestria == 2);
    }

    [Fact]
    public async Task Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list()
    {
        var f = await NovaFichaAsync();

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AdicionarMaestria("Maestria em Investigação", "Investigacao", "Astucia", 1)));
        var added = await addResponse.Content.ReadFromJsonAsync<MaestriaLida>();

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"masteries/{added!.Id}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "masteries"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<MaestriaLida>>();
        body!.Should().NotContain(m => m.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_someone_without_access_returns_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var intruso = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), intruso,
            new AdicionarMaestria("Maestria em Atletismo", "Atletismo", "Vigor", 1)));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task Add_computes_Total_from_the_matching_skill_and_attribute_rows()
    {
        var f = await NovaFichaAsync();

        // Bruto[Pontaria] = Modificador(9) = 3
        var skillUpdate = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Pontaria"), f.GmToken,
            new AtualizarPericia(9, null)));
        skillUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // AtributoTotal[Destreza] = 4 (Gasto 4, Bonus 0, sem maestria)
        var attributeUpdate = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Destreza"), f.GmToken,
            new AtualizarAtributo(4, 0, false)));
        attributeUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AdicionarMaestria("Maestria em Pontaria", "Pontaria", "Destreza", 5)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<MaestriaLida>();

        // Total = GastoMaestria (5) + Bruto[Pontaria] (3) + AtributoTotal[Destreza] (4) = 12
        added!.Total.Should().Be(12);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "masteries"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<MaestriaLida>>();
        body!.Should().ContainSingle(m => m.Id == added.Id && m.Total == 12);
    }

    [Fact]
    public async Task Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list()
    {
        // A numeric string satisfies Enum.TryParse but isn't a real Pericia/Atributo value - without
        // an Enum.IsDefined check, the row would be saved before ComputeTotalAsync runs and then every
        // later GET .../masteries would 500 forever trying to look up a skill/attribute row that
        // doesn't exist for that undefined enum value.
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AdicionarMaestria("Maestria Inválida", "999", "Destreza", 3)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "masteries"), f.Token));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<MaestriaLida>>();
        body!.Should().BeEmpty();
    }
}

/// <summary>O bônus do Histórico, que só Personagem e NPC têm.</summary>
public abstract class MaestriasDePersonagemOuNpcTestes<TTipo>(ApiFixture fixture) : MaestriasDaFichaTestes<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    [Fact]
    public async Task Add_a_mastery_includes_the_Historico_bonus_in_Total()
    {
        var f = await NovaFichaAsync();
        await DefinirHistoricoAsync(f, Tipo, "Teste de Bônus Maestria", "Pontaria", "Furtividade");
        // Pontaria Gasto 9, Histórico bonus 6 counts as Pericia points -> Modificador (9+6)/3 = 5.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Pontaria"), f.Token,
            new AtualizarPericia(9, null)));
        // Destreza Gasto 4, no bônus/maestria/artefato -> Total 4.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Destreza"), f.Token,
            new AtualizarAtributo(4, 0, false)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AdicionarMaestria("Teste Maestria Bônus", "Pontaria", "Destreza", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MaestriaLida>();
        body!.Total.Should().Be(11); // gastoMaestria 2 + bruto ((9+6)/3=5) + atributoTotal 4
    }
}

/// <summary>Só o Personagem edita uma Maestria (PUT); NPC e Criatura apagam e adicionam de novo.</summary>
public sealed class MaestriasDaFichaTestesPersonagem(ApiFixture fixture) : MaestriasDePersonagemOuNpcTestes<TipoDeTestePersonagem>(fixture)
{
    [Fact]
    public async Task Update_an_existing_mastery_returns_200_and_recomputes_Total()
    {
        var f = await NovaFichaAsync();

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AddMasteryRequest("Maestria em Pontaria", "Pontaria", "Destreza", 3)));
        var added = await addResponse.Content.ReadFromJsonAsync<MasteryResponse>();

        // Bruto[Furtividade] = Modificador(9) = 3; AtributoTotal[Agilidade] = 4 (Gasto 4, sem maestria)
        var skillUpdate = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Furtividade"), f.GmToken,
            new UpdateSkillRequest(9, null)));
        skillUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var attributeUpdate = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Agilidade"), f.GmToken,
            new UpdateAttributeRequest(4, 0, false)));
        attributeUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"masteries/{added!.Id}"), f.Token,
            new UpdateCharacterMasteryRequest("Maestria em Furtividade", "Furtividade", "Agilidade", 5)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<MasteryResponse>();
        updated!.Nome.Should().Be("Maestria em Furtividade");
        updated.Pericia.Should().Be("Furtividade");
        updated.Atributo.Should().Be("Agilidade");
        updated.GastoMaestria.Should().Be(5);
        // Total = GastoMaestria (5) + Bruto[Furtividade] (3) + AtributoTotal[Agilidade] (4) = 12
        updated.Total.Should().Be(12);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "masteries"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<MasteryResponse>>();
        body!.Should().ContainSingle(m => m.Id == added.Id && m.Total == 12);
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var f = await NovaFichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AddMasteryRequest("Maestria em Atletismo", "Atletismo", "Vigor", 1)));
        var added = await addResponse.Content.ReadFromJsonAsync<MasteryResponse>();

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"masteries/{added!.Id}"), otherToken,
            new UpdateCharacterMasteryRequest("Maestria Alheia", "Atletismo", "Vigor", 2)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_a_nonexistent_mastery_returns_404()
    {
        var f = await NovaFichaAsync();

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"masteries/{Guid.NewGuid()}"), f.Token,
            new UpdateCharacterMasteryRequest("Maestria Inexistente", "Atletismo", "Vigor", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_change_the_stored_row()
    {
        var f = await NovaFichaAsync();

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AddMasteryRequest("Maestria em Pontaria", "Pontaria", "Destreza", 3)));
        var added = await addResponse.Content.ReadFromJsonAsync<MasteryResponse>();

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"masteries/{added!.Id}"), f.Token,
            new UpdateCharacterMasteryRequest("Maestria Inválida", "999", "Destreza", 3)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "masteries"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<MasteryResponse>>();
        body!.Should().ContainSingle(m => m.Id == added.Id && m.Pericia == "Pontaria" && m.GastoMaestria == 3);
    }
}

public sealed class MaestriasDaFichaTestesNpc(ApiFixture fixture) : MaestriasDePersonagemOuNpcTestes<TipoDeTesteNpc>(fixture);

public sealed class MaestriasDaFichaTestesCriatura(ApiFixture fixture) : MaestriasDaFichaTestes<TipoDeTesteCriatura>(fixture)
{
    // Additional Criatura-only guard: Alquimia is a real Pericia enum value but not in the R0005
    // 20-skill allow-list, so no CreatureSkill row exists for it — without this check, Add would
    // 500 trying to SingleAsync a skill row that was never seeded.
    [Fact]
    public async Task Add_with_a_disallowed_Pericia_returns_400_and_does_not_poison_the_list()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "masteries"), f.Token,
            new AdicionarMaestria("Maestria em Alquimia", "Alquimia", "Destreza", 3)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "masteries"), f.Token));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<MaestriaLida>>();
        body!.Should().BeEmpty();
    }
}
