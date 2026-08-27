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

        var attachment = new CampaignAttachment
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            ItemId = request.ItemId is not null ? Guid.Parse(request.ItemId) : null,
            NpcSheetId = request.NpcSheetId is not null ? Guid.Parse(request.NpcSheetId) : null,
            CreatureSheetId = request.CreatureSheetId is not null ? Guid.Parse(request.CreatureSheetId) : null,
            SpellAbilityBankEntryId = request.SpellAbilityBankEntryId is not null ? Guid.Parse(request.SpellAbilityBankEntryId) : null,
            ImageId = request.ImageId is not null ? Guid.Parse(request.ImageId) : null,
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
            responses.Add(await ToResponseAsync(attachment));
        return responses;
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
            return new CampaignAttachmentResponse(a.Id.ToString(), "Item", item!.Nome, a.IsPublic, null, null, null, null);
        }
        if (a.SpellAbilityBankEntryId is not null)
        {
            var entry = await db.SpellAbilityBankEntries.FindAsync(a.SpellAbilityBankEntryId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "SpellAbilityBankEntry", entry!.Nome, a.IsPublic, null, null, null, null);
        }
        if (a.ImageId is not null)
        {
            var image = await db.Images.FindAsync(a.ImageId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "Image", image!.Path, a.IsPublic, null, null, null, null);
        }
        if (a.NpcSheetId is not null)
        {
            var npc = await db.NpcSheets.FindAsync(a.NpcSheetId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "NpcSheet", npc!.Nome ?? "", null, a.NpcNomePublico, a.NpcImagemPublica, null, null);
        }
        var creature = await db.CreatureSheets.FindAsync(a.CreatureSheetId!.Value);
        return new CampaignAttachmentResponse(a.Id.ToString(), "CreatureSheet", creature!.Nome ?? "", null, null, null, a.CreatureNomePublico, a.CreatureImagemPublica);
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
