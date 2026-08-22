using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public class CharacterPossessionsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost("inventory")]
    public async Task<ActionResult<CharacterInventoryItemResponse>> AddInventoryItem(Guid sheetId, AddCharacterInventoryItemRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
            return BadRequest("Item não encontrado.");

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
        return new CharacterInventoryItemResponse(inventoryItem.Id.ToString(), item.Id.ToString(), item.Nome, item.Peso, inventoryItem.Qtd, item.Peso * inventoryItem.Qtd);
    }

    private async Task<CharacterArtifactResponse> ToArtifactResponseAsync(CharacterArtifact artifact)
    {
        var item = await db.Set<Artefato>().SingleAsync(a => a.Id == artifact.ArtifactItemId);
        return new CharacterArtifactResponse(artifact.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeAlvo?.ToString() ?? string.Empty, item.Alvo ?? string.Empty, item.Valor ?? 0);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
