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
/// A second, genuinely separate characteristic catalog — only a Ficha de Criatura's "add
/// characteristic" picker (CreaturePossessionsController.AddTrait) ever resolves an Id against
/// this table. TraitsController, CompendioController, CharacterPossessionsController and
/// NpcPossessionsController never read this table at all — that is what keeps a row created here
/// out of the Livro de Regras/Compêndio and unreachable from Personagem/NPC, by construction
/// rather than by a filter. Global catalog (no owning GmId), same access model as Trait: List
/// open to any authenticated caller, Create/Update/Delete gated to the Rules Auditor.
/// </summary>
[ApiController]
[Route("api/creature-exclusive-traits")]
[Authorize]
public class CreatureExclusiveTraitsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CreatureExclusiveTraitResponse>>> List([FromQuery] string? nome)
    {
        var query = db.Set<CreatureExclusiveTrait>().Where(t => !t.IsDeleted);
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(t => EF.Functions.ILike(t.Nome, $"%{nome}%"));

        return await query
            .Select(t => new CreatureExclusiveTraitResponse(t.Id.ToString(), t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString(), t.RequerEspecificacao))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<CreatureExclusiveTraitResponse>> Create(CreateCreatureExclusiveTraitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!Enum.TryParse<Polaridade>(request.Polaridade, out var polaridade))
            return BadRequest("Polaridade inválida.");
        if (!IsCustoSignConsistent(request.Custo, polaridade))
            return BadRequest("O sinal do Custo não é consistente com a Polaridade (Positiva >= 0, Negativa <= 0).");
        if (await db.Set<CreatureExclusiveTrait>().AnyAsync(t => t.Nome == request.Nome && t.Custo == request.Custo && t.Polaridade == polaridade))
            return BadRequest("Já existe uma característica com esse Nome, Custo e Polaridade.");

        var trait = new CreatureExclusiveTrait
        {
            Id = Guid.NewGuid(),
            Nome = request.Nome,
            Descricao = request.Descricao,
            Custo = request.Custo,
            Polaridade = polaridade,
            RequerEspecificacao = request.RequerEspecificacao,
            UpdatedByUserId = CurrentUserId(),
            UpdatedAt = DateTime.UtcNow,
        };
        db.Set<CreatureExclusiveTrait>().Add(trait);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(trait));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateCreatureExclusiveTraitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var trait = await db.Set<CreatureExclusiveTrait>().FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (trait is null)
            return NotFound();

        if (!Enum.TryParse<Polaridade>(request.Polaridade, out var polaridade))
            return BadRequest("Polaridade inválida.");
        if (!IsCustoSignConsistent(request.Custo, polaridade))
            return BadRequest("O sinal do Custo não é consistente com a Polaridade (Positiva >= 0, Negativa <= 0).");
        if (await db.Set<CreatureExclusiveTrait>().AnyAsync(t => t.Id != id && t.Nome == request.Nome && t.Custo == request.Custo && t.Polaridade == polaridade))
            return BadRequest("Já existe uma característica com esse Nome, Custo e Polaridade.");

        trait.Nome = request.Nome;
        trait.Descricao = request.Descricao;
        trait.Custo = request.Custo;
        trait.Polaridade = polaridade;
        trait.RequerEspecificacao = request.RequerEspecificacao;
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

        var trait = await db.Set<CreatureExclusiveTrait>().FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (trait is null)
            return NotFound();

        // Only CreatureTrait can ever reference this table (its FK is the only one pointing here)
        // — no need to also check CharacterTraits/NpcTraits, which don't have this column.
        var inUse = await db.CreatureTraits.AnyAsync(t => t.CreatureExclusiveTraitId == id);
        if (inUse)
            return Conflict("Esta característica está em uso em pelo menos uma ficha de criatura e não pode ser excluída.");

        trait.IsDeleted = true;
        trait.UpdatedByUserId = CurrentUserId();
        trait.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static bool IsCustoSignConsistent(int custo, Polaridade polaridade) =>
        polaridade == Polaridade.Positiva ? custo >= 0 : custo <= 0;

    private static CreatureExclusiveTraitResponse ToResponse(CreatureExclusiveTrait trait) =>
        new(trait.Id.ToString(), trait.Nome, trait.Descricao, trait.Custo, trait.Polaridade.ToString(), trait.RequerEspecificacao);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
