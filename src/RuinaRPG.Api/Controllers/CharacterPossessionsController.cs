using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public class CharacterPossessionsController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    [HttpPost("inventory")]
    public async Task<ActionResult<CharacterInventoryItemResponse>> AddInventoryItem(Guid sheetId, AddCharacterInventoryItemRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        // Ficha de Personagem 5.a: "Armas, Armaduras e Escudos não aparecem aqui" — restricted to
        // Item Geral, unlike the base Items query this used to run (which matched any catalog
        // type, letting a Weapon/Armor/Shield/Artefato double-count its Peso in both places).
        var item = await db.Set<ItemGeral>().FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
            return BadRequest("Item Geral não encontrado.");

        var inventoryItem = new CharacterInventoryItem { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = itemId, Qtd = request.Qtd };
        db.CharacterInventoryItems.Add(inventoryItem);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToInventoryItemResponseAsync(inventoryItem));
    }

    [HttpGet("inventory")]
    public async Task<ActionResult<List<CharacterInventoryItemResponse>>> ListInventory(Guid sheetId)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var items = await db.CharacterInventoryItems.Where(i => i.CharacterSheetId == sheetId).ToListAsync();
        var responses = new List<CharacterInventoryItemResponse>();
        foreach (var item in items)
            responses.Add(await ToInventoryItemResponseAsync(item));
        return responses;
    }

    [HttpDelete("inventory/{id}")]
    public async Task<IActionResult> DeleteInventoryItem(Guid sheetId, Guid id)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var item = await db.CharacterInventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.CharacterSheetId == sheetId);
        if (item is null)
            return NotFound();

        db.CharacterInventoryItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("artifacts")]
    public async Task<ActionResult<CharacterArtifactResponse>> AddArtifact(Guid sheetId, AddCharacterArtifactRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ArtifactItemId, out var artifactItemId))
            return BadRequest("ArtifactItemId inválido.");

        var newItem = await db.Set<Artefato>().FirstOrDefaultAsync(a => a.Id == artifactItemId);
        if (newItem is null)
            return BadRequest("Item de artefato não encontrado.");

        // Modelo de Dados: "limite de 3 por TipoDeAlvo validado na aplicação, não no schema".
        var existingOfSameType = await db.CharacterArtifacts
            .Where(a => a.CharacterSheetId == sheetId)
            .Join(db.Set<Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i.TipoDeAlvo)
            .CountAsync(t => t == newItem.TipoDeAlvo);
        if (existingOfSameType >= 3)
            return BadRequest($"Limite de 3 Artefatos do tipo {newItem.TipoDeAlvo} já atingido.");

        var artifact = new CharacterArtifact { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ArtifactItemId = artifactItemId };
        db.CharacterArtifacts.Add(artifact);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToArtifactResponseAsync(artifact));
    }

    [HttpGet("artifacts")]
    public async Task<ActionResult<List<CharacterArtifactResponse>>> ListArtifacts(Guid sheetId)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var artifacts = await db.CharacterArtifacts.Where(a => a.CharacterSheetId == sheetId).ToListAsync();
        var responses = new List<CharacterArtifactResponse>();
        foreach (var artifact in artifacts)
            responses.Add(await ToArtifactResponseAsync(artifact));
        return responses;
    }

    [HttpDelete("artifacts/{id}")]
    public async Task<IActionResult> DeleteArtifact(Guid sheetId, Guid id)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var artifact = await db.CharacterArtifacts.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (artifact is null)
            return NotFound();

        db.CharacterArtifacts.Remove(artifact);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("affections")]
    public async Task<ActionResult<CharacterAffectionResponse>> AddAffection(Guid sheetId, AddCharacterAffectionRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affection = new CharacterAffection { Id = Guid.NewGuid(), CharacterSheetId = sheetId, Nome = request.Nome, Favorabilidade = request.Favorabilidade };
        db.CharacterAffections.Add(affection);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToAffectionResponse(affection));
    }

    [HttpGet("affections")]
    public async Task<ActionResult<List<CharacterAffectionResponse>>> ListAffections(Guid sheetId)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affections = await db.CharacterAffections.Where(a => a.CharacterSheetId == sheetId).ToListAsync();
        return affections.Select(ToAffectionResponse).ToList();
    }

    [HttpPut("affections/{id}")]
    public async Task<ActionResult<CharacterAffectionResponse>> UpdateAffection(Guid sheetId, Guid id, UpdateCharacterAffectionRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affection = await db.CharacterAffections.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (affection is null)
            return NotFound();

        affection.Nome = request.Nome;
        affection.Favorabilidade = request.Favorabilidade;
        await db.SaveChangesAsync();

        return Ok(ToAffectionResponse(affection));
    }

    [HttpDelete("affections/{id}")]
    public async Task<IActionResult> DeleteAffection(Guid sheetId, Guid id)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affection = await db.CharacterAffections.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (affection is null)
            return NotFound();

        db.CharacterAffections.Remove(affection);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("traits")]
    public async Task<ActionResult<CharacterTraitResponse>> AddTrait(Guid sheetId, AddCharacterTraitRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.TraitId, out var traitId))
            return BadRequest("TraitId inválido.");

        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == traitId);
        if (trait is null)
            return BadRequest("Trait não encontrado.");

        if (trait.RequerEspecificacao && string.IsNullOrWhiteSpace(request.Especificacao))
            return BadRequest("Esta característica exige uma especificação.");

        // Características 5.d: the budget applies independently to each list — Positivas total may
        // not exceed it, and Negativas total (magnitude; Custo is stored negative) may not exceed it
        // either. Mirrors CharacterAttributesController's Atributos enforcement (2.a).
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        var pontosDisponiveis = TraitPointBudgetCalculator.Compute(sheet!.Nivel, rules.Niveis);
        var existingTotal = await db.CharacterTraits
            .Where(t => t.CharacterSheetId == sheetId && t.Polaridade == trait.Polaridade)
            .Join(db.Traits, ct => ct.TraitId, t => t.Id, (ct, t) => t.Custo)
            .SumAsync();
        if (Math.Abs(existingTotal) + Math.Abs(trait.Custo) > pontosDisponiveis)
            return BadRequest($"Gasto excede os {pontosDisponiveis} pontos de Característica {trait.Polaridade} disponíveis.");

        var characterTrait = new CharacterTrait
        {
            Id = Guid.NewGuid(),
            CharacterSheetId = sheetId,
            TraitId = traitId,
            Polaridade = trait.Polaridade,
            Especificacao = trait.RequerEspecificacao ? request.Especificacao : null,
        };
        db.CharacterTraits.Add(characterTrait);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToTraitResponse(characterTrait, trait));
    }

    [HttpGet("traits")]
    public async Task<ActionResult<CharacterTraitsListResponse>> ListTraits(Guid sheetId)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var rows = await db.CharacterTraits
            .Where(t => t.CharacterSheetId == sheetId)
            .Join(db.Traits, ct => ct.TraitId, t => t.Id, (ct, t) => new CharacterTraitResponse(ct.Id.ToString(), t.Id.ToString(), t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString(), ct.Especificacao, t.RequerEspecificacao))
            .ToListAsync();

        var positivas = rows.Where(r => r.Polaridade == "Positiva").ToList();
        var negativas = rows.Where(r => r.Polaridade == "Negativa").ToList();
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        var pontosDisponiveis = TraitPointBudgetCalculator.Compute(sheet!.Nivel, rules.Niveis);
        return new CharacterTraitsListResponse(positivas, positivas.Sum(r => r.Custo), negativas, negativas.Sum(r => r.Custo), pontosDisponiveis);
    }

    [HttpDelete("traits/{id}")]
    public async Task<IActionResult> DeleteTrait(Guid sheetId, Guid id)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var characterTrait = await db.CharacterTraits.FirstOrDefaultAsync(t => t.Id == id && t.CharacterSheetId == sheetId);
        if (characterTrait is null)
            return NotFound();

        db.CharacterTraits.Remove(characterTrait);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> CheckEditAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        return null;
    }

    private async Task<CharacterInventoryItemResponse> ToInventoryItemResponseAsync(CharacterInventoryItem inventoryItem)
    {
        var item = await db.Items.SingleAsync(i => i.Id == inventoryItem.ItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        return new CharacterInventoryItemResponse(inventoryItem.Id.ToString(), item.Id.ToString(), item.Nome, item.Peso, inventoryItem.Qtd, item.Peso * inventoryItem.Qtd, imageUrl, item.Descricao);
    }

    private async Task<CharacterArtifactResponse> ToArtifactResponseAsync(CharacterArtifact artifact)
    {
        var item = await db.Set<Artefato>().SingleAsync(a => a.Id == artifact.ArtifactItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        return new CharacterArtifactResponse(artifact.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeAlvo?.ToString() ?? string.Empty, item.Alvo ?? string.Empty, item.Valor ?? 0, imageUrl, item.Descricao);
    }

    private async Task<string?> ResolveImageUrlAsync(Guid? imageId)
    {
        if (imageId is null)
            return null;

        var image = await db.Images.FindAsync(imageId.Value);
        return image is not null ? $"/images/{image.Path}" : null;
    }

    private static CharacterAffectionResponse ToAffectionResponse(CharacterAffection affection) =>
        new(affection.Id.ToString(), affection.Nome, affection.Favorabilidade);

    private static CharacterTraitResponse ToTraitResponse(CharacterTrait characterTrait, Trait trait) =>
        new(characterTrait.Id.ToString(), trait.Id.ToString(), trait.Nome, trait.Descricao, trait.Custo, trait.Polaridade.ToString(), characterTrait.Especificacao, trait.RequerEspecificacao);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
