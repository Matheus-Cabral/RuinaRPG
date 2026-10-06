using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/attributes")]
public class CreatureAttributesController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, RuinaRPG.Api.Services.CreatureSheetStats stats) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CreatureAttributeResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var attributes = await db.CreatureAttributes.Where(a => a.CreatureSheetId == sheetId).OrderBy(a => a.Atributo).ToListAsync();
        var artefatos = await stats.ModificadoresAsync(sheet);
        return attributes
            .Select(a => new CreatureAttributeResponse(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                    artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString()))))
            .ToList();
    }

    /// <summary>
    /// Pontos de Atributo que o Rank + o Nível da ficha já deram (Rank no lugar do Nível 1 da
    /// Tabela de Níveis — ver Requisitos - Ficha de Criaturas R0005 2.a) contra o Gasto somado. Só informa — ao contrário do Personagem, o GM pode passar do total, então
    /// o Update nunca rejeita (ver Requisitos - Ficha de NPCs R0007).
    /// </summary>
    [HttpGet("budget")]
    public async Task<ActionResult<AttributePointBudgetResponse>> Budget(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var gastoTotal = await db.CreatureAttributes.Where(a => a.CreatureSheetId == sheetId).SumAsync(a => a.Gasto);
        var tabela = await tabelaDeNiveis.ObterAsync();
        return new AttributePointBudgetResponse(gastoTotal, CreatureAttributePointBudgetCalculator.Compute(sheet.Rank, sheet.Nivel, tabela));
    }

    [HttpPut("{atributo}")]
    public async Task<IActionResult> Update(Guid sheetId, AtributoCriatura atributo, UpdateCreatureAttributeRequest request)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var attribute = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetId && a.Atributo == atributo);
        attribute.Gasto = request.Gasto;
        attribute.Bonus = request.Bonus;
        attribute.TemMaestria = request.TemMaestria;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
