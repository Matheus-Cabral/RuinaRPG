using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/skills")]
public class CreatureSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CreatureSkillResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var skills = await db.CreatureSkills.Where(s => s.CreatureSheetId == sheetId).OrderBy(s => s.PericiaId).ToListAsync();
        var porId = await pericias.PorIdAsync();

        var artefatos = await GetArtifactBonusInputsAsync(sheetId);

        var attributeTotals = await db.CreatureAttributes
            .Where(a => a.CreatureSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto, 0);
                var total = s.AtributoEscolhido is not null && attributeTotals.TryGetValue(s.AtributoEscolhido.Value, out var atributoTotal)
                    ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, porId[s.PericiaId].Chave))
                    : (int?)null;
                return new CreatureSkillResponse(porId[s.PericiaId].Chave, s.Gasto, modificador, s.AtributoEscolhido?.ToString(), total);
            })
            .ToList();
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, string pericia, UpdateCreatureSkillRequest request)
    {
        // Chave desconhecida ou Perícia removida: resolvida antes de tudo, como a antiga falha de binding da rota.
        var def = await pericias.AtivaPorChaveAsync(pericia);
        if (def is null)
            return NotFound();

        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        // R0005's "lista fixa mais curta" — only Perícias flagged DisponivelParaCriaturas have a seeded
        // CreatureSkill row. Checked before the SingleAsync below so a disallowed-but-real Perícia
        // (e.g. Alquimia) 400s instead of 500ing on a row that was never seeded.
        if (!def.DisponivelParaCriaturas)
            return BadRequest("Perícia fora da lista permitida para Criaturas.");

        var skill = await db.CreatureSkills.SingleAsync(s => s.CreatureSheetId == sheetId && s.PericiaId == def.Id);
        skill.Gasto = request.Gasto;
        skill.AtributoEscolhido = Enum.TryParse<AtributoCriatura>(request.AtributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
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
