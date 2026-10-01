using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
public class NpcSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NpcSkillResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var porId = await pericias.PorIdAsync();
        var ativas = porId.Values.Where(p => !p.IsDeleted).ToList();
        var linhas = await db.NpcSkills.Where(s => s.NpcSheetId == sheetId).ToDictionaryAsync(s => s.PericiaId);

        var artefatos = await GetArtifactBonusInputsAsync(sheetId);

        var attributeTotals = await db.NpcAttributes
            .Where(a => a.NpcSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        return ativas
            .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p =>
            {
                linhas.TryGetValue(p.Id, out var s);
                var gasto = s?.Gasto ?? 0;
                var atributo = s?.AtributoEscolhido ?? p.AtributoSugerido;
                var historicoBonus = HistoricoBonusCalculator.For(p.Id, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId);
                var modificador = SkillFormulas.Modificador(gasto, historicoBonus);
                var total = atributo is not null && attributeTotals.TryGetValue(atributo.Value, out var atributoTotal)
                    ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave))
                    : (int?)null;
                return new NpcSkillResponse(p.Chave, gasto, modificador, atributo?.ToString(), total, historicoBonus > 0, p.Nome, p.Descricao);
            })
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
        var gastoAtual = await db.NpcSkills.Where(s => s.NpcSheetId == sheetId && s.PericiaId == def.Id).Select(s => s.Gasto).SingleOrDefaultAsync();
        if (LimitesDeNivel.Gasto(def.Nome, gastoAtual, request.Gasto, tabela.Limite(ChavesDeNivel.MaxPericia, sheet.Nivel), sheet.Nivel) is { } erroDeLimite)
            return BadRequest(erroDeLimite);

        var skill = await db.NpcSkills.SingleOrDefaultAsync(s => s.NpcSheetId == sheetId && s.PericiaId == def.Id);
        if (skill is null)
        {
            skill = new NpcSkill { Id = Guid.NewGuid(), NpcSheetId = sheetId, PericiaId = def.Id };
            db.NpcSkills.Add(skill);
        }
        skill.Gasto = request.Gasto;
        skill.AtributoEscolhido = Enum.TryParse<Atributo>(request.AtributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
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
