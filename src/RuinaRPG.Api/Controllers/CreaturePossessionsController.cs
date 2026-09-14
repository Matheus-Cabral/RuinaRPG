using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}")]
public class CreaturePossessionsController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    /// <summary>
    /// R0006 3.d: Butim (Spoils), not the plain inventory the Personagem/NPC sheet has — Custo and
    /// CustoTotal are computed live from the linked Item's Preco (never persisted/stale), and a DT
    /// field is accepted/returned that the inventory-item equivalent doesn't have.
    /// </summary>
    [HttpPost("spoils")]
    public async Task<ActionResult<CreatureSpoilResponse>> AddSpoil(Guid sheetId, AddCreatureSpoilRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
            return BadRequest("Item não encontrado.");

        var spoil = new CreatureSpoil { Id = Guid.NewGuid(), CreatureSheetId = sheetId, ItemId = itemId, Qtd = request.Qtd, DT = request.DT };
        db.CreatureSpoils.Add(spoil);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToSpoilResponseAsync(spoil));
    }

    [HttpGet("spoils")]
    public async Task<ActionResult<List<CreatureSpoilResponse>>> ListSpoils(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var spoils = await db.CreatureSpoils.Where(i => i.CreatureSheetId == sheetId).ToListAsync();
        var responses = new List<CreatureSpoilResponse>();
        foreach (var spoil in spoils)
            responses.Add(await ToSpoilResponseAsync(spoil));
        return responses;
    }

    [HttpPut("spoils/{id}/qtd")]
    public async Task<IActionResult> UpdateSpoilQtd(Guid sheetId, Guid id, [FromBody] int qtd)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var spoil = await db.CreatureSpoils.FirstOrDefaultAsync(i => i.Id == id && i.CreatureSheetId == sheetId);
        if (spoil is null)
            return NotFound();

        spoil.Qtd = qtd;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("spoils/{id}")]
    public async Task<IActionResult> DeleteSpoil(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var spoil = await db.CreatureSpoils.FirstOrDefaultAsync(i => i.Id == id && i.CreatureSheetId == sheetId);
        if (spoil is null)
            return NotFound();

        db.CreatureSpoils.Remove(spoil);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("artifacts")]
    public async Task<ActionResult<CreatureArtifactResponse>> AddArtifact(Guid sheetId, AddCreatureArtifactRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ArtifactItemId, out var artifactItemId))
            return BadRequest("ArtifactItemId inválido.");

        var newItem = await db.Set<Artefato>().FirstOrDefaultAsync(a => a.Id == artifactItemId);
        if (newItem is null)
            return BadRequest("Item de artefato não encontrado.");

        // Modelo de Dados: "limite de 3 por TipoDeAlvo validado na aplicação, não no schema".
        var existingOfSameType = await db.CreatureArtifacts
            .Where(a => a.CreatureSheetId == sheetId)
            .Join(db.Set<Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i.TipoDeAlvo)
            .CountAsync(t => t == newItem.TipoDeAlvo);
        if (existingOfSameType >= 3)
            return BadRequest($"Limite de 3 Artefatos do tipo {newItem.TipoDeAlvo} já atingido.");

        var artifact = new CreatureArtifact { Id = Guid.NewGuid(), CreatureSheetId = sheetId, ArtifactItemId = artifactItemId };
        db.CreatureArtifacts.Add(artifact);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToArtifactResponseAsync(artifact));
    }

    [HttpGet("artifacts")]
    public async Task<ActionResult<List<CreatureArtifactResponse>>> ListArtifacts(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var artifacts = await db.CreatureArtifacts.Where(a => a.CreatureSheetId == sheetId).ToListAsync();
        var responses = new List<CreatureArtifactResponse>();
        foreach (var artifact in artifacts)
            responses.Add(await ToArtifactResponseAsync(artifact));
        return responses;
    }

    [HttpDelete("artifacts/{id}")]
    public async Task<IActionResult> DeleteArtifact(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var artifact = await db.CreatureArtifacts.FirstOrDefaultAsync(a => a.Id == id && a.CreatureSheetId == sheetId);
        if (artifact is null)
            return NotFound();

        db.CreatureArtifacts.Remove(artifact);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("affections")]
    public async Task<ActionResult<CreatureAffectionResponse>> AddAffection(Guid sheetId, AddCreatureAffectionRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affection = new CreatureAffection { Id = Guid.NewGuid(), CreatureSheetId = sheetId, Nome = request.Nome, Favorabilidade = request.Favorabilidade };
        db.CreatureAffections.Add(affection);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToAffectionResponse(affection));
    }

    [HttpGet("affections")]
    public async Task<ActionResult<List<CreatureAffectionResponse>>> ListAffections(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affections = await db.CreatureAffections.Where(a => a.CreatureSheetId == sheetId).ToListAsync();
        return affections.Select(ToAffectionResponse).ToList();
    }

    [HttpDelete("affections/{id}")]
    public async Task<IActionResult> DeleteAffection(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affection = await db.CreatureAffections.FirstOrDefaultAsync(a => a.Id == id && a.CreatureSheetId == sheetId);
        if (affection is null)
            return NotFound();

        db.CreatureAffections.Remove(affection);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("traits")]
    public async Task<ActionResult<CreatureTraitResponse>> AddTrait(Guid sheetId, AddCreatureTraitRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.TraitId, out var traitId))
            return BadRequest("TraitId inválido.");

        // Try the shared catalog first, then the creature-exclusive one — an Id can only ever
        // exist in one of the two tables (separate Guid spaces), so this is unambiguous.
        var normalTrait = await db.Traits.FirstOrDefaultAsync(t => t.Id == traitId && !t.IsDeleted);
        var exclusiveTrait = normalTrait is null
            ? await db.Set<CreatureExclusiveTrait>().FirstOrDefaultAsync(t => t.Id == traitId && !t.IsDeleted)
            : null;
        if (normalTrait is null && exclusiveTrait is null)
            return BadRequest("Trait não encontrado.");

        var trait = normalTrait is not null ? ToResolved(normalTrait) : ToResolved(exclusiveTrait!);

        if (trait.RequerEspecificacao && string.IsNullOrWhiteSpace(request.Especificacao))
            return BadRequest("Esta característica exige uma especificação.");

        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        var pontosDisponiveis = TraitPointBudgetCalculator.Compute(sheet!.Nivel, rules.Niveis);
        var existingTotal = await SumExistingCustoAsync(sheetId, trait.Polaridade);
        if (Math.Abs(existingTotal) + Math.Abs(trait.Custo) > pontosDisponiveis)
            return BadRequest($"Gasto excede os {pontosDisponiveis} pontos de Característica {trait.Polaridade} disponíveis.");

        var creatureTrait = new CreatureTrait
        {
            Id = Guid.NewGuid(),
            CreatureSheetId = sheetId,
            TraitId = normalTrait?.Id,
            CreatureExclusiveTraitId = exclusiveTrait?.Id,
            Polaridade = trait.Polaridade,
            Especificacao = trait.RequerEspecificacao ? request.Especificacao : null,
        };
        db.CreatureTraits.Add(creatureTrait);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToTraitResponse(creatureTrait, trait));
    }

    /// <summary>
    /// Sums Custo across every CreatureTrait of the given Polaridade on this sheet, resolving each
    /// row against whichever catalog it points to (TraitId vs. CreatureExclusiveTraitId) — two
    /// batched lookups instead of one LINQ .Join(), since a single SQL join can't span two
    /// different tables through one nullable-either-way FK pair. N stays small (a sheet's own
    /// characteristic count), so this is two extra queries total, not one per row.
    /// </summary>
    private async Task<int> SumExistingCustoAsync(Guid sheetId, Polaridade polaridade)
    {
        var rows = await db.CreatureTraits.Where(t => t.CreatureSheetId == sheetId && t.Polaridade == polaridade).ToListAsync();
        var normalIds = rows.Where(r => r.TraitId is not null).Select(r => r.TraitId!.Value).ToList();
        var exclusiveIds = rows.Where(r => r.CreatureExclusiveTraitId is not null).Select(r => r.CreatureExclusiveTraitId!.Value).ToList();

        var normalTotal = await db.Traits.Where(t => normalIds.Contains(t.Id)).SumAsync(t => t.Custo);
        var exclusiveTotal = await db.Set<CreatureExclusiveTrait>().Where(t => exclusiveIds.Contains(t.Id)).SumAsync(t => t.Custo);
        return normalTotal + exclusiveTotal;
    }

    [HttpGet("traits")]
    public async Task<ActionResult<CreatureTraitsListResponse>> ListTraits(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var creatureTraits = await db.CreatureTraits.Where(t => t.CreatureSheetId == sheetId).ToListAsync();
        var normalIds = creatureTraits.Where(t => t.TraitId is not null).Select(t => t.TraitId!.Value).ToList();
        var exclusiveIds = creatureTraits.Where(t => t.CreatureExclusiveTraitId is not null).Select(t => t.CreatureExclusiveTraitId!.Value).ToList();

        var normalById = await db.Traits.Where(t => normalIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var exclusiveById = await db.Set<CreatureExclusiveTrait>().Where(t => exclusiveIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);

        var rows = new List<CreatureTraitResponse>();
        foreach (var ct in creatureTraits)
        {
            if (ct.TraitId is not null && normalById.TryGetValue(ct.TraitId.Value, out var normalTrait))
                rows.Add(ToTraitResponse(ct, ToResolved(normalTrait)));
            else if (ct.CreatureExclusiveTraitId is not null && exclusiveById.TryGetValue(ct.CreatureExclusiveTraitId.Value, out var exclusiveTrait))
                rows.Add(ToTraitResponse(ct, ToResolved(exclusiveTrait)));
            // else: referenced catalog entry is missing (shouldn't happen given the FK constraints) — skip rather than 500.
        }

        var positivas = rows.Where(r => r.Polaridade == "Positiva").ToList();
        var negativas = rows.Where(r => r.Polaridade == "Negativa").ToList();
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        var pontosDisponiveis = TraitPointBudgetCalculator.Compute(sheet!.Nivel, rules.Niveis);
        return new CreatureTraitsListResponse(positivas, positivas.Sum(r => r.Custo), negativas, negativas.Sum(r => r.Custo), pontosDisponiveis);
    }

    [HttpDelete("traits/{id}")]
    public async Task<IActionResult> DeleteTrait(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var creatureTrait = await db.CreatureTraits.FirstOrDefaultAsync(t => t.Id == id && t.CreatureSheetId == sheetId);
        if (creatureTrait is null)
            return NotFound();

        db.CreatureTraits.Remove(creatureTrait);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> CheckAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        return null;
    }

    private async Task<CreatureSpoilResponse> ToSpoilResponseAsync(CreatureSpoil spoil)
    {
        var item = await db.Items.SingleAsync(i => i.Id == spoil.ItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        return new CreatureSpoilResponse(spoil.Id.ToString(), item.Id.ToString(), item.Nome, item.Preco, spoil.Qtd, item.Preco * spoil.Qtd, spoil.DT, imageUrl, item.Descricao);
    }

    private async Task<CreatureArtifactResponse> ToArtifactResponseAsync(CreatureArtifact artifact)
    {
        var item = await db.Set<Artefato>().SingleAsync(a => a.Id == artifact.ArtifactItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        return new CreatureArtifactResponse(artifact.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeAlvo?.ToString() ?? string.Empty, item.Alvo ?? string.Empty, item.Valor ?? 0, imageUrl, item.Descricao);
    }

    private async Task<string?> ResolveImageUrlAsync(Guid? imageId)
    {
        if (imageId is null)
            return null;

        var image = await db.Images.FindAsync(imageId.Value);
        return image is not null ? $"/images/{image.Path}" : null;
    }

    private static CreatureAffectionResponse ToAffectionResponse(CreatureAffection affection) =>
        new(affection.Id.ToString(), affection.Nome, affection.Favorabilidade);

    /// <summary>
    /// Trait and CreatureExclusiveTrait have the same relevant shape but are unrelated C# types
    /// (two separate tables, see the design spec) — this is the common shape AddTrait/ListTraits
    /// resolve either one into before building a response, so the rest of this controller doesn't
    /// need to branch on which catalog a characteristic came from.
    /// </summary>
    private sealed record ResolvedCatalogTrait(Guid Id, string Nome, string Descricao, int Custo, Polaridade Polaridade, bool RequerEspecificacao);

    private static ResolvedCatalogTrait ToResolved(Trait t) => new(t.Id, t.Nome, t.Descricao, t.Custo, t.Polaridade, t.RequerEspecificacao);
    private static ResolvedCatalogTrait ToResolved(CreatureExclusiveTrait t) => new(t.Id, t.Nome, t.Descricao, t.Custo, t.Polaridade, t.RequerEspecificacao);

    private static CreatureTraitResponse ToTraitResponse(CreatureTrait creatureTrait, ResolvedCatalogTrait trait) =>
        new(creatureTrait.Id.ToString(), trait.Id.ToString(), trait.Nome, trait.Descricao, trait.Custo, trait.Polaridade.ToString(), creatureTrait.Especificacao, trait.RequerEspecificacao);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
