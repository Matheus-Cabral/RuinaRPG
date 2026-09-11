using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public class NpcPossessionsController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
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

    [HttpPut("inventory/{id}/qtd")]
    public async Task<IActionResult> UpdateInventoryItemQtd(Guid sheetId, Guid id, [FromBody] int qtd)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var item = await db.NpcInventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.NpcSheetId == sheetId);
        if (item is null)
            return NotFound();

        item.Qtd = qtd;
        await db.SaveChangesAsync();
        return NoContent();
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

        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == traitId && !t.IsDeleted);
        if (trait is null)
            return BadRequest("Trait não encontrado.");

        if (trait.RequerEspecificacao && string.IsNullOrWhiteSpace(request.Especificacao))
            return BadRequest("Esta característica exige uma especificação.");

        var sheet = await db.NpcSheets.FindAsync(sheetId);
        var pontosDisponiveis = TraitPointBudgetCalculator.Compute(sheet!.Nivel, rules.Niveis);
        // Racial grants (IsRacial) are excluded — they cost 0 and never count toward this budget,
        // no matter what the underlying Trait's own Custo is (Requisitos - Ficha de NPCs / Ficha
        // de Personagem 5.d).
        var existingTotal = await db.NpcTraits
            .Where(t => t.NpcSheetId == sheetId && t.Polaridade == trait.Polaridade && !t.IsRacial)
            .Join(db.Traits, nt => nt.TraitId, t => t.Id, (nt, t) => t.Custo)
            .SumAsync();
        if (Math.Abs(existingTotal) + Math.Abs(trait.Custo) > pontosDisponiveis)
            return BadRequest($"Gasto excede os {pontosDisponiveis} pontos de Característica {trait.Polaridade} disponíveis.");

        var npcTrait = new NpcTrait
        {
            Id = Guid.NewGuid(),
            NpcSheetId = sheetId,
            TraitId = traitId,
            Polaridade = trait.Polaridade,
            Especificacao = trait.RequerEspecificacao ? request.Especificacao : null,
        };
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

        var rows = (await db.NpcTraits
            .Where(t => t.NpcSheetId == sheetId)
            .Join(db.Traits, nt => nt.TraitId, t => t.Id, (nt, t) => new { nt, t })
            .ToListAsync())
            .Select(x => ToTraitResponse(x.nt, x.t))
            .ToList();

        var positivas = rows.Where(r => r.Polaridade == "Positiva").ToList();
        var negativas = rows.Where(r => r.Polaridade == "Negativa").ToList();
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        var pontosDisponiveis = TraitPointBudgetCalculator.Compute(sheet!.Nivel, rules.Niveis);
        return new NpcTraitsListResponse(positivas, positivas.Sum(r => r.Custo), negativas, negativas.Sum(r => r.Custo), pontosDisponiveis);
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

    /// <summary>
    /// Whether the sheet's current Variante still has an unresolved racial-characteristic choice.
    /// Mirrors CharacterPossessionsController.PendingRacialTraitChoice.
    /// </summary>
    [HttpGet("racial-traits/pending")]
    public async Task<ActionResult<PendingRacialTraitChoiceResponse>> PendingRacialTraitChoice(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet!.Variante is null)
            return new PendingRacialTraitChoiceResponse(false, [], []);

        var variante = sheet.Variante.Value;
        var over = await db.RacialTraitOverrides.FirstOrDefaultAsync(o => o.GmId == sheet.GmId && o.Variante == variante);
        var slots = RacialTraitOverrideResolver.Resolve(over, variante);

        var existingCount = await db.NpcTraits.CountAsync(t => t.NpcSheetId == sheetId && t.IsRacial && t.RacialVariante == variante);
        var resolved = RacialTraitChoiceResolver.IsResolved(slots, existingCount);

        return new PendingRacialTraitChoiceResponse(!resolved,
            slots.Gratuita.Select(o => new RacialTraitOptionResponse(o.TraitNome, o.Especificacao)).ToList(),
            slots.Obrigatoria.Select(o => new RacialTraitOptionResponse(o.TraitNome, o.Especificacao)).ToList());
    }

    [HttpPost("racial-traits/resolve")]
    public async Task<IActionResult> ResolveRacialTraitChoice(Guid sheetId, ResolveRacialTraitChoiceRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet!.Variante is null)
            return BadRequest("A ficha ainda não tem uma Variante escolhida.");

        var variante = sheet.Variante.Value;
        var over = await db.RacialTraitOverrides.FirstOrDefaultAsync(o => o.GmId == sheet.GmId && o.Variante == variante);
        var slots = RacialTraitOverrideResolver.Resolve(over, variante);

        var resolution = RacialTraitChoiceResolver.Resolve(slots, request.GratuitaTraitNome, request.ObrigatoriaTraitNome);
        if (resolution.Error is not null)
            return BadRequest(resolution.Error);

        foreach (var grant in resolution.Grants!)
        {
            var trait = await db.Traits.FirstOrDefaultAsync(t => t.Nome == grant.TraitNome && !t.IsDeleted);
            if (trait is null)
                return BadRequest($"Característica \"{grant.TraitNome}\" não encontrada no catálogo.");

            db.NpcTraits.Add(new NpcTrait
            {
                Id = Guid.NewGuid(), NpcSheetId = sheetId, TraitId = trait.Id, Polaridade = trait.Polaridade,
                Especificacao = grant.Especificacao, IsRacial = true, RacialVariante = variante,
            });
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> CheckAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        return null;
    }

    private async Task<NpcInventoryItemResponse> ToInventoryItemResponseAsync(NpcInventoryItem inventoryItem)
    {
        var item = await db.Items.SingleAsync(i => i.Id == inventoryItem.ItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        return new NpcInventoryItemResponse(inventoryItem.Id.ToString(), item.Id.ToString(), item.Nome, item.Peso, inventoryItem.Qtd, item.Peso * inventoryItem.Qtd, imageUrl, item.Descricao);
    }

    private async Task<NpcArtifactResponse> ToArtifactResponseAsync(NpcArtifact artifact)
    {
        var item = await db.Set<Artefato>().SingleAsync(a => a.Id == artifact.ArtifactItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        return new NpcArtifactResponse(artifact.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeAlvo?.ToString() ?? string.Empty, item.Alvo ?? string.Empty, item.Valor ?? 0, imageUrl, item.Descricao);
    }

    private async Task<string?> ResolveImageUrlAsync(Guid? imageId)
    {
        if (imageId is null)
            return null;

        var image = await db.Images.FindAsync(imageId.Value);
        return image is not null ? $"/images/{image.Path}" : null;
    }

    private static NpcAffectionResponse ToAffectionResponse(NpcAffection affection) =>
        new(affection.Id.ToString(), affection.Nome, affection.Favorabilidade);

    private static NpcTraitResponse ToTraitResponse(NpcTrait npcTrait, Trait trait) =>
        new(npcTrait.Id.ToString(), trait.Id.ToString(), trait.Nome, trait.Descricao,
            npcTrait.IsRacial ? 0 : trait.Custo, trait.Polaridade.ToString(), npcTrait.Especificacao,
            trait.RequerEspecificacao, npcTrait.IsRacial);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
