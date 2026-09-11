using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Características.md's static, GM-independent catalog (seeded once, shared by every GM/Jogador)
/// — unlike Item/SpellAbilityBankEntry, Traits have no owning GmId at all. List (GET) stays open
/// to any authenticated caller — the Compêndio and every sheet's "add characteristic" picker use
/// it. Create/Update/Delete are gated to the Rules Auditor (see Requisitos - Auditoria de
/// Regras) — checked directly against the DB (ApplicationUser.IsRulesAuditor), not a JWT claim,
/// so a grant/revoke via `make grant-rules-auditor` takes effect on the auditor's very next
/// request.
/// </summary>
[ApiController]
[Route("api/traits")]
[Authorize]
public class TraitsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TraitResponse>>> List([FromQuery] string? nome)
    {
        var query = db.Traits.Where(t => !t.IsDeleted);
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(t => EF.Functions.ILike(t.Nome, $"%{nome}%"));

        return await query
            .Select(t => new TraitResponse(t.Id.ToString(), t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString(), t.RequerEspecificacao, t.IsCustomized))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<TraitResponse>> Create(CreateTraitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!Enum.TryParse<Polaridade>(request.Polaridade, out var polaridade))
            return BadRequest("Polaridade inválida.");
        if (!IsCustoSignConsistent(request.Custo, polaridade))
            return BadRequest("O sinal do Custo não é consistente com a Polaridade (Positiva >= 0, Negativa <= 0).");
        if (await db.Traits.AnyAsync(t => t.Nome == request.Nome && t.Custo == request.Custo && t.Polaridade == polaridade))
            return BadRequest("Já existe uma característica com esse Nome, Custo e Polaridade.");

        var trait = new Trait
        {
            Id = Guid.NewGuid(),
            Nome = request.Nome,
            Descricao = request.Descricao,
            Custo = request.Custo,
            Polaridade = polaridade,
            RequerEspecificacao = request.RequerEspecificacao,
            IsCustomized = true,
            UpdatedByUserId = CurrentUserId(),
            UpdatedAt = DateTime.UtcNow,
        };
        db.Traits.Add(trait);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(trait));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateTraitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (trait is null)
            return NotFound();

        if (!Enum.TryParse<Polaridade>(request.Polaridade, out var polaridade))
            return BadRequest("Polaridade inválida.");
        if (!IsCustoSignConsistent(request.Custo, polaridade))
            return BadRequest("O sinal do Custo não é consistente com a Polaridade (Positiva >= 0, Negativa <= 0).");
        if (await db.Traits.AnyAsync(t => t.Id != id && t.Nome == request.Nome && t.Custo == request.Custo && t.Polaridade == polaridade))
            return BadRequest("Já existe uma característica com esse Nome, Custo e Polaridade.");

        trait.Nome = request.Nome;
        trait.Descricao = request.Descricao;
        trait.Custo = request.Custo;
        trait.Polaridade = polaridade;
        trait.RequerEspecificacao = request.RequerEspecificacao;
        trait.IsCustomized = true;
        trait.UpdatedByUserId = CurrentUserId();
        trait.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (trait is null)
            return NotFound();

        var inUse = await db.CharacterTraits.AnyAsync(t => t.TraitId == id)
            || await db.NpcTraits.AnyAsync(t => t.TraitId == id)
            || await db.CreatureTraits.AnyAsync(t => t.TraitId == id);
        if (inUse)
            return Conflict("Esta característica está em uso em pelo menos uma ficha e não pode ser excluída.");

        trait.IsDeleted = true;
        trait.UpdatedByUserId = CurrentUserId();
        trait.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static bool IsCustoSignConsistent(int custo, Polaridade polaridade) =>
        polaridade == Polaridade.Positiva ? custo >= 0 : custo <= 0;

    private static TraitResponse ToResponse(Trait trait) =>
        new(trait.Id.ToString(), trait.Nome, trait.Descricao, trait.Custo, trait.Polaridade.ToString(), trait.RequerEspecificacao, trait.IsCustomized);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
