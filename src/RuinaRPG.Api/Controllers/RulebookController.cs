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
        (await renderer.GetDocuments())
            .Select(d => new RulebookDocumentResponse(d.Slug, d.Titulo, d.IntroHtml,
                d.Sections.Select(s => new RulebookSectionResponse(s.Id, s.Titulo, s.Html, s.Grupo)).ToList()))
            .ToList();
}
