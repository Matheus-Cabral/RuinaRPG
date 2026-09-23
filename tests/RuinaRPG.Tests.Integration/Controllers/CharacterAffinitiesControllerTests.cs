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

    [Fact]
    public async Task Add_a_valid_combination_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm1", "aff1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer1", "affplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Elemento == "Fogo" && a.ElementoValor == 3 && a.SubElemento == "Curar" && a.SubElementoValor == 2
            && a.CaminhoNome == "Vida" && a.Experiencia == 10);
    }

    [Fact]
    public async Task Add_an_invalid_Elemento_SubElemento_combination_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm2", "aff2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer2", "affplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Ar", 0, "Ferro", 0, null, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
            new AddCharacterAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Terra", 5, "Aprimorar", 1, "Vida", 8)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        updated!.Elemento.Should().Be("Terra");
        updated.ElementoValor.Should().Be(5);
        updated.SubElemento.Should().Be("Aprimorar");
        updated.SubElementoValor.Should().Be(1);
        updated.CaminhoNome.Should().Be("Vida");
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
            new AddCharacterAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest(null, null, null, null, null, null)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        updated!.Elemento.Should().BeNull();
        updated.SubElemento.Should().BeNull();
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
    public async Task Add_rejects_an_Elemento_not_liberado_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm12", "aff12@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer12", "affplayer12@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto (Dobra+Consagração)

        // Necromancia é de Maculação — Adepto não libera.
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest(null, null, "Necromancia", 1, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_an_old_SubElemento_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm13", "aff13@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer13", "affplayer13@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        // Troca a Vocação pra Feiticeiro (não libera mais Curar, que é de Consagração).
        var updateSheet = new UpdateCharacterSheetRequest(null, "Ficha de Teste", null, null, "Feiticeiro", null, null, null,
            false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, null, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, updateSheet));

        // Reenvia a mesma linha sem mudar Elemento/Sub-Elemento — não deve ser bloqueado.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_rejects_a_duplicate_Elemento_already_used_by_another_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm14", "aff14@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer14", "affplayer14@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 5, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_does_not_flag_a_duplicate_against_its_own_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm15", "aff15@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer15", "affplayer15@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        // Reenviar a mesma linha com o mesmo Elemento não deve se auto-rejeitar como duplicata.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 5, null, null, null, null)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_does_not_reject_a_pre_existing_duplicate_left_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm16", "aff16@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer16", "affplayer16@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var addResponse1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null)));
        var row1 = await addResponse1.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var addResponse2 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Terra", 1, null, null, null, null)));
        var row2 = await addResponse2.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        // Simulate legacy pre-branch data: force row2's Elemento to collide with row1's directly
        // in the DB, bypassing the controller's own duplicate check entirely.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var row2Entity = await db.CharacterAffinities.SingleAsync(a => a.Id == Guid.Parse(row2!.Id));
            row2Entity.Elemento = Elemento.Fogo;
            await db.SaveChangesAsync();
        }

        // Re-send row2 with the same (now-colliding) Elemento, changing only Experiencia — the
        // pre-existing collision is not newly introduced, so it must be grandfathered through.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{row2!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 1, null, null, null, 7)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_a_Curar_row_with_Fogo_and_the_Vida_Caminho_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm17", "aff17@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer17", "affplayer17@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 1, "Curar", 1, "Vida", 0)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("Fogo", "Mundano")] // Caminho errado
    [InlineData("Fogo", "Alma")]    // Caminho errado
    [InlineData("Fogo", null)]      // sem Caminho
    [InlineData(null, "Vida")]      // sem Elemento
    [InlineData("Ar", "Vida")]      // Elemento fora da Matriz para Curar
    public async Task Add_a_Curar_row_without_both_Fogo_and_the_Vida_Caminho_returns_400(string? elemento, string? caminho)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"AffGm18{elemento}{caminho}", $"aff18{elemento}{caminho}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"AffPlayer18{elemento}{caminho}", $"affplayer18{elemento}{caminho}@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest(elemento, 1, "Curar", 1, caminho, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_rejects_a_free_text_Caminho()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm19", "aff19@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer19", "affplayer19@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 1, null, null, "Caminho da Fênix", 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_rejects_changing_the_Caminho_out_from_under_a_Curar_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm20", "aff20@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer20", "affplayer20@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 1, "Curar", 1, "Vida", 0)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 1, "Curar", 1, "Mundano", 0)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_a_legacy_free_text_Caminho_row_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm21", "aff21@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer21", "affplayer21@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        Guid rowId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            rowId = Guid.NewGuid();
            db.CharacterAffinities.Add(new RuinaRPG.Infrastructure.CharacterSheets.CharacterAffinity
            {
                Id = rowId, CharacterSheetId = Guid.Parse(sheetId), Elemento = Elemento.Fogo, SubElemento = SubElemento.Curar,
                CaminhoNome = "Caminho da Fênix", Experiencia = 3
            });
            await db.SaveChangesAsync();
        }

        // Só a Experiência muda — Elemento/Sub-Elemento/Caminho iguais ao salvo.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{rowId}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", null, "Curar", null, "Caminho da Fênix", 9)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Alma e Vida são Caminhos (dropdown próprio), não Sub-Elementos — Requisitos - Ficha de Personagem 2.c.
    [Theory]
    [InlineData("Ar", "Alma")]
    [InlineData("Fogo", "Vida")]
    public async Task Add_rejects_a_new_Alma_or_Vida_as_SubElemento(string elemento, string subElemento)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"AlmaVidaGmChA1{subElemento}", $"almavidaChA1{subElemento}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"AlmaVidaPChA1{subElemento}", $"almavidapChA1{subElemento}@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest(elemento, 1, subElemento, 1, null, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_rejects_changing_the_SubElemento_to_Vida()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AlmaVidaGmChB1", "almavidaChB1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AlmaVidaPChB1", "almavidapChB1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 1, "Curar", 1, "Vida", 0)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 1, "Vida", 1, "Vida", 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_a_legacy_row_whose_SubElemento_is_Vida_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AlmaVidaGmChC1", "almavidaChC1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AlmaVidaPChC1", "almavidapChC1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        Guid rowId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            rowId = Guid.NewGuid();
            db.CharacterAffinities.Add(new RuinaRPG.Infrastructure.CharacterSheets.CharacterAffinity
            {
                Id = rowId, CharacterSheetId = Guid.Parse(sheetId), Elemento = Elemento.Fogo, SubElemento = SubElemento.Vida, Experiencia = 3
            });
            await db.SaveChangesAsync();
        }

        // Só a Experiência muda — Elemento/Sub-Elemento/Caminho iguais ao salvo.
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{rowId}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", null, "Vida", null, null, 9)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
