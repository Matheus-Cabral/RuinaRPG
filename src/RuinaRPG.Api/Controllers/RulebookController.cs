using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/rulebook")]
[Authorize]
public class RulebookController(IRulebookRenderer renderer) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<RulebookDocumentResponse>> Get() =>
        renderer.GetDocuments()
            .Select(d => new RulebookDocumentResponse(d.Slug, d.Titulo, d.Html))
            .ToList();
}
