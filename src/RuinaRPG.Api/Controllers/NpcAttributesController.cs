using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/attributes")]
public class NpcAttributesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NpcAttributeResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        // Display order is independent of the enum's underlying (persisted) int value —
        // see AttributeDisplayOrder's doc comment — so this sorts in memory, not in SQL.
        var attributes = (await db.NpcAttributes.Where(a => a.NpcSheetId == sheetId).ToListAsync())
            .OrderBy(a => AttributeDisplayOrder.Rank(a.Atributo))
            .ToList();

        var artefatos = await GetArtifactBonusInputsAsync(sheetId);

        return attributes
            .Select(a => new NpcAttributeResponse(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                    artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString()))))
            .ToList();
    }

    [HttpPut("{atributo}")]
    public async Task<IActionResult> Update(Guid sheetId, Atributo atributo, UpdateNpcAttributeRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        attribute.Gasto = request.Gasto;
        attribute.Bonus = request.Bonus;
        attribute.TemMaestria = request.TemMaestria;
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Every NpcArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses 5.b has
    /// no equip/unequip toggle for Artefatos, so simply being on the sheet counts as equipped.
    /// Mirrors CharacterSheetsController.GetArtifactBonusInputsAsync.
    /// </summary>
    private async Task<List<ArtifactBonusInput>> GetArtifactBonusInputsAsync(Guid sheetId) =>
        await db.NpcArtifacts
            .Where(a => a.NpcSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
