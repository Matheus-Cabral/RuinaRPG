using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
// Os pedidos de Atributo e Artefato têm o mesmo JSON nos três tipos de ficha.
using AdicionarArtefato = RuinaRPG.Contracts.CharacterSheets.AddCharacterArtifactRequest;
using AtualizarAtributo = RuinaRPG.Contracts.CharacterSheets.UpdateCharacterAttributeRequest;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>Atributos da ficha e o orçamento de pontos (<c>/attributes</c>), nos três tipos.</summary>
public abstract class AtributosDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    [Fact]
    public async Task List_returns_every_attribute_of_the_kind_all_zeroed()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<List<AtributoLido>>();
        body!.Should().HaveCount(Tipo.AtributosEmOrdem.Length);
        body!.Should().OnlyContain(a => a.Total == 0);
    }

    [Fact]
    public async Task Update_an_attribute_by_the_owner_recomputes_Total()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(5, 3, false)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<AtributoLido>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(6); // 5 + floor(3/2) + 0
    }

    [Fact]
    public async Task Update_an_attribute_by_the_player_the_sheet_was_granted_to_recomputes_Total()
    {
        // No Personagem, a ficha do próprio Jogador; no NPC/Criatura, uma ficha concedida a ele.
        var f = await Tipo.CriarFichaDeJogadorAsync(this);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(5, 3, false)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<AtributoLido>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(6); // 5 + floor(3/2)
    }

    [Fact]
    public async Task Total_sums_the_Valor_of_equipped_Artefatos_targeting_that_Atributo()
    {
        // Ficha de Personagem 2.a: "Artefatos refere-se à soma dos Valores de Artefatos equipados
        // cujo Tipo é Atributo e cujo Alvo é este atributo."
        var f = await NovaFichaAsync();

        var forcaArtifactId = await CreateArtefatoItemAsync(f.GmToken, "Luva da Força", "Atributo", "Forca", 3);
        var otherArtifactId = await CreateArtefatoItemAsync(f.GmToken, "Anel do Vigor", "Atributo", "Vigor", 7); // different Alvo — must not leak into Forca

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(forcaArtifactId)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(otherArtifactId)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(5, 0, false)));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<AtributoLido>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(8); // 5 + floor(0/2) + 3
        body!.Single(a => a.Atributo == "Vigor").Total.Should().Be(7); // 0 + 0 + 7
    }

    [Fact]
    public async Task An_equipped_Atributo_Artefato_is_summed_into_that_Atributos_Total()
    {
        // Requisitos - Ficha de NPCs/Criaturas R0005: equipping an Artefato targeting an Atributo feeds
        // AttributeTotalCalculator.Total's Artefatos term (previously hardcoded 0 for NPCs and Criaturas).
        var f = await NovaFichaAsync();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(5, 3, false)));

        var artifactItemId = await CreateArtefatoItemAsync(f.GmToken, "Anel de Teste", "Atributo", "Forca", 2);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifactItemId)));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<AtributoLido>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(8); // 5 + floor(3/2) + 2
    }

    [Fact]
    public async Task List_returns_attributes_in_canonical_order_and_stays_stable_after_an_update()
    {
        // Personagem/NPC: AttributeDisplayOrder (Domain) — Força, Vigor, Agilidade, Destreza, Astúcia,
        // Instinto, Influência, Vontade —, independente do valor inteiro persistido do enum Atributo.
        // Criatura (Ficha de Criaturas R0005 §2.a): Força, Vigor, Agilidade, Destreza, Astúcia e Ego —
        // a ordem de declaração do enum AtributoCriatura.
        var f = await NovaFichaAsync();

        var beforeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes"), f.Token));
        var before = (await beforeResponse.Content.ReadFromJsonAsync<List<AtributoLido>>())!;
        before.Select(a => a.Atributo).Should().Equal(Tipo.AtributosEmOrdem);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Agilidade"), f.Token, new AtualizarAtributo(3, 0, false)));

        var afterResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes"), f.Token));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<AtributoLido>>())!;
        after.Select(a => a.Atributo).Should().Equal(before.Select(a => a.Atributo));
    }

    [Fact]
    public async Task Update_an_attribute_by_someone_without_access_returns_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var intruso = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), intruso, new AtualizarAtributo(5, 3, false)));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task List_by_someone_without_access_returns_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var intruso = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes"), intruso));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task Budget_reports_GastoTotal_and_the_points_received_from_creation_and_levels()
    {
        var f = await NovaFichaAsync();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(5, 0, false)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes/budget"), f.Token));
        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.GastoTotal.Should().Be(5);
        body.PontosDisponiveis.Should().Be(9); // Nível 1 default → só o grant de criação (real Tabela de Níveis)
    }

    [Fact]
    public async Task Budget_at_level_1_offers_9_points_and_reports_zero_spent()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes/budget"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.PontosDisponiveis.Should().Be(9);
        body.GastoTotal.Should().Be(0);
    }

    [Fact]
    public async Task Budget_GastoTotal_is_the_sum_of_Gasto_across_attributes()
    {
        var f = await NovaFichaAsync();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(3, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Vigor"), f.Token, new AtualizarAtributo(4, 5, true)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes/budget"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.GastoTotal.Should().Be(7); // Bônus não entra no orçamento
    }

    [Fact]
    public async Task Budget_by_someone_without_access_returns_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var intruso = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes/budget"), intruso));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }
}

/// <summary>NPC e Criatura: Nível editável direto e orçamento que só informa (não bloqueia o gasto).</summary>
public abstract class AtributosDeNpcOuCriaturaTestes<TTipo>(ApiFixture fixture) : AtributosDaFichaTestes<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    [Fact]
    public async Task Budget_adds_the_Pontos_de_Atributo_of_every_level_up_to_the_sheets_Nivel()
    {
        // Tabela de Níveis: 9 (nível 1) + 1 (nível 2) + 1 (nível 6) + 2 (nível 10) = 13.
        var f = await NovaFichaAsync();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "nivel"), f.Token, 10));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes/budget"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.PontosDisponiveis.Should().Be(13);
    }

    [Fact]
    public async Task Update_above_the_budget_is_still_accepted_and_the_budget_reports_the_overspend()
    {
        var f = await NovaFichaAsync(); // nível 1 = 9 pontos

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(50, 0, false)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes/budget"), f.Token));
        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.GastoTotal.Should().Be(50);
        body.PontosDisponiveis.Should().Be(9);
    }
}

public sealed class AtributosDaFichaTestesPersonagem(ApiFixture fixture) : AtributosDaFichaTestes<TipoDeTestePersonagem>(fixture)
{
    [Fact]
    public async Task Update_rejects_a_Gasto_that_would_make_the_8_attribute_sum_exceed_the_point_budget()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token,
            new UpdateCharacterAttributeRequest(10, 0, false))); // Nível 1 budget is 9

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_rejects_when_this_attribute_plus_the_others_already_spent_exceeds_the_budget()
    {
        var f = await NovaFichaAsync();

        var first = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new UpdateCharacterAttributeRequest(5, 0, false)));
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5 (Forca já gasto) + 5 (Vigor) = 10 > 9.
        var second = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Vigor"), f.Token, new UpdateCharacterAttributeRequest(5, 0, false)));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Exactly at the budget (5 + 4 = 9) is allowed.
        var third = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Vigor"), f.Token, new UpdateCharacterAttributeRequest(4, 0, false)));
        third.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

public sealed class AtributosDaFichaTestesNpc(ApiFixture fixture) : AtributosDeNpcOuCriaturaTestes<TipoDeTesteNpc>(fixture);

public sealed class AtributosDaFichaTestesCriatura(ApiFixture fixture) : AtributosDeNpcOuCriaturaTestes<TipoDeTesteCriatura>(fixture)
{
    [Fact]
    public async Task Budget_replaces_the_level_1_grant_with_the_Ranks_starting_points()
    {
        // Ficha de Criaturas R0005 2.a: Rank S começa com 14 no lugar dos +9 do Nível 1.
        var f = await NovaFichaAsync();
        var update = new UpdateCreatureSheetRequest(null, "Dragão", null, null, null, null,
            "S", 1, 0, 0, 0, 0, 0, "Nenhuma");
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, update)))
            .EnsureSuccessStatusCode();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "attributes/budget"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        body!.PontosDisponiveis.Should().Be(14);
    }
}
