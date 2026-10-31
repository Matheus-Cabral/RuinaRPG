using System.Net;
using System.Net.Http.Json;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// O catálogo de elementos de Afinidade como a API o semeia (ids = os inteiros do antigo enum; Mundano = 18), para os
/// handlers falsos responderem GET afinidades/catalogo.
/// </summary>
public static class ElementosDeTeste
{
    public static readonly IReadOnlyList<ElementoDeAfinidadeResponse> Semeados =
    [
        new(0, "Terra", "Base", null), new(1, "Água", "Base", null), new(2, "Fogo", "Base", null), new(3, "Ar", "Base", null),
        new(4, "Gelo", "SubElemento", null), new(5, "Flora", "SubElemento", null), new(6, "Ferro", "SubElemento", null),
        new(7, "Raio", "SubElemento", null), new(8, "Prever", "SubElemento", null), new(9, "Alma", "Caminho", null),
        new(10, "Purificar", "SubElemento", null), new(11, "Ecomancia", "SubElemento", null), new(12, "Hemomancia", "SubElemento", null),
        new(13, "Curar", "SubElemento", null), new(14, "Vida", "Caminho", null), new(15, "Aprimorar", "SubElemento", null),
        new(16, "Necromancia", "SubElemento", null), new(17, "Invocação", "SubElemento", null), new(18, "Mundano", "Caminho", null),
    ];

    public static bool EhCatalogo(HttpRequestMessage request) =>
        request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/afinidades/catalogo", StringComparison.Ordinal);

    public static HttpResponseMessage Resposta(IEnumerable<ElementoDeAfinidadeResponse>? elementos = null) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create((elementos ?? Semeados).ToList()) };

    /// <summary>Um HttpClient que só conhece o catálogo de elementos (o resto é 404).</summary>
    public static HttpClient Cliente(IEnumerable<ElementoDeAfinidadeResponse>? elementos = null) =>
        FakeHttpMessageHandler.CreateClient(r => EhCatalogo(r) ? Resposta(elementos) : new HttpResponseMessage(HttpStatusCode.NotFound));
}
