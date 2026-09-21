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

        var setVocacao = new UpdateNpcSheetRequest(null, "Ficha de Teste", null, null, "Adepto", null, null, null,
            1, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, setVocacao));

        return sheetId;
    }

    [Fact]
    public async Task Add_a_valid_combination_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm1", "npcaff1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Elemento == "Fogo" && a.ElementoValor == 3 && a.SubElemento == "Curar" && a.SubElementoValor == 2
            && a.CaminhoNome == "Vida" && a.Experiencia == 10);
    }

    [Fact]
    public async Task Add_an_invalid_Elemento_SubElemento_combination_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm2", "npcaff2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Ar", 0, "Ferro", 0, null, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm3", "npcaff3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Terra", 1, "Aprimorar", 1, "Vida", 5)));
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
            new AddNpcAffinityRequest("Terra", 1, "Aprimorar", 1, "Vida", 5)));

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
            new AddNpcAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Terra", 5, "Aprimorar", 1, "Vida", 8)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();
        updated!.Elemento.Should().Be("Terra");
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
            new AddNpcAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmTokenOther,
            new UpdateNpcAffinityRequest("Terra", 1, "Aprimorar", 1, "Vida", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_rejects_an_Elemento_not_liberado_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm12", "npcaff12@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto (Dobra+Consagração)

        // Necromancia é de Maculação — Adepto não libera.
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest(null, null, "Necromancia", 1, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_an_old_SubElemento_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm13", "npcaff13@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        // Troca a Vocação pra Feiticeiro (não libera mais Vida, que é de Consagração).
        var updateSheet = new UpdateNpcSheetRequest(null, "Ficha de Teste", null, null, "Feiticeiro", null, null, null,
            1, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, updateSheet));

        // Reenvia a mesma linha sem mudar Elemento/Sub-Elemento — não deve ser bloqueado.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 3, "Curar", 2, "Vida", 10)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_rejects_a_duplicate_Elemento_already_used_by_another_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm14", "npcaff14@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, null, null, null, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 5, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_does_not_flag_a_duplicate_against_its_own_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm15", "npcaff15@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, null, null, null, null)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 5, null, null, null, null)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_does_not_reject_a_pre_existing_duplicate_left_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm16", "npcaff16@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        var addResponse1 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, null, null, null, null)));
        var row1 = await addResponse1.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var addResponse2 = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Terra", 1, null, null, null, null)));
        var row2 = await addResponse2.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        // Simulate legacy pre-branch data: force row2's Elemento to collide with row1's directly
        // in the DB, bypassing the controller's own duplicate check entirely.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var row2Entity = await db.NpcAffinities.SingleAsync(a => a.Id == Guid.Parse(row2!.Id));
            row2Entity.Elemento = Elemento.Fogo;
            await db.SaveChangesAsync();
        }

        // Re-send row2 with the same (now-colliding) Elemento, changing only Experiencia — the
        // pre-existing collision is not newly introduced, so it must be grandfathered through.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{row2!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 1, null, null, null, 7)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_a_Curar_row_with_Fogo_and_the_Vida_Caminho_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm17", "npcaff17@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 1, "Curar", 1, "Vida", 0)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("Fogo", "Mundano")]
    [InlineData("Fogo", "Alma")]
    [InlineData("Fogo", null)]
    [InlineData(null, "Vida")]
    [InlineData("Ar", "Vida")]
    public async Task Add_a_Curar_row_without_both_Fogo_and_the_Vida_Caminho_returns_400(string? elemento, string? caminho)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"NpcAffGm18{elemento}{caminho}", $"npcaff18{elemento}{caminho}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest(elemento, 1, "Curar", 1, caminho, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_rejects_a_free_text_Caminho()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm19", "npcaff19@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 1, null, null, "Caminho da Fênix", 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_rejects_changing_the_Caminho_out_from_under_a_Curar_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm20", "npcaff20@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 1, "Curar", 1, "Vida", 0)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 1, "Curar", 1, "Mundano", 0)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_a_legacy_free_text_Caminho_row_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm21", "npcaff21@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        Guid rowId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            rowId = Guid.NewGuid();
            db.NpcAffinities.Add(new RuinaRPG.Infrastructure.NpcSheets.NpcAffinity
            {
                Id = rowId, NpcSheetId = Guid.Parse(sheetId), Elemento = Elemento.Fogo, SubElemento = SubElemento.Curar,
                CaminhoNome = "Caminho da Fênix", Experiencia = 3
            });
            await db.SaveChangesAsync();
        }

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{rowId}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", null, "Curar", null, "Caminho da Fênix", 9)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Alma e Vida são Caminhos (dropdown próprio), não Sub-Elementos — Requisitos - Ficha de Personagem 2.c.
    [Theory]
    [InlineData("Ar", "Alma")]
    [InlineData("Fogo", "Vida")]
    public async Task Add_rejects_a_new_Alma_or_Vida_as_SubElemento(string elemento, string subElemento)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"AlmaVidaGmNpA1{subElemento}", $"almavidaNpA1{subElemento}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest(elemento, 1, subElemento, 1, null, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_rejects_changing_the_SubElemento_to_Vida()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AlmaVidaGmNpB1", "almavidaNpB1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 1, "Curar", 1, "Vida", 0)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 1, "Vida", 1, "Vida", 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_a_legacy_row_whose_SubElemento_is_Vida_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AlmaVidaGmNpC1", "almavidaNpC1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        Guid rowId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            rowId = Guid.NewGuid();
            db.NpcAffinities.Add(new RuinaRPG.Infrastructure.NpcSheets.NpcAffinity
            {
                Id = rowId, NpcSheetId = Guid.Parse(sheetId), Elemento = Elemento.Fogo, SubElemento = SubElemento.Vida, Experiencia = 3
            });
            await db.SaveChangesAsync();
        }

        // Só a Experiência muda — Elemento/Sub-Elemento/Caminho iguais ao salvo.
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{rowId}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", null, "Vida", null, null, 9)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
