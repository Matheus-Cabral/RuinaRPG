using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/rulebook")]
[AllowAnonymous]
public class RulebookController(IRulebookRenderer renderer) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RulebookDocumentResponse>>> Get() =>
        (await renderer.GetDocuments()).Select(ToResponse).ToList();

    /// <summary>
    /// One document on its own — e.g. the Estrela popup on the sheets (Ficha de Personagem R0001
    /// 1.a) only needs "estrelas-alkerianas", not all 7 rendered documents.
    /// </summary>
    [HttpGet("{slug}")]
    public async Task<ActionResult<RulebookDocumentResponse>> GetDocument(string slug) =>
        await renderer.GetDocument(slug) is { } document ? ToResponse(document) : NotFound();

    private static RulebookDocumentResponse ToResponse(RulebookDocument d) =>
        new(d.Slug, d.Titulo, d.IntroHtml,
            d.Sections.Select(s => new RulebookSectionResponse(s.Id, s.Titulo, s.Html, s.Grupo)).ToList());
}
