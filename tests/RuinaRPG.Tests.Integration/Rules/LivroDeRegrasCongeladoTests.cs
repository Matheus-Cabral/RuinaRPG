using System.Net.Http.Json;
using System.Text.Json;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

// Rede de segurança do G: o que a API renderiza a partir de Docs/Sistema RPG/ (documentos do Livro de
// Regras e o Compêndio) tem de sair byte a byte igual ao de antes do enxugamento da documentação.
// Fora do congelamento: "equipagem" (já congelado por RulebookEquipagemCongeladoTests; depende de kits).
public class LivroDeRegrasCongeladoTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static readonly string[] Slugs =
        ["sistema-basico", "caracteristicas", "graus-e-circulos", "tabela-de-niveis", "estrelas-alkerianas", "historicos"];

    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    [Fact]
    public async Task Os_documentos_do_livro_de_regras_continuam_identicos()
    {
        var esperado = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Rules/livro-de-regras.json"));
        Assert.Equal(esperado, await ObterLivroAsync());
    }

    [Fact]
    public async Task O_compendio_continua_identico()
    {
        var esperado = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Rules/compendio.json"));
        Assert.Equal(esperado, await ObterCompendioAsync());
    }

    private async Task<string> ObterLivroAsync()
    {
        var documentos = new List<RulebookDocumentResponse?>();
        foreach (var slug in Slugs)
            documentos.Add(await Client.GetFromJsonAsync<RulebookDocumentResponse>($"/api/rulebook/{slug}"));
        return JsonSerializer.Serialize(documentos, Opcoes);
    }

    private async Task<string> ObterCompendioAsync()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioCongelado", "compendio.congelado@example.com");
        var resposta = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search", token));
        resposta.EnsureSuccessStatusCode();
        var resultados = await resposta.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        // A consulta das Características não tem ORDER BY (ordem física varia entre execuções): ordena aqui.
        var ordenados = resultados!
            .OrderBy(r => r.Categoria, StringComparer.Ordinal).ThenBy(r => r.Origem, StringComparer.Ordinal)
            .ThenBy(r => r.Titulo, StringComparer.Ordinal).ThenBy(r => r.Conteudo, StringComparer.Ordinal);
        return JsonSerializer.Serialize(ordenados, Opcoes);
    }
}
