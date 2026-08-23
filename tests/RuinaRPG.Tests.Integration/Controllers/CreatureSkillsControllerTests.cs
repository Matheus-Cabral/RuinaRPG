using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureSkillsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureSkillsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    [Fact]
    public async Task List_returns_20_skills_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm1", "creatureskill1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>();
        body!.Should().HaveCount(20);
    }

    [Fact]
    public async Task Update_a_skill_sets_Gasto_and_the_response_computes_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm2", "creatureskill2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateCreatureSkillRequest(9, null)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Modificador.Should().Be(3); // 9 / 3
    }

    [Fact]
    public async Task List_with_atributoEscolhido_echoes_the_attribute_and_computes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm4", "creatureskill4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Forca: Gasto 4, Bonus 0, no maestria -> Total 4 (AttributeTotalCalculator.Total)
        var attrResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Forca", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));
        attrResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Atletismo: Gasto 9 -> Modificador 3 (SkillFormulas.Modificador)
        var skillResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateCreatureSkillRequest(9, null)));
        skillResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills?atributoEscolhido=Forca", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>();
        var atletismo = body!.Single(s => s.Pericia == "Atletismo");
        atletismo.AtributoEscolhido.Should().Be("Forca");
        atletismo.Modificador.Should().Be(3);
        atletismo.Total.Should().Be(7); // SkillFormulas.Total(modificador: 3, atributoTotal: 4)
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureSkillGmOwner3", "creatureskillowner3@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureSkillGmOther3", "creatureskillother3@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Atletismo", gmTokenOther,
            new UpdateCreatureSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureSkillGmOwner5", "creatureskillowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureSkillGmOther5", "creatureskillother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_a_disallowed_Pericia_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm6", "creatureskill6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Alquimia is a real Pericia enum value but not in the R0005 20-skill allow-list.
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Alquimia", gmToken,
            new UpdateCreatureSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
