using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Historico.md's static, GM-independent catalog (seeded once, shared by every GM/Jogador) — no
/// owning GmId, same treatment as TraitsController. List (GET) stays open to any authenticated
/// caller. Create/Update/Delete are gated to the Rules Auditor (see Requisitos - Auditoria de
/// Regras), checked directly against the DB (ApplicationUser.IsRulesAuditor), not a JWT claim.
/// </summary>
[ApiController]
[Route("api/historicos")]
[Authorize]
public class HistoricosController(RuinaRpgDbContext db, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HistoricoResponse>>> List()
    {
        var historicos = await db.Historicos.Where(h => !h.IsDeleted).OrderBy(h => h.Nome).ToListAsync();
        var porId = await pericias.PorIdAsync();
        return historicos.Select(h => ToResponse(h, porId)).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<HistoricoResponse>> Create(CreateHistoricoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Descricao))
            return BadRequest("Descrição é obrigatória.");

        var periciaMaisSeis = await pericias.AtivaPorChaveAsync(request.PericiaMaisSeis);
        if (periciaMaisSeis is null)
            return BadRequest("PericiaMaisSeis inválida.");
        var periciaMaisTres = await pericias.AtivaPorChaveAsync(request.PericiaMaisTres);
        if (periciaMaisTres is null)
            return BadRequest("PericiaMaisTres inválida.");
        if (periciaMaisSeis.Id == periciaMaisTres.Id)
            return BadRequest("PericiaMaisSeis e PericiaMaisTres não podem ser a mesma Perícia.");

        var historico = new Historico
        {
            Id = Guid.NewGuid(),
            Nome = request.Nome,
            Descricao = request.Descricao,
            PericiaMaisSeisId = periciaMaisSeis.Id,
            PericiaMaisTresId = periciaMaisTres.Id,
            IsCustomized = true,
            UpdatedByUserId = CurrentUserId(),
            UpdatedAt = DateTime.UtcNow,
        };
        db.Historicos.Add(historico);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(historico, await pericias.PorIdAsync()));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateHistoricoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var historico = await db.Historicos.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        if (historico is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Descricao))
            return BadRequest("Descrição é obrigatória.");

        // Uma Chave igual à perícia já gravada na linha é aceita mesmo removida (senão o Histórico
        // ficaria impossível de editar); só um valor novo exige perícia ativa.
        var periciaMaisSeis = await ResolveForUpdateAsync(request.PericiaMaisSeis, historico.PericiaMaisSeisId);
        if (periciaMaisSeis is null)
            return BadRequest("PericiaMaisSeis inválida.");
        var periciaMaisTres = await ResolveForUpdateAsync(request.PericiaMaisTres, historico.PericiaMaisTresId);
        if (periciaMaisTres is null)
            return BadRequest("PericiaMaisTres inválida.");
        if (periciaMaisSeis.Id == periciaMaisTres.Id)
            return BadRequest("PericiaMaisSeis e PericiaMaisTres não podem ser a mesma Perícia.");

        historico.Nome = request.Nome;
        historico.Descricao = request.Descricao;
        historico.PericiaMaisSeisId = periciaMaisSeis.Id;
        historico.PericiaMaisTresId = periciaMaisTres.Id;
        historico.IsCustomized = true;
        historico.UpdatedByUserId = CurrentUserId();
        historico.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var historico = await db.Historicos.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        if (historico is null)
            return NotFound();

        var inUse = await db.CharacterSheets.AnyAsync(s => s.HistoricoId == id)
            || await db.NpcSheets.AnyAsync(s => s.HistoricoId == id);
        if (inUse)
            return Conflict("Este Histórico está em uso em pelo menos uma ficha e não pode ser excluído.");

        historico.IsDeleted = true;
        historico.UpdatedByUserId = CurrentUserId();
        historico.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<PericiaDefinicao?> ResolveForUpdateAsync(string chave, int idAtual)
    {
        var todas = await pericias.TodasAsync();
        var atual = todas.FirstOrDefault(p => p.Id == idAtual);
        if (atual is not null && atual.Chave == chave)
            return atual;
        return await pericias.AtivaPorChaveAsync(chave);
    }

    private static HistoricoResponse ToResponse(Historico h, IReadOnlyDictionary<int, PericiaDefinicao> porId) =>
        new(h.Id.ToString(), h.Nome, h.Descricao, porId[h.PericiaMaisSeisId].Chave, porId[h.PericiaMaisTresId].Chave, h.IsCustomized);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
