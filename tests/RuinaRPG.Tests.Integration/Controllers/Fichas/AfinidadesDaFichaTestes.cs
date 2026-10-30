using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
// Pedidos e resposta de Afinidade têm o mesmo JSON no Personagem e no NPC.
using AdicionarAfinidade = RuinaRPG.Contracts.Fichas.AddAffinityRequest;
using AfinidadeLida = RuinaRPG.Contracts.Fichas.AffinityResponse;
using AtualizarAfinidade = RuinaRPG.Contracts.Fichas.UpdateAffinityRequest;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// Afinidades da ficha (<c>/affinities</c>), que só Personagem e NPC têm. A ficha começa como Adepto com
/// Afinidade Adicional 100, para o limite da Vocação Arcana (só do Personagem) não interferir.
/// </summary>
public abstract class AfinidadesDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    /// <summary>Corpo do PUT da ficha com a Vocação e a Afinidade Adicional dadas.</summary>
    protected abstract object AtualizacaoComVocacao(string? vocacao, int afinidadeAdicional);

    /// <summary>Linha de Afinidade nova da tabela do tipo, para inserir direto no banco.</summary>
    protected abstract IAfinidadeDaFicha NovaLinha();

    protected async Task<FichaDeTeste> FichaAsync()
    {
        var f = await NovaFichaAsync();
        // Adepto libera Dobra+Consagração — cobre todas as combinações que os testes usam: Fogo/Terra são
        // Dobra, Curar/Aprimorar são Consagração.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.GmToken, AtualizacaoComVocacao("Adepto", 100)));
        return f;
    }

    protected Task<HttpResponseMessage> AddAsync(FichaDeTeste f, string? elemento, string? segunda, int? subValor = null) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "affinities"), f.Token, new AdicionarAfinidade(elemento, null, segunda, null, subValor, null)));

    protected Task<HttpResponseMessage> AddAsync(FichaDeTeste f, AdicionarAfinidade request) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "affinities"), f.Token, request));

    protected Task<HttpResponseMessage> UpdateAsync(FichaDeTeste f, string token, string id, AtualizarAfinidade request) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"affinities/{id}"), token, request));

    protected async Task<List<AfinidadeLida>> ListAsync(FichaDeTeste f) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "affinities"), f.Token)))
            .Content.ReadFromJsonAsync<List<AfinidadeLida>>())!;

    private async Task InserirDiretoAsync(Guid id, FichaDeTeste f, Elemento elemento, EssenciaBasica? segunda, SubElemento sub)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var linha = NovaLinha();
        linha.Id = id;
        linha.SheetId = Guid.Parse(f.SheetId);
        linha.Elemento = elemento;
        linha.SegundaEssencia = segunda;
        linha.SubElemento = sub;
        db.Add((object)linha);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Add_a_valid_combination_returns_201()
    {
        var f = await FichaAsync();

        var response = await AddAsync(f, new AdicionarAfinidade("Fogo", 3, "Vida", 4, 2, 10));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ListAsync(f);
        body.Should().ContainSingle(a => a.Elemento == "Fogo" && a.ElementoValor == 3 && a.SegundaEssencia == "Vida" && a.SegundaEssenciaValor == 4
            && a.SubElemento == "Curar" && a.SubElementoValor == 2 && a.Experiencia == 10);
    }

    [Fact]
    public async Task Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list()
    {
        var f = await FichaAsync();

        var addResponse = await AddAsync(f, new AdicionarAfinidade("Terra", 1, null, null, 1, 5));
        var added = await addResponse.Content.ReadFromJsonAsync<AfinidadeLida>();

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"affinities/{added!.Id}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListAsync(f)).Should().NotContain(a => a.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_someone_without_access_returns_the_denied_status()
    {
        var f = await FichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "affinities"), otherToken,
            new AdicionarAfinidade("Terra", 1, null, null, 1, 5)));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task List_by_someone_without_access_returns_the_denied_status()
    {
        var f = await FichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "affinities"), otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_with_every_field_null_returns_201_and_a_blank_row()
    {
        var f = await FichaAsync();

        var response = await AddAsync(f, new AdicionarAfinidade(null, null, null, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<AfinidadeLida>();
        added!.Elemento.Should().BeNull();
        added.SubElemento.Should().BeNull();
        added.Experiencia.Should().BeNull();
    }

    [Fact]
    public async Task Add_with_only_Elemento_given_returns_201_without_checking_a_combination()
    {
        var f = await FichaAsync();

        var response = await AddAsync(f, new AdicionarAfinidade("Fogo", 3, null, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<AfinidadeLida>();
        added!.Elemento.Should().Be("Fogo");
        added.SubElemento.Should().BeNull();
    }

    [Fact]
    public async Task Update_an_existing_affinity_returns_200_and_the_list_reflects_the_change()
    {
        var f = await FichaAsync();

        var addResponse = await AddAsync(f, new AdicionarAfinidade("Fogo", 3, "Vida", 4, 2, 10));
        var added = await addResponse.Content.ReadFromJsonAsync<AfinidadeLida>();

        var updateResponse = await UpdateAsync(f, f.Token, added!.Id, new AtualizarAfinidade("Terra", 5, "Vida", 4, 1, 8));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<AfinidadeLida>();
        updated!.Elemento.Should().Be("Terra");
        updated.ElementoValor.Should().Be(5);
        updated.SegundaEssencia.Should().Be("Vida");
        updated.SubElemento.Should().Be("Aprimorar");
        updated.SubElementoValor.Should().Be(1);
        updated.Experiencia.Should().Be(8);

        (await ListAsync(f)).Should().ContainSingle(a => a.Id == added.Id && a.Elemento == "Terra" && a.SubElemento == "Aprimorar");
    }

    [Fact]
    public async Task Update_can_clear_fields_back_to_null()
    {
        var f = await FichaAsync();

        var addResponse = await AddAsync(f, new AdicionarAfinidade("Fogo", 3, "Vida", 4, 2, 10));
        var added = await addResponse.Content.ReadFromJsonAsync<AfinidadeLida>();

        var updateResponse = await UpdateAsync(f, f.Token, added!.Id, new AtualizarAfinidade(null, null, null, null, null, null));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<AfinidadeLida>();
        updated!.Elemento.Should().BeNull();
        updated.SubElemento.Should().BeNull();
        updated.SegundaEssencia.Should().BeNull();
    }

    [Fact]
    public async Task Update_by_someone_without_access_returns_the_denied_status()
    {
        var f = await FichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);

        var addResponse = await AddAsync(f, new AdicionarAfinidade("Fogo", 3, null, null, 2, 10));
        var added = await addResponse.Content.ReadFromJsonAsync<AfinidadeLida>();

        var updateResponse = await UpdateAsync(f, otherToken, added!.Id, new AtualizarAfinidade("Terra", 1, null, null, 1, 1));

        updateResponse.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task Update_a_nonexistent_affinity_returns_404()
    {
        var f = await FichaAsync();

        var updateResponse = await UpdateAsync(f, f.Token, Guid.NewGuid().ToString(), new AtualizarAfinidade("Terra", 1, null, null, 1, 1));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_does_not_flag_a_duplicate_against_its_own_row()
    {
        var f = await FichaAsync(); // Vocacao=Adepto

        var addResponse = await AddAsync(f, new AdicionarAfinidade("Fogo", 3, "Vida", null, null, null));
        var added = await addResponse.Content.ReadFromJsonAsync<AfinidadeLida>();

        // Reenviar a mesma linha com o mesmo Sub-Elemento derivado não deve se auto-rejeitar como duplicata.
        var updateResponse = await UpdateAsync(f, f.Token, added!.Id, new AtualizarAfinidade("Fogo", 5, "Vida", null, null, null));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_does_not_reject_a_pre_existing_duplicate_left_unchanged()
    {
        var f = await FichaAsync(); // Vocacao=Adepto

        await AddAsync(f, new AdicionarAfinidade("Fogo", 3, "Vida", null, null, null));

        // Simulate a pre-existing duplicate SubElemento inserted directly in the DB, bypassing the
        // controller's own duplicate check entirely — the API now refuses to create this via HTTP.
        var row2Id = Guid.NewGuid();
        await InserirDiretoAsync(row2Id, f, Elemento.Fogo, EssenciaBasica.Vida, SubElemento.Curar);

        // Re-send row2 with the same (now-colliding) SubElemento, changing only Experiencia — the
        // pre-existing collision is not newly introduced, so it must be grandfathered through.
        var updateResponse = await UpdateAsync(f, f.Token, row2Id.ToString(), new AtualizarAfinidade("Fogo", 1, "Vida", null, null, 7));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Ar", "Agua", "Gelo")]
    [InlineData("Agua", "Ar", "Gelo")]
    [InlineData("Terra", "Vida", "Aprimorar")]
    [InlineData("Agua", "Mundano", "Hemomancia")]
    public async Task Add_derives_the_SubElemento_from_the_two_essencias(string e1, string e2, string sub)
    {
        var f = await FichaAsync();

        (await AddAsync(f, e1, e2)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ListAsync(f)).Should().ContainSingle(a => a.Elemento == e1 && a.SegundaEssencia == e2 && a.SubElemento == sub);
    }

    /// <summary>Era o NPC <c>Add_derives_the_SubElemento_from_the_two_essencias</c> (um caso só, com valor).</summary>
    [Fact]
    public async Task Add_derives_the_SubElemento_and_keeps_the_SegundaEssenciaValor()
    {
        var f = await FichaAsync();

        var response = await AddAsync(f, new AdicionarAfinidade("Fogo", null, "Terra", 2, null, null));
        var list = await ListAsync(f);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        list.Should().ContainSingle(a => a.SubElemento == "Ferro" && a.SegundaEssencia == "Terra" && a.SegundaEssenciaValor == 2);
    }

    // ("Agua", "Vida") era o caso único do NPC.
    [Theory]
    [InlineData("Ar", "Terra")]
    [InlineData("Agua", "Fogo")]
    [InlineData("Fogo", "Fogo")]
    [InlineData("Fogo", "Alma")]
    [InlineData("Ar", "Vida")]
    [InlineData("Agua", "Vida")]
    public async Task Add_with_essencias_that_dont_cross_returns_400(string e1, string e2)
    {
        var f = await FichaAsync();

        var response = await AddAsync(f, e1, e2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Essas duas Essências não se cruzam na Matriz Elemental.");
    }

    [Fact]
    public async Task Add_with_SegundaEssencia_but_no_Elemento_returns_400()
    {
        var f = await FichaAsync();

        var response = await AddAsync(f, null, "Mundano");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Escolha a Essência Básica 1 antes da 2.");
    }

    [Fact]
    public async Task Add_rejects_a_second_row_with_the_same_SubElemento_but_allows_repeating_Essencia1()
    {
        var f = await FichaAsync();
        await AddAsync(f, "Fogo", "Vida");

        var sameSub = await AddAsync(f, "Fogo", "Vida");
        var sameE1 = await AddAsync(f, "Fogo", "Mundano");

        sameSub.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await sameSub.Content.ReadAsStringAsync()).Should().Contain("Já existe uma linha de Afinidade com esse Sub-Elemento.");
        sameE1.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>Era o caso do NPC: o mesmo Sub-Elemento com as Essências trocadas.</summary>
    [Fact]
    public async Task Add_rejects_a_duplicate_SubElemento()
    {
        var f = await FichaAsync();
        await AddAsync(f, "Ar", "Agua");

        var response = await AddAsync(f, "Agua", "Ar");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Campeao")]
    [InlineData("Feiticeiro")]
    public async Task Any_Vocacao_can_pick_any_essencias(string? vocacao)
    {
        var f = await FichaAsync();
        var setVocacao = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.GmToken, AtualizacaoComVocacao(vocacao, 100)));
        setVocacao.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AddAsync(f, "Terra", "Mundano")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AddAsync(f, "Agua", "Alma")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_changing_only_values_keeps_a_legacy_SubElemento()
    {
        var f = await FichaAsync();
        var legacyId = Guid.NewGuid();
        await InserirDiretoAsync(legacyId, f, Elemento.Ar, null, SubElemento.Curar);

        var response = await UpdateAsync(f, f.Token, legacyId.ToString(), new AtualizarAfinidade("Ar", 5, null, null, 3, 7));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListAsync(f)).Should().ContainSingle(a => a.SubElemento == "Curar" && a.ElementoValor == 5 && a.SubElementoValor == 3 && a.Experiencia == 7);
    }

    [Fact]
    public async Task Update_changing_an_essencia_recomputes_the_SubElemento()
    {
        var f = await FichaAsync();
        var added = await (await AddAsync(f, "Fogo", "Vida")).Content.ReadFromJsonAsync<AfinidadeLida>();

        await UpdateAsync(f, f.Token, added!.Id, new AtualizarAfinidade("Fogo", null, "Mundano", null, null, null));
        var afterChange = (await ListAsync(f)).Single();
        await UpdateAsync(f, f.Token, added.Id, new AtualizarAfinidade("Fogo", null, null, null, null, null));
        var afterClear = (await ListAsync(f)).Single();

        afterChange.SubElemento.Should().Be("Necromancia");
        afterClear.SubElemento.Should().BeNull();
        afterClear.SegundaEssencia.Should().BeNull();
    }

    [Fact]
    public async Task Negative_AfinidadeAdicional_is_rejected()
    {
        var f = await FichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.GmToken, AtualizacaoComVocacao("Feiticeiro", -1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

/// <summary>Só o Personagem tem o limite da Vocação Arcana (2.c); o NPC aceita além dele e só informa.</summary>
public sealed class AfinidadesDaFichaTestesPersonagem(ApiFixture fixture) : AfinidadesDaFichaTestes<TipoDeTestePersonagem>(fixture)
{
    protected override object AtualizacaoComVocacao(string? vocacao, int afinidadeAdicional) =>
        new UpdateCharacterSheetRequest(null, "Ficha de Teste", null, null, vocacao, null, null, null,
            false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, null, null, afinidadeAdicional);

    protected override IAfinidadeDaFicha NovaLinha() => new RuinaRPG.Infrastructure.CharacterSheets.CharacterAffinity();

    // Vocação Arcana (2.c): Feiticeiro sem coração de mana está no Círculo 0 → máximo 3 + adicional.
    private async Task SetVocacaoAsync(FichaDeTeste f, string? vocacao, int afinidadeAdicional)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{f.SheetId}", f.GmToken, AtualizacaoComVocacao(vocacao, afinidadeAdicional)));
        response.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> AddValoresAsync(FichaDeTeste f, string elemento, int elementoValor, string? segunda, int? segundaValor, int? subValor) =>
        AddAsync(f, new AddAffinityRequest(elemento, elementoValor, segunda, segundaValor, subValor, null));

    private async Task<CharacterSheetResponse> GetSheetAsync(FichaDeTeste f) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{f.SheetId}", f.Token)))
            .Content.ReadFromJsonAsync<CharacterSheetResponse>())!;

    [Fact]
    public async Task Add_up_to_the_vocacao_arcana_maximum_is_accepted_and_beyond_it_is_rejected()
    {
        var f = await FichaAsync();
        await SetVocacaoAsync(f, "Feiticeiro", 0);

        var within = await AddValoresAsync(f, "Fogo", 2, "Terra", 1, 0);
        var beyond = await AddValoresAsync(f, "Ar", 1, null, null, null);

        within.StatusCode.Should().Be(HttpStatusCode.Created);
        beyond.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await beyond.Content.ReadAsStringAsync()).Should().Contain("Vocação Arcana");
        (await ListAsync(f)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_repeated_essencia_only_costs_its_highest_value()
    {
        var f = await FichaAsync();
        await SetVocacaoAsync(f, "Feiticeiro", 0);

        (await AddValoresAsync(f, "Fogo", 2, null, null, null)).StatusCode.Should().Be(HttpStatusCode.Created);
        // Fogo 1 na Essência 2 não custa nada além do Fogo 2 já pago; Mundano 1 leva o gasto a 3.
        (await AddValoresAsync(f, "Terra", 0, "Fogo", 1, null)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AddValoresAsync(f, "Agua", 0, "Mundano", 1, null)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await GetSheetAsync(f)).VocacaoArcanaGasta.Should().Be(3);
    }

    [Fact]
    public async Task AfinidadeAdicional_raises_the_maximum()
    {
        var f = await FichaAsync();
        await SetVocacaoAsync(f, "Feiticeiro", 2);

        (await AddValoresAsync(f, "Fogo", 5, null, null, null)).StatusCode.Should().Be(HttpStatusCode.Created);

        var sheet = await GetSheetAsync(f);
        sheet.AfinidadeAdicional.Should().Be(2);
        sheet.VocacaoArcanaMaxima.Should().Be(5);
        sheet.VocacaoArcanaGasta.Should().Be(5);
    }

    [Fact]
    public async Task A_martial_vocacao_only_has_the_afinidade_adicional()
    {
        var f = await FichaAsync();
        await SetVocacaoAsync(f, "Campeao", 0);

        var costly = await AddValoresAsync(f, "Fogo", 1, null, null, null);
        var free = await AddAsync(f, "Fogo", null);

        costly.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        free.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetSheetAsync(f)).VocacaoArcanaMaxima.Should().Be(0);
    }

    [Fact]
    public async Task Lowering_the_maximum_is_allowed_and_then_only_updates_that_lower_the_gasto_go_through()
    {
        var f = await FichaAsync();
        await SetVocacaoAsync(f, "Feiticeiro", 3);
        var added = await (await AddValoresAsync(f, "Fogo", 6, null, null, null)).Content.ReadFromJsonAsync<AffinityResponse>();

        await SetVocacaoAsync(f, "Feiticeiro", 0); // gasto 6 > máximo 3, mas baixar o máximo não é bloqueado

        var lower = await UpdateAsync(f, f.Token, added!.Id, new UpdateAffinityRequest("Fogo", 5, null, null, null, null));
        var higher = await UpdateAsync(f, f.Token, added.Id, new UpdateAffinityRequest("Fogo", 6, null, null, null, null));

        lower.StatusCode.Should().Be(HttpStatusCode.OK);
        higher.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetSheetAsync(f)).VocacaoArcanaGasta.Should().Be(5);
    }
}

public sealed class AfinidadesDaFichaTestesNpc(ApiFixture fixture) : AfinidadesDaFichaTestes<TipoDeTesteNpc>(fixture)
{
    private static UpdateNpcSheetRequest ValidUpdate() => new(null, "Ficha de Teste", null, null, "Adepto", null, null, null,
        1, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, null, null, 0);

    protected override object AtualizacaoComVocacao(string? vocacao, int afinidadeAdicional) =>
        ValidUpdate() with { Vocacao = vocacao, AfinidadeAdicional = afinidadeAdicional };

    protected override IAfinidadeDaFicha NovaLinha() => new RuinaRPG.Infrastructure.NpcSheets.NpcAffinity();

    [Fact]
    public async Task Npc_affinities_beyond_the_vocacao_arcana_maximum_are_accepted_and_reported()
    {
        var f = await FichaAsync();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{f.SheetId}", f.GmToken, ValidUpdate() with { AfinidadeAdicional = 1 }));

        var response = await AddAsync(f, new AddAffinityRequest("Fogo", 3, "Terra", 2, 1, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var sheet = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{f.SheetId}", f.GmToken)))
            .Content.ReadFromJsonAsync<NpcSheetResponse>())!;
        sheet.AfinidadeAdicional.Should().Be(1);
        sheet.VocacaoArcanaMaxima.Should().Be(4); // Adepto sem coração de mana → Círculo 0 → 3, + 1
        sheet.VocacaoArcanaGasta.Should().Be(6);
    }

    // Ficha de Personagem 2.b, igual no NPC: Eficiência e Dano Elemental vêm da Tabela de Afinidades
    // (7 pontos no elemento da Afinidade → linha 7).
    [Fact]
    public async Task SubAttributes_derive_EficienciaElemental_and_DanoElemental_from_the_points_in_the_Afinidade()
    {
        var f = await FichaAsync();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{f.SheetId}", f.GmToken,
            ValidUpdate() with { Afinidade = "Fogo", AfinidadeAdicional = 10 }));
        var added = await AddAsync(f, new AddAffinityRequest("Fogo", 7, null, null, null, null));
        added.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{f.SheetId}/sub-attributes", f.GmToken));
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();

        body!.EficienciaElemental.Should().Be(4);
        body.DanoElemental.Should().Be(3);
    }
}
