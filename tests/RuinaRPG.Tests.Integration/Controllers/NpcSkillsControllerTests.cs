using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcSkillsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcSkillsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    [Fact]
    public async Task List_returns_39_skills_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGm1", "npcskill1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();
        body!.Should().HaveCount(39);
    }

    [Fact]
    public async Task Update_a_skill_sets_Gasto_and_the_response_computes_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGm2", "npcskill2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateNpcSkillRequest(9, null)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Modificador.Should().Be(3); // 9 / 3
    }

    [Fact]
    public async Task Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGm4", "npcskill4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Forca: Gasto 4, Bonus 0, no maestria -> Total 4 (AttributeTotalCalculator.Total)
        var attrResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));
        attrResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Atletismo: Gasto 9 -> Modificador 3 (SkillFormulas.Modificador), Atributo escolhido Forca
        var skillResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateNpcSkillRequest(9, "Forca")));
        skillResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();

        var atletismo = body!.Single(s => s.Pericia == "Atletismo");
        atletismo.AtributoEscolhido.Should().Be("Forca");
        atletismo.Modificador.Should().Be(3);
        atletismo.Total.Should().Be(7); // SkillFormulas.Total(modificador: 3, atributoTotal: 4)

        var acrobacia = body!.Single(s => s.Pericia == "Acrobacia");
        acrobacia.AtributoEscolhido.Should().BeNull();
        acrobacia.Total.Should().BeNull();
    }

    [Fact]
    public async Task List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGm6", "npcskill6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var beforeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var before = (await beforeResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>())!;
        before.Select(s => s.Pericia).Should().BeInAscendingOrder(StringComparer.Ordinal).And.HaveCount(39);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Sobrevivencia", gmToken,
            new UpdateNpcSkillRequest(3, null)));

        var afterResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>())!;
        after.Select(s => s.Pericia).Should().Equal(before.Select(s => s.Pericia));
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcSkillGmOwner3", "npcskillowner3@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcSkillGmOther3", "npcskillother3@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gmTokenOther,
            new UpdateNpcSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcSkillGmOwner5", "npcskillowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcSkillGmOther5", "npcskillother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
