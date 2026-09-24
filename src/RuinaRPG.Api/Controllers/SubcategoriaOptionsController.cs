using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using static RuinaRPG.Api.Controllers.EnumParsing;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Global (not per-GM) vocabulary of Categoria/Família values used by the Catálogo item form's
/// "Item Inicial" constructor and by Equipagem choice-slot authoring — same treatment as
/// HistoricosController/EquipmentKitsController. List (GET) stays open; Create/Delete are gated
/// to the Rules Auditor, checked directly against the DB, not a JWT claim. No PUT — renaming a
/// value is delete + re-add.
/// </summary>
[ApiController]
[Route("api/subcategoria-options")]
[Authorize]
public class SubcategoriaOptionsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SubcategoriaOptionResponse>>> List([FromQuery] string? tipo, [FromQuery] string? facet)
    {
        var query = db.SubcategoriaOptions.Where(o => !o.IsDeleted);

        // An unparseable filter (e.g. "99") is silently ignored — the query stays unfiltered on
        // that clause — rather than coerced into a defined-looking-but-wrong enum value that would
        // filter the result set down to nothing. TryParseExact is what makes "99" register as
        // unparseable in the first place; plain Enum.TryParse would happily accept it.
        if (tipo is not null && TryParseExact<ItemTipo>(tipo, out var tipoParsed))
            query = query.Where(o => o.Tipo == tipoParsed);
        if (facet is not null && TryParseExact<SubcategoriaFacet>(facet, out var facetParsed))
            query = query.Where(o => o.Facet == facetParsed);

        var options = await query.OrderBy(o => o.Valor).ToListAsync();
        return options.Select(ToResponse).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<SubcategoriaOptionResponse>> Create(CreateSubcategoriaOptionRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!TryParseExact<ItemTipo>(request.Tipo, out var tipo) || tipo == ItemTipo.ItemGeral)
            return BadRequest($"Tipo inválido para Construtor de Subcategoria: \"{request.Tipo}\".");
        if (!TryParseExact<SubcategoriaFacet>(request.Facet, out var facet))
            return BadRequest($"Facet inválido: \"{request.Facet}\".");

        var valor = request.Valor?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(valor))
            return BadRequest("Valor é obrigatório.");
        if (valor.StartsWith('-') || valor.EndsWith('-'))
            return BadRequest("Valor não pode começar ou terminar com \"-\".");
        if (valor.Contains(','))
            return BadRequest("Valor não pode conter \",\" — usado como separador na lista de Subcategorias de um slot de escolha.");
        if (valor.Contains(SubcategoriaBuilder.Separator))
            return BadRequest($"Valor não pode conter \"{SubcategoriaBuilder.Separator}\".");

        var option = new SubcategoriaOption { Id = Guid.NewGuid(), Tipo = tipo, Facet = facet, Valor = valor };
        db.SubcategoriaOptions.Add(option);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(option));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var option = await db.SubcategoriaOptions.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
        if (option is null)
            return NotFound();

        option.IsDeleted = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static SubcategoriaOptionResponse ToResponse(SubcategoriaOption o) =>
        new(o.Id.ToString(), o.Tipo.ToString(), o.Facet.ToString(), o.Valor);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
