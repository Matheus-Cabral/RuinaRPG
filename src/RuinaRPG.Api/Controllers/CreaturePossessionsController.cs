using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
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

        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == traitId && !t.IsDeleted);
        if (trait is null)
            return BadRequest("Trait não encontrado.");

        if (trait.RequerEspecificacao && string.IsNullOrWhiteSpace(request.Especificacao))
            return BadRequest("Esta característica exige uma especificação.");

        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        var pontosDisponiveis = TraitPointBudgetCalculator.Compute(sheet!.Nivel, rules.Niveis);
        var existingTotal = await db.CreatureTraits
            .Where(t => t.CreatureSheetId == sheetId && t.Polaridade == trait.Polaridade)
            .Join(db.Traits, ct => ct.TraitId, t => t.Id, (ct, t) => t.Custo)
            .SumAsync();
        if (Math.Abs(existingTotal) + Math.Abs(trait.Custo) > pontosDisponiveis)
            return BadRequest($"Gasto excede os {pontosDisponiveis} pontos de Característica {trait.Polaridade} disponíveis.");

        var creatureTrait = new CreatureTrait
        {
            Id = Guid.NewGuid(),
            CreatureSheetId = sheetId,
            TraitId = traitId,
            Polaridade = trait.Polaridade,
            Especificacao = trait.RequerEspecificacao ? request.Especificacao : null,
        };
        db.CreatureTraits.Add(creatureTrait);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToTraitResponse(creatureTrait, trait));
    }

    [HttpGet("traits")]
    public async Task<ActionResult<CreatureTraitsListResponse>> ListTraits(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var rows = await db.CreatureTraits
            .Where(t => t.CreatureSheetId == sheetId)
            .Join(db.Traits, ct => ct.TraitId, t => t.Id, (ct, t) => new CreatureTraitResponse(ct.Id.ToString(), t.Id.ToString(), t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString(), ct.Especificacao, t.RequerEspecificacao))
            .ToListAsync();

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

    private static CreatureTraitResponse ToTraitResponse(CreatureTrait creatureTrait, Trait trait) =>
        new(creatureTrait.Id.ToString(), trait.Id.ToString(), trait.Nome, trait.Descricao, trait.Custo, trait.Polaridade.ToString(), creatureTrait.Especificacao, trait.RequerEspecificacao);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
