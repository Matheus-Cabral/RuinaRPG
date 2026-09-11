using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// GM-editable overrides of the Livro de Regras' raw Markdown, for the 3 documents that aren't
/// DB-driven (Sistema Básico, Graus & Círculos, Tabela de Níveis — "caracteristicas" is excluded
/// on purpose, it's rebuilt from the live Traits table by RulebookRenderer instead, see
/// TraitsController). Display-only: nothing here feeds IRulesDataProvider or any gameplay
/// calculator — see Requisitos - Auditoria de Regras. Unlike TraitsController.List, List (GET)
/// here is gated to the Rules Auditor too — this endpoint's only consumer in the whole app is
/// the AuditoriaLivroDeRegras.razor page itself, so it doesn't need the same open-to-everyone
/// precedent as GET /api/traits. Same inline DB check as Update/Delete (not a JWT claim, so a
/// grant/revoke via `make grant-rules-auditor` takes effect on the very next request).
/// </summary>
[ApiController]
[Authorize]
public class RulebookDocumentsController(RuinaRpgDbContext db) : ControllerBase
{
    private static readonly string[] ValidSlugs = ["sistema-basico", "graus-e-circulos", "tabela-de-niveis"];

    [HttpGet("api/rulebook-documents")]
    public async Task<ActionResult<List<RulebookDocumentOverrideResponse>>> List()
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var overrides = await db.RulebookDocumentOverrides.ToListAsync();

        return ValidSlugs.Select(slug =>
        {
            var over = overrides.FirstOrDefault(o => o.Slug == slug);
            return over is not null
                ? new RulebookDocumentOverrideResponse(slug, over.MarkdownText, false)
                : new RulebookDocumentOverrideResponse(slug, RulebookRenderer.ReadEmbeddedMarkdown(slug), true);
        }).ToList();
    }

    [HttpPut("api/rulebook-documents/{slug}")]
    public async Task<IActionResult> Update(string slug, UpdateRulebookDocumentOverrideRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!ValidSlugs.Contains(slug))
            return BadRequest("Slug de documento desconhecido.");

        var existing = await db.RulebookDocumentOverrides.FirstOrDefaultAsync(o => o.Slug == slug);
        if (existing is null)
        {
            db.RulebookDocumentOverrides.Add(new RulebookDocumentOverride
            {
                Id = Guid.NewGuid(), Slug = slug, MarkdownText = request.MarkdownText,
                UpdatedByUserId = CurrentUserId(), UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.MarkdownText = request.MarkdownText;
            existing.UpdatedByUserId = CurrentUserId();
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/rulebook-documents/{slug}")]
    public async Task<IActionResult> Delete(string slug)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!ValidSlugs.Contains(slug))
            return BadRequest("Slug de documento desconhecido.");

        var existing = await db.RulebookDocumentOverrides.FirstOrDefaultAsync(o => o.Slug == slug);
        if (existing is not null)
        {
            db.RulebookDocumentOverrides.Remove(existing);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
