using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterAffinitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterAffinitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        // Adepto libera Dobra+Consagração — cobre todas as combinações que os testes já existentes
        // usam: Fogo/Terra são Dobra, Curar/Aprimorar são Consagração.
        var setVocacao = new UpdateCharacterSheetRequest(null, "Ficha de Teste", null, null, "Adepto", null, null, null,
            false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, null, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, setVocacao));

        return sheetId;
    }

    private async Task<HttpResponseMessage> AddAsync(string token, string sheetId, string? elemento, string? segunda, int? subValor = null) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", token,
            new AddCharacterAffinityRequest(elemento, null, null, subValor, null, null, segunda, null)));

    private async Task<List<CharacterAffinityResponse>> ListAsync(string token, string sheetId) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", token)))
            .Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>())!;

    [Fact]
    public async Task Add_a_valid_combination_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm1", "aff1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer1", "affplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, 2, null, 10, "Vida", 4)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Elemento == "Fogo" && a.ElementoValor == 3 && a.SegundaEssencia == "Vida" && a.SegundaEssenciaValor == 4
            && a.SubElemento == "Curar" && a.SubElementoValor == 2 && a.Experiencia == 10);
    }

    [Fact]
    public async Task Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm3", "aff3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer3", "affplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Terra", 1, "Aprimorar", 1, "Vida", 5)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>();
        body!.Should().NotContain(a => a.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm4", "aff4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer4", "affplayer4@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer4b", "affplayer4b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", otherToken,
            new AddCharacterAffinityRequest("Terra", 1, "Aprimorar", 1, "Vida", 5)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm5", "aff5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer5", "affplayer5@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer5b", "affplayer5b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Add_with_every_field_null_returns_201_and_a_blank_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm6", "aff6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer6", "affplayer6@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        added!.Elemento.Should().BeNull();
        added.SubElemento.Should().BeNull();
        added.CaminhoNome.Should().BeNull();
        added.Experiencia.Should().BeNull();
    }

    [Fact]
    public async Task Add_with_only_Elemento_given_returns_201_without_checking_a_combination()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm7", "aff7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer7", "affplayer7@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        added!.Elemento.Should().Be("Fogo");
        added.SubElemento.Should().BeNull();
    }

    [Fact]
    public async Task Update_an_existing_affinity_returns_200_and_the_list_reflects_the_change()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm8", "aff8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer8", "affplayer8@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, 2, null, 10, "Vida", 4)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Terra", 5, null, 1, null, 8, "Vida", 4)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        updated!.Elemento.Should().Be("Terra");
        updated.ElementoValor.Should().Be(5);
        updated.SegundaEssencia.Should().Be("Vida");
        updated.SubElemento.Should().Be("Aprimorar");
        updated.SubElementoValor.Should().Be(1);
        updated.Experiencia.Should().Be(8);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Id == added.Id && a.Elemento == "Terra" && a.SubElemento == "Aprimorar");
    }

    [Fact]
    public async Task Update_can_clear_fields_back_to_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm9", "aff9@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer9", "affplayer9@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, 2, null, 10, "Vida", 4)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest(null, null, null, null, null, null)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        updated!.Elemento.Should().BeNull();
        updated.SubElemento.Should().BeNull();
        updated.SegundaEssencia.Should().BeNull();
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm10", "aff10@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer10", "affplayer10@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer10b", "affplayer10b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", otherToken,
            new UpdateCharacterAffinityRequest("Terra", 1, "Aprimorar", 1, "Vida", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_a_nonexistent_affinity_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm11", "aff11@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer11", "affplayer11@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{Guid.NewGuid()}", playerToken,
            new UpdateCharacterAffinityRequest("Terra", 1, "Aprimorar", 1, "Vida", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_does_not_flag_a_duplicate_against_its_own_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm15", "aff15@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer15", "affplayer15@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null, "Vida", null)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        // Reenviar a mesma linha com o mesmo Sub-Elemento derivado não deve se auto-rejeitar como duplicata.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 5, null, null, null, null, "Vida", null)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_does_not_reject_a_pre_existing_duplicate_left_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm16", "aff16@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer16", "affplayer16@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var addResponse1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null, "Vida", null)));
        var row1 = await addResponse1.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        // Simulate a pre-existing duplicate SubElemento inserted directly in the DB, bypassing the
        // controller's own duplicate check entirely — the API now refuses to create this via HTTP.
        Guid row2Id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            row2Id = Guid.NewGuid();
            db.CharacterAffinities.Add(new RuinaRPG.Infrastructure.CharacterSheets.CharacterAffinity
            {
                Id = row2Id, CharacterSheetId = Guid.Parse(sheetId), Elemento = Elemento.Fogo, SegundaEssencia = EssenciaBasica.Vida, SubElemento = SubElemento.Curar
            });
            await db.SaveChangesAsync();
        }

        // Re-send row2 with the same (now-colliding) SubElemento, changing only Experiencia — the
        // pre-existing collision is not newly introduced, so it must be grandfathered through.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{row2Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 1, null, null, null, 7, "Vida", null)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Ar", "Agua", "Gelo")]
    [InlineData("Agua", "Ar", "Gelo")]
    [InlineData("Terra", "Vida", "Aprimorar")]
    [InlineData("Agua", "Mundano", "Hemomancia")]
    public async Task Add_derives_the_SubElemento_from_the_two_essencias(string e1, string e2, string sub)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"AffEssGm{e1}{e2}", $"affessgm{e1}{e2}@teste.com".ToLowerInvariant());
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"AffEssP{e1}{e2}", $"affessp{e1}{e2}@teste.com".ToLowerInvariant());
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        (await AddAsync(playerToken, sheetId, e1, e2)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ListAsync(playerToken, sheetId)).Should().ContainSingle(a => a.Elemento == e1 && a.SegundaEssencia == e2 && a.SubElemento == sub);
    }

    [Fact]
    public async Task Add_ignores_a_SubElemento_sent_by_the_client()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssGmIgn", "affessgmign@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssPIgn", "affesspign@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", null, "Gelo", null, "Alma", null, "Terra", null)));

        (await ListAsync(playerToken, sheetId)).Should().ContainSingle(a => a.SubElemento == "Ferro");
    }

    [Theory]
    [InlineData("Ar", "Terra")]
    [InlineData("Agua", "Fogo")]
    [InlineData("Fogo", "Fogo")]
    [InlineData("Fogo", "Alma")]
    [InlineData("Ar", "Vida")]
    public async Task Add_with_essencias_that_dont_cross_returns_400(string e1, string e2)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"AffEssBadGm{e1}{e2}", $"affessbadgm{e1}{e2}@teste.com".ToLowerInvariant());
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"AffEssBadP{e1}{e2}", $"affessbadp{e1}{e2}@teste.com".ToLowerInvariant());
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await AddAsync(playerToken, sheetId, e1, e2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Essas duas Essências não se cruzam na Matriz Elemental.");
    }

    [Fact]
    public async Task Add_with_SegundaEssencia_but_no_Elemento_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssNoE1Gm", "affessnoe1gm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssNoE1P", "affessnoe1p@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await AddAsync(playerToken, sheetId, null, "Mundano");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Escolha a Essência Básica 1 antes da 2.");
    }

    [Fact]
    public async Task Add_rejects_a_second_row_with_the_same_SubElemento_but_allows_repeating_Essencia1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssDupGm", "affessdupgm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssDupP", "affessdupp@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await AddAsync(playerToken, sheetId, "Fogo", "Vida");

        var sameSub = await AddAsync(playerToken, sheetId, "Fogo", "Vida");
        var sameE1 = await AddAsync(playerToken, sheetId, "Fogo", "Mundano");

        sameSub.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await sameSub.Content.ReadAsStringAsync()).Should().Contain("Já existe uma linha de Afinidade com esse Sub-Elemento.");
        sameE1.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Campeao")]
    [InlineData("Feiticeiro")]
    public async Task Any_Vocacao_can_pick_any_essencias(string? vocacao)
    {
        var suffix = vocacao ?? "Nenhuma";
        var gmToken = await RegisterGmAndGetTokenAsync($"AffEssVocGm{suffix}", $"affessvocgm{suffix}@teste.com".ToLowerInvariant());
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"AffEssVocP{suffix}", $"affessvocp{suffix}@teste.com".ToLowerInvariant());
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var setVocacao = new UpdateCharacterSheetRequest(null, "Ficha de Teste", null, null, vocacao, null, null, null,
            false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, null, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, setVocacao));

        (await AddAsync(playerToken, sheetId, "Terra", "Mundano")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AddAsync(playerToken, sheetId, "Agua", "Alma")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_changing_only_values_keeps_a_legacy_SubElemento()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssLegGm", "affesslegm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssLegP", "affesslegp@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var legacyId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            db.CharacterAffinities.Add(new RuinaRPG.Infrastructure.CharacterSheets.CharacterAffinity
                { Id = legacyId, CharacterSheetId = Guid.Parse(sheetId), Elemento = RuinaRPG.Domain.CharacterSheets.Elemento.Ar, SubElemento = RuinaRPG.Domain.CharacterSheets.SubElemento.Curar });
            await db.SaveChangesAsync();
        }

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{legacyId}", playerToken,
            new UpdateCharacterAffinityRequest("Ar", 5, null, 3, null, 7, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListAsync(playerToken, sheetId)).Should().ContainSingle(a => a.SubElemento == "Curar" && a.ElementoValor == 5 && a.SubElementoValor == 3 && a.Experiencia == 7);
    }

    [Fact]
    public async Task Update_changing_an_essencia_recomputes_the_SubElemento()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssRecGm", "affessrecgm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssRecP", "affessrecp@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var added = await (await AddAsync(playerToken, sheetId, "Fogo", "Vida")).Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", null, null, null, null, null, "Mundano", null)));
        var afterChange = (await ListAsync(playerToken, sheetId)).Single();
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", null, null, null, null, null, null, null)));
        var afterClear = (await ListAsync(playerToken, sheetId)).Single();

        afterChange.SubElemento.Should().Be("Necromancia");
        afterClear.SubElemento.Should().BeNull();
        afterClear.SegundaEssencia.Should().BeNull();
    }
}
