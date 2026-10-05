using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/skills")]
public class CreatureSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, RuinaRPG.Api.Services.CreatureSheetStats stats) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CreatureSkillResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var porId = await pericias.PorIdAsync();
        var ativas = porId.Values.Where(p => !p.IsDeleted && p.DisponivelParaCriaturas).ToList();
        var linhas = await db.CreatureSkills.Where(s => s.CreatureSheetId == sheetId).ToDictionaryAsync(s => s.PericiaId);

        var artefatos = await stats.ModificadoresAsync(sheet);

        var attributeTotals = await db.CreatureAttributes
            .Where(a => a.CreatureSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        return ativas
            .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p =>
            {
                linhas.TryGetValue(p.Id, out var s);
                var gasto = s?.Gasto ?? 0;
                var atributo = s?.AtributoEscolhido ?? SugeridoParaCriatura(p);
                var modificador = SkillFormulas.Modificador(gasto, 0);
                var total = atributo is not null && attributeTotals.TryGetValue(atributo.Value, out var atributoTotal)
                    ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave))
                    : (int?)null;
                return new CreatureSkillResponse(p.Chave, gasto, modificador, atributo?.ToString(), total, p.Nome, p.Descricao);
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

        // R0005's "lista fixa mais curta" — só Perícias com DisponivelParaCriaturas aparecem na Criatura.
        if (!def.DisponivelParaCriaturas)
            return BadRequest("Perícia fora da lista permitida para Criaturas.");

        var skill = await db.CreatureSkills.SingleOrDefaultAsync(s => s.CreatureSheetId == sheetId && s.PericiaId == def.Id);
        if (skill is null)
        {
            skill = new CreatureSkill { Id = Guid.NewGuid(), CreatureSheetId = sheetId, PericiaId = def.Id };
            db.CreatureSkills.Add(skill);
        }
        skill.Gasto = request.Gasto;
        skill.AtributoEscolhido = Enum.TryParse<AtributoCriatura>(request.AtributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Atributo sugerido como Atributo de Criatura; nulo se a Criatura não tem esse Atributo.</summary>
    private static AtributoCriatura? SugeridoParaCriatura(PericiaDefinicao p) =>
        p.AtributoSugerido is { } a && Enum.TryParse<AtributoCriatura>(a.ToString(), out var c) ? c : null;

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
