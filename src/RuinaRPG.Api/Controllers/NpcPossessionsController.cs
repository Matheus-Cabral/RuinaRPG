using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/npc-sheets/{sheetId}")]
public class NpcPossessionsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost("inventory")]
    public async Task<ActionResult<NpcInventoryItemResponse>> AddInventoryItem(Guid sheetId, AddNpcInventoryItemRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
            return BadRequest("Item não encontrado.");

        var inventoryItem = new NpcInventoryItem { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = itemId, Qtd = request.Qtd };
        db.NpcInventoryItems.Add(inventoryItem);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToInventoryItemResponseAsync(inventoryItem));
    }

    [HttpGet("inventory")]
    public async Task<ActionResult<List<NpcInventoryItemResponse>>> ListInventory(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var items = await db.NpcInventoryItems.Where(i => i.NpcSheetId == sheetId).ToListAsync();
        var responses = new List<NpcInventoryItemResponse>();
        foreach (var item in items)
            responses.Add(await ToInventoryItemResponseAsync(item));
        return responses;
    }

    [HttpDelete("inventory/{id}")]
    public async Task<IActionResult> DeleteInventoryItem(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var item = await db.NpcInventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.NpcSheetId == sheetId);
        if (item is null)
            return NotFound();

        db.NpcInventoryItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("artifacts")]
    public async Task<ActionResult<NpcArtifactResponse>> AddArtifact(Guid sheetId, AddNpcArtifactRequest request)
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
        var existingOfSameType = await db.NpcArtifacts
            .Where(a => a.NpcSheetId == sheetId)
            .Join(db.Set<Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i.TipoDeAlvo)
            .CountAsync(t => t == newItem.TipoDeAlvo);
        if (existingOfSameType >= 3)
            return BadRequest($"Limite de 3 Artefatos do tipo {newItem.TipoDeAlvo} já atingido.");

        var artifact = new NpcArtifact { Id = Guid.NewGuid(), NpcSheetId = sheetId, ArtifactItemId = artifactItemId };
        db.NpcArtifacts.Add(artifact);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToArtifactResponseAsync(artifact));
    }

    [HttpGet("artifacts")]
    public async Task<ActionResult<List<NpcArtifactResponse>>> ListArtifacts(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var artifacts = await db.NpcArtifacts.Where(a => a.NpcSheetId == sheetId).ToListAsync();
        var responses = new List<NpcArtifactResponse>();
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

        var artifact = await db.NpcArtifacts.FirstOrDefaultAsync(a => a.Id == id && a.NpcSheetId == sheetId);
        if (artifact is null)
            return NotFound();

        db.NpcArtifacts.Remove(artifact);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("affections")]
    public async Task<ActionResult<NpcAffectionResponse>> AddAffection(Guid sheetId, AddNpcAffectionRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affection = new NpcAffection { Id = Guid.NewGuid(), NpcSheetId = sheetId, Nome = request.Nome, Favorabilidade = request.Favorabilidade };
        db.NpcAffections.Add(affection);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToAffectionResponse(affection));
    }

    [HttpGet("affections")]
    public async Task<ActionResult<List<NpcAffectionResponse>>> ListAffections(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affections = await db.NpcAffections.Where(a => a.NpcSheetId == sheetId).ToListAsync();
        return affections.Select(ToAffectionResponse).ToList();
    }

    [HttpDelete("affections/{id}")]
    public async Task<IActionResult> DeleteAffection(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var affection = await db.NpcAffections.FirstOrDefaultAsync(a => a.Id == id && a.NpcSheetId == sheetId);
        if (affection is null)
            return NotFound();

        db.NpcAffections.Remove(affection);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("traits")]
    public async Task<ActionResult<NpcTraitResponse>> AddTrait(Guid sheetId, AddNpcTraitRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.TraitId, out var traitId))
            return BadRequest("TraitId inválido.");

        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == traitId);
        if (trait is null)
            return BadRequest("Trait não encontrado.");

        var npcTrait = new NpcTrait { Id = Guid.NewGuid(), NpcSheetId = sheetId, TraitId = traitId, Polaridade = trait.Polaridade };
        db.NpcTraits.Add(npcTrait);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToTraitResponse(npcTrait, trait));
    }

    [HttpGet("traits")]
    public async Task<ActionResult<NpcTraitsListResponse>> ListTraits(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var rows = await db.NpcTraits
            .Where(t => t.NpcSheetId == sheetId)
            .Join(db.Traits, nt => nt.TraitId, t => t.Id, (nt, t) => new NpcTraitResponse(nt.Id.ToString(), t.Id.ToString(), t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString()))
            .ToListAsync();

        var positivas = rows.Where(r => r.Polaridade == "Positiva").ToList();
        var negativas = rows.Where(r => r.Polaridade == "Negativa").ToList();
        return new NpcTraitsListResponse(positivas, positivas.Sum(r => r.Custo), negativas, negativas.Sum(r => r.Custo));
    }

    [HttpDelete("traits/{id}")]
    public async Task<IActionResult> DeleteTrait(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var npcTrait = await db.NpcTraits.FirstOrDefaultAsync(t => t.Id == id && t.NpcSheetId == sheetId);
        if (npcTrait is null)
            return NotFound();

        db.NpcTraits.Remove(npcTrait);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> CheckAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (sheet.GmId != CurrentGmId())
            return NotFound();

        return null;
    }

    private async Task<NpcInventoryItemResponse> ToInventoryItemResponseAsync(NpcInventoryItem inventoryItem)
    {
        var item = await db.Items.SingleAsync(i => i.Id == inventoryItem.ItemId);
        return new NpcInventoryItemResponse(inventoryItem.Id.ToString(), item.Id.ToString(), item.Nome, item.Peso, inventoryItem.Qtd, item.Peso * inventoryItem.Qtd);
    }

    private async Task<NpcArtifactResponse> ToArtifactResponseAsync(NpcArtifact artifact)
    {
        var item = await db.Set<Artefato>().SingleAsync(a => a.Id == artifact.ArtifactItemId);
        return new NpcArtifactResponse(artifact.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeAlvo?.ToString() ?? string.Empty, item.Alvo ?? string.Empty, item.Valor ?? 0);
    }

    private static NpcAffectionResponse ToAffectionResponse(NpcAffection affection) =>
        new(affection.Id.ToString(), affection.Nome, affection.Favorabilidade);

    private static NpcTraitResponse ToTraitResponse(NpcTrait npcTrait, Trait trait) =>
        new(npcTrait.Id.ToString(), trait.Id.ToString(), trait.Nome, trait.Descricao, trait.Custo, trait.Polaridade.ToString());

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
