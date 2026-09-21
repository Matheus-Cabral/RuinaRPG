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

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/attributes")]
public class CreatureAttributesController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
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
        var artefatos = await GetArtifactBonusInputsAsync(sheetId);
        return attributes
            .Select(a => new CreatureAttributeResponse(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                    artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString()))))
            .ToList();
    }

    /// <summary>
    /// Pontos de Atributo que o Nível da ficha já deu (mesma Tabela de Níveis do Personagem) contra
    /// o Gasto somado. Só informa — ao contrário do Personagem, o GM pode passar do total, então
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
        return new AttributePointBudgetResponse(gastoTotal, AttributePointBudgetCalculator.Compute(sheet.Nivel, rules.Niveis));
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

    /// <summary>
    /// Every CreatureArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses
    /// 5.b has no equip/unequip toggle for Artefatos, so simply being on the sheet counts as
    /// equipped. Mirrors CharacterSheetsController.GetArtifactBonusInputsAsync.
    /// </summary>
    private async Task<List<ArtifactBonusInput>> GetArtifactBonusInputsAsync(Guid sheetId) =>
        await db.CreatureArtifacts
            .Where(a => a.CreatureSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
