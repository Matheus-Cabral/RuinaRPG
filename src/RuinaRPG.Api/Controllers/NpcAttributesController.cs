using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/attributes")]
public class NpcAttributesController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, RuinaRPG.Api.Services.NpcSheetStats stats, RuinaRPG.Api.Services.CondicoesAtivasDaFicha condicoes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NpcAttributeResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        // Artefatos (Posses 5.b: estar na ficha é estar equipado), penalidades de equipamento ativas e Condições: NpcSheetStats.
        return (await stats.AtributosAsync(sheet, await condicoes.ParaNpcAsync(sheet)))
            .Select(a => new NpcAttributeResponse(a.Atributo, a.Gasto, a.Bonus, a.TemMaestria, a.Total))
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
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var gastoTotal = await db.NpcAttributes.Where(a => a.NpcSheetId == sheetId).SumAsync(a => a.Gasto);
        var tabela = await tabelaDeNiveis.ObterAsync();
        return new AttributePointBudgetResponse(gastoTotal, AttributePointBudgetCalculator.Compute(sheet.Nivel, tabela));
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
        var tabela = await tabelaDeNiveis.ObterAsync();
        if (LimitesDeNivel.Gasto(atributo.ToString(), attribute.Gasto, request.Gasto, tabela.Limite(ChavesDeNivel.MaxAtributo, sheet.Nivel), sheet.Nivel) is { } erroDeLimite)
            return BadRequest(erroDeLimite);

        attribute.Gasto = request.Gasto;
        attribute.Bonus = request.Bonus;
        attribute.TemMaestria = request.TemMaestria;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
