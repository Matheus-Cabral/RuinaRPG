using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/skills")]
public class CharacterSkillsController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CharacterSkillResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var skills = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheetId).OrderBy(s => s.Pericia).ToListAsync();

        var attributeTotals = await db.CharacterAttributes
            .Where(a => a.CharacterSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria, artefatos: 0));

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto);
                var total = s.AtributoEscolhido is not null && attributeTotals.TryGetValue(s.AtributoEscolhido.Value, out var atributoTotal)
                    ? SkillFormulas.Total(modificador, atributoTotal)
                    : (int?)null;
                return new CharacterSkillResponse(s.Pericia.ToString(), s.Gasto, modificador, s.AtributoEscolhido?.ToString(), total);
            })
            .ToList();
    }

    [HttpGet("budget")]
    public async Task<ActionResult<SkillPointBudgetResponse>> Budget(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var gastoBruto = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheetId).SumAsync(s => s.Gasto);
        // Pontos ganhos por Acerto Crítico não vêm do orçamento por Nível — subtraídos do Gasto
        // Total para não acusar "acima do máximo" por um ganho legítimo (2.d).
        var gastoTotal = gastoBruto - sheet.PontosDePericiaBonusCritico;
        var pontosDisponiveis = SkillPointBudgetCalculator.Compute(sheet.Nivel, rules.Niveis);
        return new SkillPointBudgetResponse(gastoTotal, pontosDisponiveis);
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, Pericia pericia, UpdateCharacterSkillRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var skill = await db.CharacterSkills.SingleAsync(s => s.CharacterSheetId == sheetId && s.Pericia == pericia);
        skill.Gasto = request.Gasto;
        skill.AtributoEscolhido = Enum.TryParse<Atributo>(request.AtributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
