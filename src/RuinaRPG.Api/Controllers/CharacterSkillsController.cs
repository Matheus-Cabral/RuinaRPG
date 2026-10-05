using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/skills")]
public class CharacterSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, RuinaRPG.Api.Services.CharacterSheetStats stats) : ControllerBase
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

        var porId = await pericias.PorIdAsync();
        var ativas = porId.Values.Where(p => !p.IsDeleted).ToList();
        var linhas = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheetId).ToDictionaryAsync(s => s.PericiaId);

        // Artefatos (Posses 5.b: estar na ficha é estar equipado) + penalidades de equipamento ativas.
        var artefatos = await stats.ModificadoresAsync(sheet);

        var attributeTotals = await db.CharacterAttributes
            .Where(a => a.CharacterSheetId == sheetId)
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
                return new CharacterSkillResponse(p.Chave, gasto, modificador, atributo?.ToString(), total, historicoBonus > 0, p.Nome, p.Descricao);
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

        var gastoBruto = await db.CharacterSkills
            .Where(s => s.CharacterSheetId == sheetId && !db.Pericias.Any(p => p.Id == s.PericiaId && p.IsDeleted))
            .SumAsync(s => s.Gasto);
        // Pontos ganhos por Acerto Crítico não vêm do orçamento por Nível — subtraídos do Gasto
        // Total para não acusar "acima do máximo" por um ganho legítimo (2.d).
        var gastoTotal = gastoBruto - sheet.PontosDePericiaBonusCritico;
        var tabela = await tabelaDeNiveis.ObterAsync();
        var pontosDisponiveis = SkillPointBudgetCalculator.Compute(sheet.Nivel, tabela);
        return new SkillPointBudgetResponse(gastoTotal, pontosDisponiveis);
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, string pericia, UpdateCharacterSkillRequest request)
    {
        // Chave desconhecida ou Perícia removida: resolvida antes de tudo, como a antiga falha de binding da rota.
        var def = await pericias.AtivaPorChaveAsync(pericia);
        if (def is null)
            return NotFound();

        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var tabela = await tabelaDeNiveis.ObterAsync();
        var gastoAtual = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheetId && s.PericiaId == def.Id).Select(s => s.Gasto).SingleOrDefaultAsync();
        if (LimitesDeNivel.Gasto(def.Nome, gastoAtual, request.Gasto, tabela.Limite(ChavesDeNivel.MaxPericia, sheet.Nivel), sheet.Nivel) is { } erroDeLimite)
            return BadRequest(erroDeLimite);

        var skill = await db.CharacterSkills.SingleOrDefaultAsync(s => s.CharacterSheetId == sheetId && s.PericiaId == def.Id);
        if (skill is null)
        {
            skill = new CharacterSkill { Id = Guid.NewGuid(), CharacterSheetId = sheetId, PericiaId = def.Id };
            db.CharacterSkills.Add(skill);
        }
        skill.Gasto = request.Gasto;
        skill.AtributoEscolhido = Enum.TryParse<Atributo>(request.AtributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
