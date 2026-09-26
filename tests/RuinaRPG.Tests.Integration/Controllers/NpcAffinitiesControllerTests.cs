using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcAffinitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcAffinitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        var sheetId = (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate()));

        return sheetId;
    }

    private static UpdateNpcSheetRequest ValidUpdate() => new(null, "Ficha de Teste", null, null, "Adepto", null, null, null,
        1, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, null, null);

    private Task<HttpResponseMessage> AddAsync(string gmToken, string sheetId, string? elemento, string? segundaEssencia) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest(elemento, null, segundaEssencia, null, null, null)));

    private async Task<List<NpcAffinityResponse>> ListAsync(string gmToken, string sheetId) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken)))
            .Content.ReadFromJsonAsync<List<NpcAffinityResponse>>())!;

    [Fact]
    public async Task Add_a_valid_combination_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm1", "npcaff1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Vida", 4, 2, 10)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Elemento == "Fogo" && a.ElementoValor == 3 && a.SegundaEssencia == "Vida" && a.SegundaEssenciaValor == 4
            && a.SubElemento == "Curar" && a.SubElementoValor == 2 && a.Experiencia == 10);
    }

    [Fact]
    public async Task Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm3", "npcaff3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Terra", 1, null, null, 1, 5)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();
        body!.Should().NotContain(a => a.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAffGmOwner4", "npcaffowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAffGmOther4", "npcaffother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmTokenOther,
            new AddNpcAffinityRequest("Terra", 1, null, null, 1, 5)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAffGmOwner5", "npcaffowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAffGmOther5", "npcaffother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_with_every_field_null_returns_201_and_a_blank_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm6", "npcaff6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<NpcAffinityResponse>();
        added!.Elemento.Should().BeNull();
        added.SubElemento.Should().BeNull();
    }

    [Fact]
    public async Task Update_an_existing_affinity_returns_200_and_the_list_reflects_the_change()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm7", "npcaff7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Vida", 4, 2, 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Terra", 5, "Vida", 4, 1, 8)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();
        updated!.Elemento.Should().Be("Terra");
        updated.SegundaEssencia.Should().Be("Vida");
        updated.SubElemento.Should().Be("Aprimorar");

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Id == added.Id && a.Elemento == "Terra" && a.SubElemento == "Aprimorar");
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcAffGmOwner8", "npcaffowner8@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcAffGmOther8", "npcaffother8@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmTokenOwner,
            new AddNpcAffinityRequest("Fogo", 3, null, null, 2, 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmTokenOther,
            new UpdateNpcAffinityRequest("Terra", 1, null, null, 1, 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_does_not_flag_a_duplicate_against_its_own_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm15", "npcaff15@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Vida", null, null, null)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 5, "Vida", null, null, null)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_does_not_reject_a_pre_existing_duplicate_left_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm16", "npcaff16@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        var addResponse1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Vida", null, null, null)));
        var row1 = await addResponse1.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        // Simulate a pre-existing duplicate SubElemento inserted directly in the DB, bypassing the
        // controller's own duplicate check entirely — the API now refuses to create this via HTTP.
        Guid row2Id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            row2Id = Guid.NewGuid();
            db.NpcAffinities.Add(new RuinaRPG.Infrastructure.NpcSheets.NpcAffinity
            {
                Id = row2Id, NpcSheetId = Guid.Parse(sheetId), Elemento = Elemento.Fogo, SegundaEssencia = EssenciaBasica.Vida, SubElemento = SubElemento.Curar
            });
            await db.SaveChangesAsync();
        }

        // Re-send row2 with the same (now-colliding) SubElemento, changing only Experiencia — the
        // pre-existing collision is not newly introduced, so it must be grandfathered through.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{row2Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 1, "Vida", null, null, 7)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_derives_the_SubElemento_from_the_two_essencias()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssGm1", "npcaffessgm1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", null, "Terra", 2, null, null)));
        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken)))
            .Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        list!.Should().ContainSingle(a => a.SubElemento == "Ferro" && a.SegundaEssencia == "Terra" && a.SegundaEssenciaValor == 2);
    }

    [Fact]
    public async Task Add_with_essencias_that_dont_cross_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssGm2", "npcaffessgm2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Agua", null, "Vida", null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_rejects_a_duplicate_SubElemento()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssGm3", "npcaffessgm3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Ar", null, "Agua", null, null, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Agua", null, "Ar", null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_with_SegundaEssencia_but_no_Elemento_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssNoE1Gm", "npcaffessnoe1gm@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await AddAsync(gmToken, sheetId, null, "Mundano");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Escolha a Essência Básica 1 antes da 2.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Campeao")]
    [InlineData("Feiticeiro")]
    public async Task Any_Vocacao_can_pick_any_essencias(string? vocacao)
    {
        var suffix = vocacao ?? "Nenhuma";
        var gmToken = await RegisterGmAndGetTokenAsync($"NpcAffEssVocGm{suffix}", $"npcaffessvocgm{suffix}@teste.com".ToLowerInvariant());
        var sheetId = await CreateSheetAsync(gmToken);
        var setVocacao = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Vocacao = vocacao }));
        setVocacao.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AddAsync(gmToken, sheetId, "Terra", "Mundano")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AddAsync(gmToken, sheetId, "Agua", "Alma")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_changing_only_values_keeps_a_legacy_SubElemento()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssLegGm", "npcaffesslegm@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var legacyId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.NpcAffinities.Add(new RuinaRPG.Infrastructure.NpcSheets.NpcAffinity
                { Id = legacyId, NpcSheetId = Guid.Parse(sheetId), Elemento = Elemento.Ar, SubElemento = SubElemento.Curar });
            await db.SaveChangesAsync();
        }

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{legacyId}", gmToken,
            new UpdateNpcAffinityRequest("Ar", 5, null, null, 3, 7)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListAsync(gmToken, sheetId)).Should().ContainSingle(a => a.SubElemento == "Curar" && a.ElementoValor == 5 && a.SubElementoValor == 3 && a.Experiencia == 7);
    }

    [Fact]
    public async Task Update_changing_an_essencia_recomputes_the_SubElemento()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssRecGm", "npcaffessrecgm@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var added = await (await AddAsync(gmToken, sheetId, "Fogo", "Vida")).Content.ReadFromJsonAsync<NpcAffinityResponse>();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", null, "Mundano", null, null, null)));
        var afterChange = (await ListAsync(gmToken, sheetId)).Single();
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", null, null, null, null, null)));
        var afterClear = (await ListAsync(gmToken, sheetId)).Single();

        afterChange.SubElemento.Should().Be("Necromancia");
        afterClear.SubElemento.Should().BeNull();
        afterClear.SegundaEssencia.Should().BeNull();
    }
}
