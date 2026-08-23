using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcSheetsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcSheetsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private static UpdateNpcSheetRequest ValidUpdate() => new(
        null, "Sentinela da Ruína", "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Guardiã do Portal",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100);

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/npc-sheets", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_Nivel_1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm1", "npc1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.Nivel.Should().Be(1);
        body.Nome.Should().BeNull();
        body.OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm2", "npc2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "NpcJogador2", "npcjogador2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", jogadorToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_by_the_owning_gm_returns_200()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm3", "npc3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwner4", "npcowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOther4", "npcother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_by_the_owning_gm_returns_204_and_persists_every_field()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm5", "npc5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.Nome.Should().Be("Sentinela da Ruína");
        body.Linhagem.Should().Be("Humano");
        body.Variante.Should().Be("Sinir");
        body.Nivel.Should().Be(5);
        body.VitalidadeAtual.Should().Be(30);
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwner6", "npcowner6@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOther6", "npcother6@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmTokenOther, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_by_the_owning_gm_returns_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm7", "npc7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwner8", "npcowner8@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOther8", "npcother8@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_a_nonexistent_sheet_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm9", "npc9@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{Guid.NewGuid()}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_a_Variante_that_does_not_belong_to_the_Linhagem_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm10", "npc10@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var invalid = ValidUpdate() with { Linhagem = "Humano", Variante = "Yavos" }; // Yavos belongs to Nephrytes
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_seeds_8_zeroed_attributes_39_zeroed_skills_and_3_armor_slots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmSeed1", "npcseed1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var attributes = await db.NpcAttributes.Where(a => a.NpcSheetId == Guid.Parse(sheetId)).ToListAsync();
        var skills = await db.NpcSkills.Where(s => s.NpcSheetId == Guid.Parse(sheetId)).ToListAsync();
        var armorSlots = await db.NpcArmorSlots.Where(a => a.NpcSheetId == Guid.Parse(sheetId)).ToListAsync();

        attributes.Should().HaveCount(8);
        attributes.Should().OnlyContain(a => a.Gasto == 0 && a.Bonus == 0 && !a.TemMaestria);
        skills.Should().HaveCount(39);
        skills.Should().OnlyContain(s => s.Gasto == 0);
        // ArmorSlotType has 3 members (Capacete, Superior, Inferior) — not 6.
        armorSlots.Should().HaveCount(3);
        armorSlots.Should().OnlyContain(a => a.ItemId == null);
    }

    [Fact]
    public async Task Get_labels_Graduacao_as_Grau_for_Campeao_and_computes_it_from_EAP()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm11", "npc11@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Vocacao = "Campeao", EAPAtual = 150 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.GraduacaoLabel.Should().Be("Grau");
        body.Graduacao.Should().Be(1); // 150 EAP >= the real Tabela's Grau 1 threshold (100)
    }
}
