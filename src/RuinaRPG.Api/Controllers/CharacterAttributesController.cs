using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/attributes")]
public class CharacterAttributesController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CharacterAttributeResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        // Display order is independent of the enum's underlying (persisted) int value —
        // see AttributeDisplayOrder's doc comment — so this sorts in memory, not in SQL.
        var attributes = (await db.CharacterAttributes.Where(a => a.CharacterSheetId == sheetId).ToListAsync())
            .OrderBy(a => AttributeDisplayOrder.Rank(a.Atributo))
            .ToList();

        // Posses 5.b has no equip/unequip toggle for Artefatos — being on the sheet counts as equipped.
        var artefatos = await db.CharacterArtifacts
            .Where(a => a.CharacterSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

        return attributes
            .Select(a => new CharacterAttributeResponse(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                    artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString()))))
            .ToList();
    }

    [HttpGet("budget")]
    public async Task<ActionResult<AttributePointBudgetResponse>> Budget(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var gastoTotal = await db.CharacterAttributes.Where(a => a.CharacterSheetId == sheetId).SumAsync(a => a.Gasto);
        var pontosDisponiveis = AttributePointBudgetCalculator.Compute(sheet.Nivel, rules.Niveis);
        return new AttributePointBudgetResponse(gastoTotal, pontosDisponiveis);
    }

    [HttpPut("{atributo}")]
    public async Task<IActionResult> Update(Guid sheetId, Atributo atributo, UpdateCharacterAttributeRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var attribute = await db.CharacterAttributes.SingleAsync(a => a.CharacterSheetId == sheetId && a.Atributo == atributo);

        // "A soma de Gasto de todos os 8 atributos ... não pode ultrapassar o total de pontos que
        // o personagem já recebeu (criação + níveis)" (2.a). Rejected rather than clamped —
        // unlike Atual/Máximo, this budget only ever grows (levels/creation), so there's no
        // legitimate scenario where a previously-valid Gasto becomes invalid out from under the
        // player; the only way to exceed it is trying to spend more than they have.
        var gastoDosOutros = await db.CharacterAttributes.Where(a => a.CharacterSheetId == sheetId && a.Atributo != atributo).SumAsync(a => a.Gasto);
        var pontosDisponiveis = AttributePointBudgetCalculator.Compute(sheet.Nivel, rules.Niveis);
        if (gastoDosOutros + request.Gasto > pontosDisponiveis)
            return BadRequest($"Gasto excede os {pontosDisponiveis} pontos de Atributo disponíveis.");

        attribute.Gasto = request.Gasto;
        attribute.Bonus = request.Bonus;
        attribute.TemMaestria = request.TemMaestria;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
