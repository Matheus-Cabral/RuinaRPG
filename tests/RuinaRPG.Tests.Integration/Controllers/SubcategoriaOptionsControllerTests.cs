using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SubcategoriaOptionsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SubcategoriaOptionsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Copied verbatim from HistoricosControllerTests.cs's established pattern — no grant endpoint
    // exists, real grants happen via `make grant-rules-auditor`.
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_is_open_to_any_authenticated_caller_and_filters_by_Tipo_and_Facet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm1", "subcat1@teste.com");
        await GrantRulesAuditorAsync("subcat1@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Varinha")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Categoria", "Mágica")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Armadura", "Familia", "Couro")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/subcategoria-options?tipo=Arma&facet=Familia", gmToken));

        var options = await response.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>();
        options!.Should().ContainSingle(o => o.Valor == "Varinha");
        options.Should().NotContain(o => o.Valor == "Mágica" || o.Valor == "Couro");
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_GM_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm2", "subcat2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Arcos")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_rejects_Tipo_ItemGeral_and_an_unknown_Facet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm3", "subcat3@teste.com");
        await GrantRulesAuditorAsync("subcat3@teste.com");

        var badTipo = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("ItemGeral", "Familia", "X")));
        badTipo.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badFacet = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "NaoExiste", "X")));
        badFacet.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // The composed Subcategoria string has no escaping (SubcategoriaBuilder joins 4 segments with
    // " - "), so a Valor containing the separator would corrupt the format — reject it up front.
    [Fact]
    public async Task Create_rejects_a_Valor_containing_the_SubcategoriaBuilder_Separator()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm6", "subcat6@teste.com");
        await GrantRulesAuditorAsync("subcat6@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Adaga - Curva")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // A comma isn't the composed-string separator, but SubcategoriasCsv (EquipmentKitChoiceSlot)
    // stores allowed Família values joined with ',' and both EquipmentKitsController.ToResponseAsync
    // and EquipmentKitGrantService.ResolveEligibleOptionsAsync split on ',' to read it back — a
    // Família containing a comma would silently break that round trip (e.g. "Espadas, Adagas"
    // becomes two separate values on split), so it must be rejected up front just like the separator.
    [Fact]
    public async Task Create_rejects_a_Valor_containing_a_comma()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm7", "subcat7@teste.com");
        await GrantRulesAuditorAsync("subcat7@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Espadas, Adagas")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Enum.TryParse alone is lenient about numeric strings ("99"), silently mapping them to an
    // underlying int value that may not even be a defined enum member — Create must reject this
    // the same way EquipmentKitsController.TryParseExact does for choice-slot Tipo/ArmorSlot.
    [Fact]
    public async Task Create_rejects_a_Tipo_that_is_a_numeric_string()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm8", "subcat8@teste.com");
        await GrantRulesAuditorAsync("subcat8@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("99", "Familia", "X")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Same strict parse must apply to Facet.
    [Fact]
    public async Task Create_rejects_a_Facet_that_is_a_numeric_string()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm9", "subcat9@teste.com");
        await GrantRulesAuditorAsync("subcat9@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "5", "X")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // List keeps ignoring unparseable filters (brief-specified: fall back to unfiltered rather than
    // 400ing), but must use the same strict exact-name parse as Create so "99" is treated as
    // unparseable — not silently coerced into a defined-looking-but-wrong ItemTipo that then
    // filters the query down to zero rows instead of leaving it unfiltered.
    [Fact]
    public async Task List_ignores_a_numeric_Tipo_filter_instead_of_filtering_on_an_undefined_value()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm10", "subcat10@teste.com");
        await GrantRulesAuditorAsync("subcat10@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "ValorParaFiltroNumerico")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/subcategoria-options?tipo=99&facet=Familia", gmToken));

        var options = await response.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>();
        options!.Should().Contain(o => o.Valor == "ValorParaFiltroNumerico");
    }

    // Valor edge cases that pass the plain " - " substring check but still break the composed
    // 4-segment round trip: a trailing "-" with no text after it ("Leve -"), a leading "-" with no
    // text before it ("- Arcos"), and surrounding whitespace that would otherwise get baked into
    // the composed string verbatim.
    [Theory]
    [InlineData("Leve -", "SubcatGm11a", "subcat11a@teste.com")]
    [InlineData("- Arcos", "SubcatGm11b", "subcat11b@teste.com")]
    [InlineData("-", "SubcatGm11c", "subcat11c@teste.com")]
    public async Task Create_rejects_a_Valor_with_a_leading_or_trailing_hyphen(string valor, string nickname, string email)
    {
        var gmToken = await RegisterGmAndGetTokenAsync(nickname, email);
        await GrantRulesAuditorAsync(email);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", valor)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_trims_a_Valor_with_surrounding_whitespace()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm12", "subcat12@teste.com");
        await GrantRulesAuditorAsync("subcat12@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "  Arcos  ")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<SubcategoriaOptionResponse>();
        created!.Valor.Should().Be("Arcos");
    }

    [Fact]
    public async Task Create_rejects_a_Valor_that_is_only_whitespace()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm13", "subcat13@teste.com");
        await GrantRulesAuditorAsync("subcat13@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "   ")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_by_a_non_Auditor_GM_returns_403_and_by_the_Auditor_soft_deletes()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm4", "subcat4@teste.com");
        await GrantRulesAuditorAsync("subcat4@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Escudo", "Familia", "Rodela"))))
            .Content.ReadFromJsonAsync<SubcategoriaOptionResponse>();

        var otherGmToken = await RegisterGmAndGetTokenAsync("SubcatGm5", "subcat5@teste.com");
        var forbidden = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/subcategoria-options/{created!.Id}", otherGmToken));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleted = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/subcategoria-options/{created.Id}", gmToken));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/subcategoria-options?tipo=Escudo&facet=Familia", gmToken));
        var options = await listResponse.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>();
        options!.Should().NotContain(o => o.Id == created.Id);
    }
}
