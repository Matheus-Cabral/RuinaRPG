using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/skills")]
public class NpcSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, RuinaRPG.Api.Services.NpcSheetStats stats, RuinaRPG.Api.Services.CondicoesAtivasDaFicha condicoes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NpcSkillResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        return (await stats.PericiasAsync(sheet, await condicoes.ParaNpcAsync(sheet)))
            .Select(p => new NpcSkillResponse(p.Chave, p.Gasto, p.Modificador, p.Atributo, p.Total, p.BonusDeHistorico, p.Nome, p.Descricao))
            .ToList();
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, string pericia, UpdateNpcSkillRequest request)
    {
        // Chave desconhecida ou Perícia removida: resolvida antes de tudo, como a antiga falha de binding da rota.
        var def = await pericias.AtivaPorChaveAsync(pericia);
        if (def is null)
            return NotFound();

        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var tabela = await tabelaDeNiveis.ObterAsync();
        var gastoAtual = await db.NpcSkills.Where(s => s.SheetId == sheetId && s.PericiaId == def.Id).Select(s => s.Gasto).SingleOrDefaultAsync();
        if (LimitesDeNivel.Gasto(def.Nome, gastoAtual, request.Gasto, tabela.Limite(ChavesDeNivel.MaxPericia, sheet.Nivel), sheet.Nivel) is { } erroDeLimite)
            return BadRequest(erroDeLimite);

        var skill = await db.NpcSkills.SingleOrDefaultAsync(s => s.SheetId == sheetId && s.PericiaId == def.Id);
        if (skill is null)
        {
            skill = new NpcSkill { Id = Guid.NewGuid(), SheetId = sheetId, PericiaId = def.Id };
            db.NpcSkills.Add(skill);
        }
        skill.Gasto = request.Gasto;
        skill.AtributoEscolhido = Enum.TryParse<Atributo>(request.AtributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
