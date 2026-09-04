using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Domain.Campaigns;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/campaigns/{campaignId}/attachments")]
public class CampaignAttachmentsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CampaignAttachmentResponse>> Attach(Guid campaignId, AttachToCampaignRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        if (!CampaignAttachmentTargetValidator.ExactlyOneSet(request.ItemId, request.NpcSheetId, request.CreatureSheetId, request.SpellAbilityBankEntryId, request.ImageId))
            return BadRequest("Informe exatamente um alvo para o anexo.");

        Guid? itemId = null, npcSheetId = null, creatureSheetId = null, bankEntryId = null, imageId = null;

        if (request.ItemId is not null)
        {
            if (!Guid.TryParse(request.ItemId, out var parsed))
                return BadRequest("ItemId inválido.");
            if (!await db.Items.AnyAsync(i => i.Id == parsed && i.GmId == gmId))
                return BadRequest("Item não encontrado.");
            itemId = parsed;
        }
        else if (request.NpcSheetId is not null)
        {
            if (!Guid.TryParse(request.NpcSheetId, out var parsed))
                return BadRequest("NpcSheetId inválido.");
            if (!await db.NpcSheets.AnyAsync(n => n.Id == parsed && n.GmId == gmId))
                return BadRequest("Ficha de NPC não encontrada.");
            npcSheetId = parsed;
        }
        else if (request.CreatureSheetId is not null)
        {
            if (!Guid.TryParse(request.CreatureSheetId, out var parsed))
                return BadRequest("CreatureSheetId inválido.");
            if (!await db.CreatureSheets.AnyAsync(c => c.Id == parsed && c.GmId == gmId))
                return BadRequest("Ficha de Criatura não encontrada.");
            creatureSheetId = parsed;
        }
        else if (request.SpellAbilityBankEntryId is not null)
        {
            if (!Guid.TryParse(request.SpellAbilityBankEntryId, out var parsed))
                return BadRequest("SpellAbilityBankEntryId inválido.");
            if (!await db.SpellAbilityBankEntries.AnyAsync(e => e.Id == parsed && e.GmId == gmId))
                return BadRequest("Entrada do Banco de Magias não encontrada.");
            bankEntryId = parsed;
        }
        else
        {
            if (!Guid.TryParse(request.ImageId, out var parsed))
                return BadRequest("ImageId inválido.");
            if (!await db.Images.AnyAsync(img => img.Id == parsed && img.UploadedByUserId == gmId))
                return BadRequest("Imagem não encontrada.");
            imageId = parsed;
        }

        var attachment = new CampaignAttachment
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            ItemId = itemId,
            NpcSheetId = npcSheetId,
            CreatureSheetId = creatureSheetId,
            SpellAbilityBankEntryId = bankEntryId,
            ImageId = imageId,
            IsPublic = false, NpcNomePublico = false, NpcImagemPublica = false, CreatureNomePublico = false, CreatureImagemPublica = false
        };
        db.CampaignAttachments.Add(attachment);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(attachment));
    }

    [HttpGet]
    public async Task<ActionResult<List<CampaignAttachmentResponse>>> List(Guid campaignId)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var attachments = await db.CampaignAttachments.Where(a => a.CampaignId == campaignId).ToListAsync();
        var responses = new List<CampaignAttachmentResponse>();
        foreach (var attachment in attachments)
        {
            if (await IsGrantLinkAsync(attachment))
                continue;
            responses.Add(await ToResponseAsync(attachment));
        }
        return responses;
    }

    /// <summary>
    /// Task 6's grant flow inserts a CampaignAttachment row purely to link a granted NPC/Creature
    /// sheet to its campaign, with all visibility booleans false — indistinguishable from a genuine
    /// GM-authored display attachment by any of its own fields. Granted sheets always have OwnerId
    /// set (the player who received them); sheets still in the GM's own registry never do. That
    /// distinguishes a grant-link row without a schema change, so it can be hidden from the GM's
    /// Anexos list (deleting it there would silently break the player's companion link).
    /// </summary>
    private async Task<bool> IsGrantLinkAsync(CampaignAttachment a)
    {
        if (a.NpcSheetId is not null)
        {
            var npc = await db.NpcSheets.FindAsync(a.NpcSheetId.Value);
            return npc?.OwnerId is not null;
        }
        if (a.CreatureSheetId is not null)
        {
            var creature = await db.CreatureSheets.FindAsync(a.CreatureSheetId.Value);
            return creature?.OwnerId is not null;
        }
        return false;
    }

    [HttpPut("{id}/visibility")]
    public async Task<IActionResult> ToggleVisibility(Guid campaignId, Guid id, [FromBody] bool isPublic)
    {
        var attachment = await FindOwnedAttachmentAsync(campaignId, id);
        if (attachment is null)
            return NotFound();

        if (attachment.NpcSheetId is not null || attachment.CreatureSheetId is not null)
            return BadRequest("Anexos de NPC/Criatura usam os toggles de Nome/Imagem, não este endpoint.");

        attachment.IsPublic = isPublic;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/npc-visibility")]
    public async Task<IActionResult> ToggleNpcVisibility(Guid campaignId, Guid id, [FromBody] NpcVisibilityRequest request)
    {
        var attachment = await FindOwnedAttachmentAsync(campaignId, id);
        if (attachment is null)
            return NotFound();
        if (attachment.NpcSheetId is null)
            return BadRequest("Este anexo não é uma Ficha de NPC.");

        attachment.NpcNomePublico = request.NomePublico;
        attachment.NpcImagemPublica = request.ImagemPublica;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/creature-visibility")]
    public async Task<IActionResult> ToggleCreatureVisibility(Guid campaignId, Guid id, [FromBody] CreatureVisibilityRequest request)
    {
        var attachment = await FindOwnedAttachmentAsync(campaignId, id);
        if (attachment is null)
            return NotFound();
        if (attachment.CreatureSheetId is null)
            return BadRequest("Este anexo não é uma Ficha de Criatura.");

        attachment.CreatureNomePublico = request.NomePublico;
        attachment.CreatureImagemPublica = request.ImagemPublica;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(Guid campaignId, Guid id)
    {
        var attachment = await FindOwnedAttachmentAsync(campaignId, id);
        if (attachment is null)
            return NotFound();

        db.CampaignAttachments.Remove(attachment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<CampaignAttachment?> FindOwnedAttachmentAsync(Guid campaignId, Guid id)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return null;

        return await db.CampaignAttachments.FirstOrDefaultAsync(a => a.Id == id && a.CampaignId == campaignId);
    }

    private async Task<CampaignAttachmentResponse> ToResponseAsync(CampaignAttachment a)
    {
        if (a.ItemId is not null)
        {
            var item = await db.Items.FindAsync(a.ItemId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "Item", item!.Nome, a.IsPublic, null, null, null, null, await ImageUrlAsync(item.ImageId));
        }
        if (a.SpellAbilityBankEntryId is not null)
        {
            var entry = await db.SpellAbilityBankEntries.FindAsync(a.SpellAbilityBankEntryId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "SpellAbilityBankEntry", entry!.Nome, a.IsPublic, null, null, null, null, null);
        }
        if (a.ImageId is not null)
        {
            var image = await db.Images.FindAsync(a.ImageId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "Image", image!.Path, a.IsPublic, null, null, null, null, $"/images/{image.Path}");
        }
        if (a.NpcSheetId is not null)
        {
            var npc = await db.NpcSheets.FindAsync(a.NpcSheetId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "NpcSheet", npc!.Nome ?? "", null, a.NpcNomePublico, a.NpcImagemPublica, null, null, await ImageUrlAsync(npc.ImageId));
        }
        var creature = await db.CreatureSheets.FindAsync(a.CreatureSheetId!.Value);
        return new CampaignAttachmentResponse(a.Id.ToString(), "CreatureSheet", creature!.Nome ?? "", null, null, null, a.CreatureNomePublico, a.CreatureImagemPublica, await ImageUrlAsync(creature.ImageId));
    }

    private async Task<string?> ImageUrlAsync(Guid? imageId)
    {
        if (imageId is null)
            return null;
        var image = await db.Images.FindAsync(imageId.Value);
        return image is not null ? $"/images/{image.Path}" : null;
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}

public record NpcVisibilityRequest(bool NomePublico, bool ImagemPublica);
public record CreatureVisibilityRequest(bool NomePublico, bool ImagemPublica);
