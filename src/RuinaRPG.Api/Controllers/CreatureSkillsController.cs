using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
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
public class CreatureSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, RuinaRPG.Api.Services.CreatureSheetStats stats, RuinaRPG.Api.Services.CondicoesAtivasDaFicha condicoes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CreatureSkillResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        return (await stats.PericiasAsync(sheet, await condicoes.ParaCriaturaAsync(sheet)))
            .Select(p => new CreatureSkillResponse(p.Chave, p.Gasto, p.Modificador, p.Atributo, p.Total, p.Nome, p.Descricao))
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

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
