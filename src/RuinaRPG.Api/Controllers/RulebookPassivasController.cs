using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Requisitos - Livro de Regras R0010: a aba Habilidades Passivas. Ao contrário do resto do Livro
/// (RulebookController, anônimo e igual para todos), as Passivas vivem no banco de cada GM — por isso
/// este endpoint exige login e responde conforme o papel: o GM vê o próprio banco; o Jogador, as
/// Passivas anexadas como públicas à campanha escolhida (mesmo alcance de CampaignCatalogController).
/// </summary>
[ApiController]
[Authorize]
[Route("api/rulebook/passivas")]
public class RulebookPassivasController(RuinaRpgDbContext db, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PassivaDoLivroResponse>>> Get([FromQuery] Guid? campaignId)
    {
        var callerId = CurrentUserId();
        var query = db.SpellAbilityBankEntries.Where(e => e.Tipo == SpellAbilityTipo.Passiva && e.Categoria != null);

        if (User.IsInRole("GM"))
            query = query.Where(e => e.GmId == callerId);
        else
        {
            if (campaignId is not { } id)
                return BadRequest("Informe a campanha.");
            if (!await db.Campaigns.AnyAsync(c => c.Id == id))
                return NotFound();
            if (!await db.CampaignMembers.AnyAsync(m => m.CampaignId == id && m.UserId == callerId))
                return Forbid();
            query = query.Where(e => db.CampaignAttachments.Any(a => a.CampaignId == id && a.IsPublic && a.SpellAbilityBankEntryId == e.Id));
        }

        var entries = await query.OrderBy(e => e.Nome).ToListAsync();

        var historicoIds = entries.Select(e => e.Requisitos?.HistoricoId).OfType<Guid>().Distinct().ToList();
        var historicos = await db.Historicos.Where(h => historicoIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id, h => h.Nome);
        var porId = await pericias.PorIdAsync();
        // Uma Perícia removida some dos requisitos, como em RequisitosDePassivaMapper.ToDto.
        string? NomeDaPericia(int periciaId) => porId.TryGetValue(periciaId, out var p) && !p.IsDeleted ? p.Nome : null;

        return entries.Select(e => new PassivaDoLivroResponse(
            e.Id.ToString(), e.Nome, e.Categoria!.Value.ToString(), e.Descricao,
            PassivaRequisitosEvaluator.Descrever(e.Requisitos,
                e.Requisitos?.HistoricoId is { } h ? historicos.GetValueOrDefault(h) : null, NomeDaPericia).ToList(),
            e.Requisitos?.Vocacao is { } vocacao ? RequisitoLabels.Vocacao(vocacao) : null,
            string.IsNullOrWhiteSpace(e.Requisitos?.Classe) ? null : e.Requisitos!.Classe!.Trim())).ToList();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
