using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureSheetsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureSheetsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    private static UpdateCreatureSheetRequest ValidUpdate() => new(
        null, "Lobo das Ruínas", "Lobo", "Fisico", "Predador", "Terra", "Alfa da Matilha",
        "F", 3, 200, 5, 12, 8, 10, "Parcial");

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/creature-sheets", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_Nivel_1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm1", "criatura1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.Nivel.Should().Be(1);
        body.Nome.Should().BeNull();
        body.OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm2", "criatura2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "CreatureJogador2", "criaturajogador2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", jogadorToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_by_the_owning_gm_returns_200()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm3", "criatura3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureGmOwner4", "criaturaowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureGmOther4", "criaturaother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_by_the_owning_gm_returns_204_and_persists_every_field()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm5", "criatura5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.Nome.Should().Be("Lobo das Ruínas");
        body.Raca.Should().Be("Lobo");
        body.Arquetipo.Should().Be("Fisico");
        body.Rank.Should().Be("F");
        body.Nivel.Should().Be(3);
        body.VitalidadeAtual.Should().Be(12);
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureGmOwner6", "criaturaowner6@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureGmOther6", "criaturaother6@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmTokenOther, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_by_the_owning_gm_returns_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm7", "criatura7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureGmOwner8", "criaturaowner8@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureGmOther8", "criaturaother8@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_a_nonexistent_sheet_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm9", "criatura9@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{Guid.NewGuid()}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_seeds_6_zeroed_attributes_20_zeroed_skills_and_3_armor_slots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSeed1", "creatureseed1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var attributes = await db.CreatureAttributes.Where(a => a.CreatureSheetId == Guid.Parse(sheetId)).ToListAsync();
        var skills = await db.CreatureSkills.Where(s => s.CreatureSheetId == Guid.Parse(sheetId)).ToListAsync();
        var armorSlots = await db.CreatureArmorSlots.Where(a => a.CreatureSheetId == Guid.Parse(sheetId)).ToListAsync();

        // AtributoCriatura has 6 members (not Atributo's 8).
        attributes.Should().HaveCount(6);
        attributes.Should().OnlyContain(a => a.Gasto == 0 && a.Bonus == 0 && !a.TemMaestria);
        // The R0005 allow-list has 20 members (not all 39 Pericia values, unlike Ficha de NPCs).
        skills.Should().HaveCount(20);
        skills.Should().OnlyContain(s => s.Gasto == 0);
        skills.Select(s => s.Pericia).Should().BeEquivalentTo(CreatureSkillAllowList.AllowedPericias);
        // ArmorSlotType has 3 members (Capacete, Superior, Inferior) — not 6.
        armorSlots.Should().HaveCount(3);
        armorSlots.Should().OnlyContain(a => a.ItemId == null);
    }

    [Fact]
    public async Task Get_computes_Kill_and_Assistencia_from_ExperienciaAtual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmXp1", "criaturaxp1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { ExperienciaAtual = 100 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.Kill.Should().Be(15);
        body.Assistencia.Should().Be(12);
    }

    // vigorTotal/astuciaTotal are hardcoded to 0 in this task (CreatureAttribute doesn't exist
    // until Task 3), so VitalidadeMaximo/FocoMaximo reduce to statusVida/statusFoco alone (0*2 +
    // status). Real excerpt from Docs/Sistema RPG/Tabela de Arquetipos.md, Nível 1:
    // Fisico -> Vida 8, Arcana 4; Arcano -> Vida 4, Arcana 8. Exercising both proves the plain
    // Arquetipo.ToString() lookup (no accent-mapping helper needed, unlike Vocacao) works for
    // both enum members.
    [Fact]
    public async Task Get_computes_Vitalidade_Foco_and_Adrenalina_Maximo_for_Fisico_Arquetipo()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmMax1", "criaturamax1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Arquetipo = "Fisico", Nivel = 1 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(8);
        body.FocoMaximo.Should().Be(4);
        body.AdrenalinaMaximo.Should().Be(10); // 10 + Artefato bonus (não modelado ainda → 0)
    }

    [Fact]
    public async Task Get_computes_Vitalidade_and_Foco_Maximo_for_Arcano_Arquetipo()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmMax2", "criaturamax2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Arquetipo = "Arcano", Nivel = 1 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(4);
        body.FocoMaximo.Should().Be(8);
    }
}
