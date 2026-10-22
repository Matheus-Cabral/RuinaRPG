using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
// Pedidos e respostas de Afeição e Característica têm o mesmo JSON nos três tipos de ficha (a Criatura
// só não tem IsRacial, que nenhum teste daqui lê).
using AdicionarAfeicao = RuinaRPG.Contracts.Fichas.AddAffectionRequest;
using AdicionarCaracteristica = RuinaRPG.Contracts.Fichas.AddTraitRequest;
using AfeicaoLida = RuinaRPG.Contracts.Fichas.AffectionResponse;
using CaracteristicaLida = RuinaRPG.Contracts.Fichas.TraitDaFichaResponse;
using CaracteristicasLidas = RuinaRPG.Contracts.Fichas.TraitsListResponse;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// Afeições e Características da ficha (<c>/affections</c>, <c>/traits</c>), nos três tipos. Personagem e
/// NPC escolhem do catálogo normal (semeado de Características.md); a Criatura, só do catálogo exclusivo de
/// criatura — por isso as Características usadas vêm dos ganchos abaixo.
/// </summary>
public abstract class AfeicoesECaracteristicasDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    /// <summary>Uma Positiva e uma Negativa sem Especificação, cada uma de custo até 3.</summary>
    protected abstract Task<(string PositiveTraitId, string NegativeTraitId)> CaracteristicasSimplesAsync();

    /// <summary>Duas Positivas que cabem no orçamento do nível 1 (5) sozinhas, mas não juntas.</summary>
    protected abstract Task<List<(string Id, int Custo)>> PositivasQuePassamDoOrcamentoAsync();

    /// <summary>Oito Negativas sem Especificação, de custo até 5 cada, das maiores para as menores.</summary>
    protected abstract Task<List<(string Id, int Custo)>> NegativasCandidatasAsync();

    /// <summary>Uma Negativa que exige Especificação.</summary>
    protected abstract Task<string> CaracteristicaComEspecificacaoAsync();

    [Fact]
    public async Task AddAffection_then_ListAffections_returns_it_then_DeleteAffection_removes_it()
    {
        var f = await NovaFichaAsync();

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "affections"), f.Token,
            new AdicionarAfeicao("Amigo de infância", 5)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<AfeicaoLida>();
        added!.Nome.Should().Be("Amigo de infância");
        added.Favorabilidade.Should().Be(5);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "affections"), f.Token));
        var list = await listResponse.Content.ReadFromJsonAsync<List<AfeicaoLida>>();
        list!.Should().ContainSingle(a => a.Id == added.Id);

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"affections/{added.Id}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterDelete = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "affections"), f.Token));
        var listAfter = await listAfterDelete.Content.ReadFromJsonAsync<List<AfeicaoLida>>();
        listAfter!.Should().NotContain(a => a.Id == added.Id);
    }

    [Fact]
    public async Task AddTrait_lands_in_the_correct_bucket_with_correct_totals_and_DeleteTrait_removes_it()
    {
        var f = await NovaFichaAsync();
        var (positiveTraitId, negativeTraitId) = await CaracteristicasSimplesAsync();

        var addPositiveResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AdicionarCaracteristica(positiveTraitId, null)));
        addPositiveResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var addNegativeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AdicionarCaracteristica(negativeTraitId, null)));
        addNegativeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var addedNegative = await addNegativeResponse.Content.ReadFromJsonAsync<CaracteristicaLida>();

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "traits"), f.Token));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<CaracteristicasLidas>();
        list!.Positivas.Should().ContainSingle(t => t.TraitId == positiveTraitId);
        list.Negativas.Should().ContainSingle(t => t.TraitId == negativeTraitId);
        list.TotalPositivas.Should().Be(list.Positivas.Sum(t => t.Custo));
        list.TotalNegativas.Should().Be(list.Negativas.Sum(t => t.Custo));

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"traits/{addedNegative!.Id}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterDelete = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "traits"), f.Token));
        var listAfter = await listAfterDelete.Content.ReadFromJsonAsync<CaracteristicasLidas>();
        listAfter!.Negativas.Should().NotContain(t => t.Id == addedNegative.Id);
    }

    [Fact]
    public async Task AddTrait_with_a_nonexistent_TraitId_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AdicionarCaracteristica(Guid.NewGuid().ToString(), null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTrait_rejects_a_Positiva_that_would_push_the_side_total_past_the_budget()
    {
        var f = await NovaFichaAsync();
        var top = await PositivasQuePassamDoOrcamentoAsync();
        top.Sum(t => t.Custo).Should().BeGreaterThan(5); // level-1 creation budget

        var first = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AdicionarCaracteristica(top[0].Id, null)));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AdicionarCaracteristica(top[1].Id, null)));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Ficha de Personagem 5.d: o limite de Negativas é o dobro do de Positivas — no nível 1, 10
    /// contra 5. As Negativas são aceitas enquanto o total cabe em 10 e rejeitadas quando passaria.
    /// </summary>
    [Fact]
    public async Task AddTrait_accepts_Negativas_up_to_twice_the_positive_budget_and_rejects_past_it()
    {
        var f = await NovaFichaAsync();
        var candidatas = await NegativasCandidatasAsync();

        var total = 0;
        var rejeitadas = 0;
        foreach (var (id, custo) in candidatas)
        {
            var cabe = total + Math.Abs(custo) <= 10;
            var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
                new AdicionarCaracteristica(id, null)));
            response.StatusCode.Should().Be(cabe ? HttpStatusCode.Created : HttpStatusCode.BadRequest);
            if (cabe) total += Math.Abs(custo); else rejeitadas++;
        }

        total.Should().BeGreaterThan(5, "o limite de Negativas passa do de Positivas (5)").And.BeLessThanOrEqualTo(10);
        rejeitadas.Should().BeGreaterThan(0, "alguma Negativa tem de ter estourado o limite de 10");
        var lista = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "traits"), f.Token)))
            .Content.ReadFromJsonAsync<CaracteristicasLidas>();
        lista!.PontosDisponiveis.Should().Be(5);
        lista.PontosDisponiveisNegativas.Should().Be(10);
    }

    // Este e o seguinte cobrem, juntos, o antigo NPC/Criatura
    // AddTrait_rejects_a_RequerEspecificacao_trait_added_without_an_Especificacao_and_persists_it_when_provided.
    [Fact]
    public async Task AddTrait_rejects_a_RequerEspecificacao_trait_added_without_an_Especificacao()
    {
        var f = await NovaFichaAsync();
        var alergiaId = await CaracteristicaComEspecificacaoAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AdicionarCaracteristica(alergiaId, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTrait_persists_and_returns_the_Especificacao_for_a_RequerEspecificacao_trait()
    {
        var f = await NovaFichaAsync();
        var alergiaId = await CaracteristicaComEspecificacaoAsync();

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AdicionarCaracteristica(alergiaId, "Poeira")));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<CaracteristicaLida>();
        added!.Especificacao.Should().Be("Poeira");
        added.RequerEspecificacao.Should().BeTrue();

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "traits"), f.Token));
        var list = await listResponse.Content.ReadFromJsonAsync<CaracteristicasLidas>();
        list!.Negativas.Should().ContainSingle(t => t.Id == added.Id && t.Especificacao == "Poeira");
    }

    [Fact]
    public async Task Affection_and_trait_actions_by_someone_without_access_return_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);
        var (positiveTraitId, _) = await CaracteristicasSimplesAsync();

        var addAffection = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "affections"), f.Token, new AdicionarAfeicao("Rival", -2)));
        var affectionId = (await addAffection.Content.ReadFromJsonAsync<AfeicaoLida>())!.Id;
        var addTrait = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token, new AdicionarCaracteristica(positiveTraitId, null)));
        var traitId = (await addTrait.Content.ReadFromJsonAsync<CaracteristicaLida>())!.Id;

        var addAffectionResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "affections"), otherToken, new AdicionarAfeicao("Rival", -2)));
        addAffectionResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var listAffectionsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "affections"), otherToken));
        listAffectionsResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var deleteAffectionResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"affections/{affectionId}"), otherToken));
        deleteAffectionResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var addTraitResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), otherToken, new AdicionarCaracteristica(positiveTraitId, null)));
        addTraitResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var listTraitsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "traits"), otherToken));
        listTraitsResponse.StatusCode.Should().Be(Tipo.StatusNegado);

        var deleteTraitResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"traits/{traitId}"), otherToken));
        deleteTraitResponse.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    protected async Task<Trait> GetTraitAsync(string nome)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return await db.Traits.SingleAsync(t => t.Nome == nome);
    }

    protected async Task<string> CreateExclusiveTraitDirectlyAsync(string nome, string descricao, int custo, Polaridade polaridade, bool requerEspecificacao)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var trait = new CreatureExclusiveTrait
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Descricao = descricao,
            Custo = custo,
            Polaridade = polaridade,
            RequerEspecificacao = requerEspecificacao,
        };
        db.Set<CreatureExclusiveTrait>().Add(trait);
        await db.SaveChangesAsync();
        return trait.Id.ToString();
    }
}

/// <summary>Personagem e NPC: Características do catálogo normal, semeado de Características.md.</summary>
public abstract class AfeicoesECaracteristicasDePersonagemOuNpcTestes<TTipo>(ApiFixture fixture)
    : AfeicoesECaracteristicasDaFichaTestes<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    // Restricted to !RequerEspecificacao so callers that don't care about that field can add the trait
    // with a bare TraitId, no Especificacao required.
    protected override async Task<(string PositiveTraitId, string NegativeTraitId)> CaracteristicasSimplesAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var positive = await db.Traits.FirstAsync(t => t.Polaridade == Polaridade.Positiva && !t.RequerEspecificacao && t.Custo <= 3);
        var negative = await db.Traits.FirstAsync(t => t.Polaridade == Polaridade.Negativa && !t.RequerEspecificacao && t.Custo >= -3);
        return (positive.Id.ToString(), negative.Id.ToString());
    }

    protected override Task<List<(string Id, int Custo)>> PositivasQuePassamDoOrcamentoAsync() => GetTopCostTraitsAsync(Polaridade.Positiva, 2);

    protected override Task<List<(string Id, int Custo)>> NegativasCandidatasAsync() => GetTopCostTraitsAsync(Polaridade.Negativa, 8);

    // "Alergia" is a real, stable single-tier Negativa (-1 ponto) that RequerEspecificacao — used
    // directly by name, mirroring TraitSeedParserTests' own convention of referencing it verbatim.
    protected override async Task<string> CaracteristicaComEspecificacaoAsync() => (await GetTraitAsync("Alergia")).Id.ToString();

    // The highest-cost traits on a side (excluding ones needing a specification, to isolate the budget
    // check from the specification check). Each candidate's own |Custo| must stay within the level-1
    // budget (5) on its own — the tests add them one at a time and expect the FIRST to succeed, so a
    // trait that alone already exceeds the budget (e.g. a -10 magnitude Negativa) would be a false failure.
    private async Task<List<(string Id, int Custo)>> GetTopCostTraitsAsync(Polaridade polaridade, int take)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var ordered = polaridade == Polaridade.Positiva
            ? await db.Traits.Where(t => t.Polaridade == polaridade && !t.RequerEspecificacao && t.Custo <= 5).OrderByDescending(t => t.Custo).Take(take).ToListAsync()
            : await db.Traits.Where(t => t.Polaridade == polaridade && !t.RequerEspecificacao && t.Custo >= -5).OrderBy(t => t.Custo).Take(take).ToListAsync();
        return ordered.Select(t => (t.Id.ToString(), t.Custo)).ToList();
    }

    [Fact]
    public async Task AddTrait_with_a_creature_exclusive_characteristic_Id_returns_400()
    {
        var f = await NovaFichaAsync();
        var exclusiveId = await CreateExclusiveTraitDirectlyAsync("Só de Criatura Teste", "Descrição.", 1, Polaridade.Positiva, false);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AdicionarCaracteristica(exclusiveId, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

/// <summary>Só o Personagem edita uma Afeição (PUT); NPC e Criatura apagam e adicionam de novo.</summary>
public sealed class AfeicoesECaracteristicasDaFichaTestesPersonagem(ApiFixture fixture) : AfeicoesECaracteristicasDePersonagemOuNpcTestes<TipoDeTestePersonagem>(fixture)
{
    [Fact]
    public async Task UpdateAffection_returns_200_and_the_list_reflects_the_change()
    {
        var f = await NovaFichaAsync();

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "affections"), f.Token,
            new AddAffectionRequest("Amigo de infância", 5)));
        var added = await addResponse.Content.ReadFromJsonAsync<AffectionResponse>();

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"affections/{added!.Id}"), f.Token,
            new UpdateCharacterAffectionRequest("Rival de infância", -3)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<AffectionResponse>();
        updated!.Nome.Should().Be("Rival de infância");
        updated.Favorabilidade.Should().Be(-3);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "affections"), f.Token));
        var body = await listResponse.Content.ReadFromJsonAsync<List<AffectionResponse>>();
        body!.Should().ContainSingle(a => a.Id == added.Id && a.Nome == "Rival de infância" && a.Favorabilidade == -3);
    }

    [Fact]
    public async Task UpdateAffection_by_an_unrelated_jogador_returns_403()
    {
        var f = await NovaFichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);

        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "affections"), f.Token,
            new AddAffectionRequest("Mentor", 8)));
        var added = await addResponse.Content.ReadFromJsonAsync<AffectionResponse>();

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"affections/{added!.Id}"), otherToken,
            new UpdateCharacterAffectionRequest("Impostor", 0)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAffection_for_a_nonexistent_id_returns_404()
    {
        var f = await NovaFichaAsync();

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"affections/{Guid.NewGuid()}"), f.Token,
            new UpdateCharacterAffectionRequest("Ninguém", 0)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

public sealed class AfeicoesECaracteristicasDaFichaTestesNpc(ApiFixture fixture) : AfeicoesECaracteristicasDePersonagemOuNpcTestes<TipoDeTesteNpc>(fixture);

/// <summary>Criatura: só o catálogo exclusivo de criatura; as Características são criadas direto no banco.</summary>
public sealed class AfeicoesECaracteristicasDaFichaTestesCriatura(ApiFixture fixture) : AfeicoesECaracteristicasDaFichaTestes<TipoDeTesteCriatura>(fixture)
{
    protected override async Task<(string PositiveTraitId, string NegativeTraitId)> CaracteristicasSimplesAsync() => (
        await CreateExclusiveTraitDirectlyAsync("Couraça Viva", "Pele endurecida.", 2, Polaridade.Positiva, false),
        await CreateExclusiveTraitDirectlyAsync("Lentidão Crônica", "Move-se devagar.", -2, Polaridade.Negativa, false));

    protected override async Task<List<(string Id, int Custo)>> PositivasQuePassamDoOrcamentoAsync() =>
    [
        (await CreateExclusiveTraitDirectlyAsync("Presas Colossais", "Mordida devastadora.", 4, Polaridade.Positiva, false), 4),
        (await CreateExclusiveTraitDirectlyAsync("Garras Afiadas", "Rasga a carne.", 3, Polaridade.Positiva, false), 3),
    ];

    protected override async Task<List<(string Id, int Custo)>> NegativasCandidatasAsync()
    {
        var candidatas = new List<(string Id, int Custo)>();
        var custos = new[] { -5, -4, -3, -3, -2, -2, -1, -1 };
        for (var i = 0; i < custos.Length; i++)
            candidatas.Add((await CreateExclusiveTraitDirectlyAsync($"Defeito Bestial {i}", "Fraqueza da criatura.", custos[i], Polaridade.Negativa, false), custos[i]));
        return candidatas;
    }

    protected override Task<string> CaracteristicaComEspecificacaoAsync() =>
        CreateExclusiveTraitDirectlyAsync("Alergia Bestial", "Reage a um elemento.", -1, Polaridade.Negativa, true);

    [Fact]
    public async Task AddTrait_rejects_a_characteristic_of_the_normal_catalog()
    {
        var f = await NovaFichaAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var positiveTraitId = (await db.Traits.FirstAsync(t => t.Polaridade == Polaridade.Positiva && !t.RequerEspecificacao && t.Custo <= 3)).Id.ToString();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AddTraitRequest(positiveTraitId, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Esta característica não é do catálogo de criatura.");
    }

    [Fact]
    public async Task AddTrait_resolves_a_creature_exclusive_characteristic_and_it_lands_in_ListTraits()
    {
        var f = await NovaFichaAsync();
        var exclusiveId = await CreateExclusiveTraitDirectlyAsync("Regeneração Bestial", "Recupera Vitalidade.", 3, Polaridade.Positiva, false);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AddTraitRequest(exclusiveId, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreatureTraitResponse>();
        created!.Nome.Should().Be("Regeneração Bestial");
        created.Custo.Should().Be(3);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "traits"), f.Token));
        var list = await listResponse.Content.ReadFromJsonAsync<CreatureTraitsListResponse>();
        list!.Positivas.Should().ContainSingle(t => t.Nome == "Regeneração Bestial");
        list.TotalPositivas.Should().Be(3);
    }

    [Fact]
    public async Task AddTrait_sums_a_normal_and_a_creature_exclusive_characteristic_into_the_same_Positivas_budget()
    {
        var f = await NovaFichaAsync();

        // "Alfabetizado" (Características.md: "1 ponto") — same by-name reference style this file
        // already uses for "Alergia" in GetTraitAsync. Asserted below rather than trusted blindly, so
        // this test fails loudly (not silently under-tests) if that game-data value ever changes.
        var alfabetizado = await GetTraitAsync("Alfabetizado");
        alfabetizado.Custo.Should().Be(1);
        var exclusiveId = await CreateExclusiveTraitDirectlyAsync("Regeneração Bestial", "Recupera Vitalidade.", 3, Polaridade.Positiva, false);

        Guid legacyRowId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            legacyRowId = Guid.NewGuid();
            db.CreatureTraits.Add(new RuinaRPG.Infrastructure.CreatureSheets.CreatureTrait
            {
                Id = legacyRowId, SheetId = Guid.Parse(f.SheetId), TraitId = alfabetizado.Id,
                Polaridade = Polaridade.Positiva,
            });
            await db.SaveChangesAsync();
        }
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token, new AddTraitRequest(exclusiveId, null)));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "traits"), f.Token));
        var list = await listResponse.Content.ReadFromJsonAsync<CreatureTraitsListResponse>();
        list!.Positivas.Should().HaveCount(2);
        list.TotalPositivas.Should().Be(4); // 1 (Alfabetizado) + 3 (Regeneração Bestial)

        var deleteLegacy = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"traits/{legacyRowId}"), f.Token));
        deleteLegacy.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddTrait_rejects_a_creature_exclusive_pick_that_requires_Especificacao_without_one()
    {
        var f = await NovaFichaAsync();
        var exclusiveId = await CreateExclusiveTraitDirectlyAsync("Fúria Sazonal", "Muda de comportamento numa estação.", 1, Polaridade.Positiva, true);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "traits"), f.Token,
            new AddTraitRequest(exclusiveId, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
