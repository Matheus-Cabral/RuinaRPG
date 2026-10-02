using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RacialAbilitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RacialAbilitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    [Fact]
    public async Task ListRacialAbilities_returns_all_8_Variantes_with_defaults_when_no_overrides_exist()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm1", "racial1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        body!.Should().HaveCount(8);
        body!.Should().OnlyContain(e => e.IsDefault);
        body!.Single(e => e.Variante == "Sinir").Nome.Should().Be("Racial (Arca)");
        body!.Single(e => e.Variante == "Sinir").Descricao.Should().Be("Role 1d20 na tabela de Arcas.");
        body!.Single(e => e.Variante == "Alora").Nome.Should().Be("Racial (Amplificador Místico)");
    }

    [Fact]
    public async Task UpdateRacialAbility_persists_the_override_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm2", "racial2@teste.com");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Alora", gmToken,
            new UpdateRacialAbilityRequest("Racial (Custom)", "Um texto novo definido pelo GM.")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        var alora = body!.Single(e => e.Variante == "Alora");
        alora.Nome.Should().Be("Racial (Custom)");
        alora.Descricao.Should().Be("Um texto novo definido pelo GM.");
        alora.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRacialAbility_called_twice_updates_the_same_row_instead_of_duplicating()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm3", "racial3@teste.com");

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Yavos", gmToken, new UpdateRacialAbilityRequest("V1", "D1")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Yavos", gmToken, new UpdateRacialAbilityRequest("V2", "D2")));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        body!.Should().ContainSingle(e => e.Variante == "Yavos");
        body!.Single(e => e.Variante == "Yavos").Nome.Should().Be("V2");
    }

    [Fact]
    public async Task DeleteRacialAbilityOverride_reverts_to_the_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm4", "racial4@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Koroanos", gmToken, new UpdateRacialAbilityRequest("Custom", "Custom desc")));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/racial-abilities/Koroanos", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();
        var koroanos = body!.Single(e => e.Variante == "Koroanos");
        koroanos.Nome.Should().Be("Racial (Sobre Voo)");
        koroanos.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteRacialAbilityOverride_on_a_Variante_with_no_override_is_a_no_op_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm5", "racial5@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/racial-abilities/PhylacTai", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateRacialAbility_with_an_unknown_Variante_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm6", "racial6@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/NaoExiste", gmToken, new UpdateRacialAbilityRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRacialAbility_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm7", "racial7@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialPlayer7", "racialplayer7@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Alora", playerToken, new UpdateRacialAbilityRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListRacialAbilities_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm8", "racial8@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialPlayer8", "racialplayer8@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListArcas_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm12", "racial12@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialPlayer12", "racialplayer12@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListArcas_returns_20_entries_with_null_fields_when_unset()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm9", "racial9@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        body!.Should().HaveCount(20);
        body!.Select(a => a.Roll).Should().BeEquivalentTo(Enumerable.Range(1, 20));
        body!.Should().OnlyContain(a => a.Nome == null && a.Descricao == null);
    }

    [Fact]
    public async Task UpdateArca_persists_the_entry_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm10", "racial10@teste.com");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/7", gmToken,
            new UpdateArcaEntryRequest("A Chama Eterna", "Concede resistência ao fogo por 1 cena.")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        var entry = body!.Single(a => a.Roll == 7);
        entry.Nome.Should().Be("A Chama Eterna");
        entry.Descricao.Should().Be("Concede resistência ao fogo por 1 cena.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task UpdateArca_with_a_roll_outside_1_to_20_returns_400(int invalidRoll)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"RacialGmRoll{invalidRoll}", $"racialroll{invalidRoll}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/{invalidRoll}", gmToken, new UpdateArcaEntryRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateArca_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialGm11", "racial11@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialPlayer11", "racialplayer11@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/1", playerToken, new UpdateArcaEntryRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListRacialTraits_returns_all_7_Variantes_with_the_Sistema_Basico_defaults()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialTraitGm1", "racialtrait1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-traits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RacialTraitSlotsEntryResponse>>();
        body!.Should().HaveCount(8);
        body!.Should().OnlyContain(e => e.IsDefault);

        var sinir = body!.Single(e => e.Variante == "Sinir");
        sinir.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Alfabetizado", "Sedutor", "Aparência Inofensiva (2 pontos)");
        sinir.Obrigatoria.Should().BeEmpty();

        var alora = body!.Single(e => e.Variante == "Alora");
        alora.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Detectar Magia", "Amado por feras");
        alora.Obrigatoria.Should().ContainSingle(o => o.TraitNome == "Desvantagem Elemental" && o.Especificacao == "Fogo");
    }

    [Fact]
    public async Task UpdateRacialTraitSlots_persists_the_override_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialTraitGm2", "racialtrait2@teste.com");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-traits/Sinir", gmToken,
            new UpdateRacialTraitSlotsRequest(
                [new RacialTraitOptionRequest("Alfabetizado", null)],
                [])));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-traits", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialTraitSlotsEntryResponse>>();
        var sinir = body!.Single(e => e.Variante == "Sinir");
        sinir.IsDefault.Should().BeFalse();
        sinir.Gratuita.Should().ContainSingle(o => o.TraitNome == "Alfabetizado");
    }

    [Fact]
    public async Task UpdateRacialTraitSlots_with_an_empty_Gratuita_list_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialTraitGm3", "racialtrait3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-traits/Yavos", gmToken,
            new UpdateRacialTraitSlotsRequest([], [])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteRacialTraitOverride_reverts_to_the_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialTraitGm4", "racialtrait4@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-traits/Koroanos", gmToken,
            new UpdateRacialTraitSlotsRequest([new RacialTraitOptionRequest("Alfabetizado", null)], [])));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/racial-traits/Koroanos", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-traits", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RacialTraitSlotsEntryResponse>>();
        var koroanos = body!.Single(e => e.Variante == "Koroanos");
        koroanos.IsDefault.Should().BeTrue();
        koroanos.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Saque Rápido", "Visão Noturna");
    }

    [Fact]
    public async Task UpdateRacialTraitSlots_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RacialTraitGm5", "racialtrait5@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "RacialTraitPlayer5", "racialtraitplayer5@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-traits/Alora", playerToken,
            new UpdateRacialTraitSlotsRequest([new RacialTraitOptionRequest("Detectar Magia", null)], [])));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddEvolucao_on_an_unfilled_roll_creates_the_Arca_row_and_List_returns_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm1", "arcaevo1@teste.com");

        var create = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/4/evolucoes", gmToken,
            new ArcaEvolucaoRequest(5, "A chama queima mais forte.")));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();
        created!.Nivel.Should().Be(5);

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken)))
            .Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        var row = list!.Single(a => a.Roll == 4);
        row.Nome.Should().Be("");
        row.Evolucoes.Should().ContainSingle().Which.Descricao.Should().Be("A chama queima mais forte.");
        list!.Single(a => a.Roll == 5).Evolucoes.Should().BeEmpty();
    }

    [Fact]
    public async Task List_returns_evolucoes_ordered_by_level_then_creation()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm2", "arcaevo2@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/2/evolucoes", gmToken, new ArcaEvolucaoRequest(10, "dez")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/2/evolucoes", gmToken, new ArcaEvolucaoRequest(3, "tres-a")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/2/evolucoes", gmToken, new ArcaEvolucaoRequest(3, "tres-b")));

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken)))
            .Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();

        list!.Single(a => a.Roll == 2).Evolucoes.Select(e => e.Descricao).Should().Equal("tres-a", "tres-b", "dez");
    }

    [Fact]
    public async Task UpdateEvolucao_changes_level_and_text()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm3", "arcaevo3@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/1/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "antes"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/1/evolucoes/{created!.Id}", gmToken, new ArcaEvolucaoRequest(7, "depois")));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken)))
            .Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        var ev = list!.Single(a => a.Roll == 1).Evolucoes.Single();
        ev.Nivel.Should().Be(7);
        ev.Descricao.Should().Be("depois");
    }

    [Fact]
    public async Task DeleteEvolucao_removes_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm4", "arcaevo4@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/9/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/arcas/9/evolucoes/{created!.Id}", gmToken));

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken)))
            .Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        list!.Single(a => a.Roll == 9).Evolucoes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1, "texto")]
    [InlineData(21, 1, "texto")]
    [InlineData(1, 0, "texto")]
    [InlineData(1, 51, "texto")]
    [InlineData(1, 5, "   ")]
    public async Task AddEvolucao_with_invalid_input_returns_400(int roll, int nivel, string descricao)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"ArcaEvoGmV{roll}{nivel}{descricao.Length}", $"arcaevov{roll}{nivel}{descricao.Length}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/arcas/{roll}/evolucoes", gmToken, new ArcaEvolucaoRequest(nivel, descricao)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateEvolucao_with_invalid_level_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm5", "arcaevo5@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/1/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/1/evolucoes/{created!.Id}", gmToken, new ArcaEvolucaoRequest(51, "x")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_or_delete_of_another_gms_evolucao_returns_404()
    {
        var ownerToken = await RegisterGmAndGetTokenAsync("ArcaEvoOwner", "arcaevoowner@teste.com");
        var otherToken = await RegisterGmAndGetTokenAsync("ArcaEvoOther", "arcaevoother@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", ownerToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/3/evolucoes/{created!.Id}", otherToken, new ArcaEvolucaoRequest(2, "y"))))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/arcas/3/evolucoes/{created.Id}", otherToken)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_of_an_evolucao_through_the_wrong_roll_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm6", "arcaevo6@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/4/evolucoes/{created!.Id}", gmToken, new ArcaEvolucaoRequest(2, "y")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddEvolucao_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm7", "arcaevo7@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "ArcaEvoJog7", "arcaevojog7@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", jogadorToken, new ArcaEvolucaoRequest(2, "x")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task SetDadoAsync(string token, int dado) =>
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/dado", token, new ArcaDadoRequest(dado))))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task<List<ArcaEntryResponse>> ListArcasAsync(string token) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", token))).Content.ReadFromJsonAsync<List<ArcaEntryResponse>>())!;

    [Fact]
    public async Task GetDado_defaults_to_20_when_the_GM_never_chose()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaDadoGm1", "arcadado1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas/dado", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ArcaDadoResponse>())!.Dado.Should().Be(20);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(100)]
    public async Task SetDado_resizes_the_table_to_one_row_per_face(int dado)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"ArcaDadoGmS{dado}", $"arcadados{dado}@teste.com");

        await SetDadoAsync(gmToken, dado);

        var dadoBody = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas/dado", gmToken))).Content.ReadFromJsonAsync<ArcaDadoResponse>();
        dadoBody!.Dado.Should().Be(dado);
        (await ListArcasAsync(gmToken)).Select(a => a.Roll).Should().Equal(Enumerable.Range(1, dado));
    }

    [Theory]
    [InlineData(18)]
    [InlineData(7)]
    [InlineData(0)]
    public async Task SetDado_with_an_invalid_die_returns_400_and_keeps_the_current_one(int dado)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"ArcaDadoGmI{dado}", $"arcadadoi{dado}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/dado", gmToken, new ArcaDadoRequest(dado)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Dado inválido. Use D6, D8, D10, D12, D20 ou D100.");
        (await ListArcasAsync(gmToken)).Should().HaveCount(20);
    }

    [Fact]
    public async Task Shrinking_the_die_only_hides_the_Arcas_and_growing_it_again_brings_them_back_intact()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaDadoGm2", "arcadado2@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/15", gmToken, new UpdateArcaEntryRequest("Arca Alta", "Descrição alta.")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/15/evolucoes", gmToken, new ArcaEvolucaoRequest(3, "evolução alta")));

        await SetDadoAsync(gmToken, 12);
        var shrunk = await ListArcasAsync(gmToken);
        shrunk.Should().HaveCount(12);
        shrunk.Should().NotContain(a => a.Roll == 15);

        await SetDadoAsync(gmToken, 20);
        var grown = await ListArcasAsync(gmToken);
        var arca15 = grown.Single(a => a.Roll == 15);
        arca15.Nome.Should().Be("Arca Alta");
        arca15.Descricao.Should().Be("Descrição alta.");
        arca15.Evolucoes.Should().ContainSingle().Which.Descricao.Should().Be("evolução alta");
    }

    [Fact]
    public async Task UpdateArca_above_the_chosen_die_returns_400_naming_the_die()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaDadoGm3", "arcadado3@teste.com");
        await SetDadoAsync(gmToken, 12);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/15", gmToken, new UpdateArcaEntryRequest("X", "Y")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Roll deve estar entre 1 e 12.");
    }

    [Fact]
    public async Task Evolucao_routes_above_the_chosen_die_return_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaDadoGm4", "arcadado4@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/15/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();
        await SetDadoAsync(gmToken, 12);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/15/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "y"))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/15/evolucoes/{created!.Id}", gmToken, new ArcaEvolucaoRequest(2, "z"))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Each_GMs_die_is_independent()
    {
        var gmA = await RegisterGmAndGetTokenAsync("ArcaDadoGmA", "arcadadoa@teste.com");
        var gmB = await RegisterGmAndGetTokenAsync("ArcaDadoGmB", "arcadadob@teste.com");

        await SetDadoAsync(gmA, 6);

        (await ListArcasAsync(gmA)).Should().HaveCount(6);
        (await ListArcasAsync(gmB)).Should().HaveCount(20);
    }

    [Fact]
    public async Task Dado_endpoints_by_a_jogador_return_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaDadoGm5", "arcadado5@teste.com");
        var playerToken = await RegisterJogadorTokenAsync(gmToken, "ArcaDadoPlayer5", "arcadadoplayer5@teste.com");

        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas/dado", playerToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/dado", playerToken, new ArcaDadoRequest(6)))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_default_Arca_description_follows_the_die_but_an_override_is_left_alone()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaDadoGm6", "arcadado6@teste.com");
        await SetDadoAsync(gmToken, 6);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/racial-abilities/Laonir", gmToken, new UpdateRacialAbilityRequest("Racial (Arca)", "Role 1d18 na tabela de Arcas.")));

        var body = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/racial-abilities", gmToken)))
            .Content.ReadFromJsonAsync<List<RacialAbilityEntryResponse>>();

        var sinir = body!.Single(e => e.Variante == "Sinir");
        sinir.IsDefault.Should().BeTrue();
        sinir.Descricao.Should().Be("Role 1d6 na tabela de Arcas.");
        var laonir = body!.Single(e => e.Variante == "Laonir");
        laonir.IsDefault.Should().BeFalse();
        laonir.Descricao.Should().Be("Role 1d18 na tabela de Arcas.");
    }
}
