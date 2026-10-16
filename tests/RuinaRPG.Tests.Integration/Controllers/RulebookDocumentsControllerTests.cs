using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RulebookDocumentsControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{

    [Fact]
    public async Task List_returns_the_3_documents_all_default_when_no_override_exists()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm1", "rulebookdocgm1@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm1@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        body!.Select(d => d.Slug).Should().BeEquivalentTo("sistema-basico", "graus-e-circulos", "estrelas-alkerianas");
        body!.Select(d => d.Slug).Should().NotContain("tabela-de-niveis");
        body!.Should().OnlyContain(d => d.IsDefault);
        body!.Should().OnlyContain(d => !string.IsNullOrEmpty(d.MarkdownText)); // the embedded default text, non-empty
    }

    [Fact]
    public async Task List_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm2", "rulebookdocgm2@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/sistema-basico", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Novo texto")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_by_a_GM_who_is_not_a_Rules_Auditor_returns_403_too()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm6", "rulebookdocgm6@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateDocument_persists_the_override_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm3", "rulebookdocgm3@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm3@teste.com");

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/sistema-basico", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Sistema Básico Editado\n\nTexto novo do Auditor.")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        try
        {
            var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));
            var body = await listResponse.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
            var edited = body!.Single(d => d.Slug == "sistema-basico");
            edited.IsDefault.Should().BeFalse();
            edited.MarkdownText.Should().Contain("Texto novo do Auditor.");
        }
        finally
        {
            // This class shares one Postgres instance across every test in it (IClassFixture, no
            // per-test reset) — clean up so this override can't leak into another test that expects
            // the embedded default for "sistema-basico" (e.g. List_returns_the_3_documents...).
            await Client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/sistema-basico", gmToken));
        }
    }

    [Fact]
    public async Task UpdateDocument_with_an_unknown_slug_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm4", "rulebookdocgm4@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm4@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/caracteristicas", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Não permitido")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteDocument_reverts_to_the_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm5", "rulebookdocgm5@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm5@teste.com");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/sistema-basico", gmToken,
            new UpdateRulebookDocumentOverrideRequest("| Custom |")));

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/sistema-basico", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        body!.Single(d => d.Slug == "sistema-basico").IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Tabela_de_niveis_is_no_longer_an_editable_document()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm9", "rulebookdocgm9@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm9@teste.com");

        var put = await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/tabela-de-niveis", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Não")));
        var delete = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/tabela-de-niveis", gmToken));

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
