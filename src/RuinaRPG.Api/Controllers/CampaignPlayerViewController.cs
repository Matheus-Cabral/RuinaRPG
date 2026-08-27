using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId}/player-view")]
public class CampaignPlayerViewController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlayerCampaignViewResponse>> Get(Guid campaignId)
    {
        var callerId = CurrentUserId();
        var campaign = await db.Campaigns.FindAsync(campaignId);
        if (campaign is null)
            return NotFound();

        var isMember = campaign.GmId == callerId || await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == callerId);
        if (!isMember)
            return Forbid();

        var minhasFichas = await db.CharacterSheets
            .Where(s => s.CampaignId == campaignId && s.OwnerId == callerId)
            .Select(s => new CharacterSheetSummary(s.Id.ToString(), s.Nome, s.Nivel))
            .ToListAsync();

        var meusNpcs = await db.NpcSheets.Where(s => s.OwnerId == callerId).Select(s => new GrantedSheetSummary(s.Id.ToString(), "Npc", s.Nome)).ToListAsync();
        var meusCriaturas = await db.CreatureSheets.Where(s => s.OwnerId == callerId).Select(s => new GrantedSheetSummary(s.Id.ToString(), "Creature", s.Nome)).ToListAsync();

        var attachments = await db.CampaignAttachments.Where(a => a.CampaignId == campaignId).ToListAsync();
        var anexosPublicos = new List<PublicAttachmentSummary>();

        foreach (var a in attachments.Where(a => a.IsPublic && a.ItemId is not null))
        {
            var item = await db.Items.FindAsync(a.ItemId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "Item", item!.Nome, item.ImageId is not null ? $"/{(await db.Images.FindAsync(item.ImageId.Value))!.Path}" : null));
        }
        foreach (var a in attachments.Where(a => a.IsPublic && a.SpellAbilityBankEntryId is not null))
        {
            var entry = await db.SpellAbilityBankEntries.FindAsync(a.SpellAbilityBankEntryId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "SpellAbilityBankEntry", entry!.Nome, null));
        }
        foreach (var a in attachments.Where(a => a.IsPublic && a.ImageId is not null))
        {
            var image = await db.Images.FindAsync(a.ImageId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "Image", null, $"/{image!.Path}"));
        }
        foreach (var a in attachments.Where(a => a.NpcSheetId is not null && (a.NpcNomePublico || a.NpcImagemPublica)))
        {
            var npc = await db.NpcSheets.FindAsync(a.NpcSheetId!.Value);
            var nome = a.NpcNomePublico ? npc!.Nome : null;
            var imageUrl = a.NpcImagemPublica && npc!.ImageId is not null ? $"/{(await db.Images.FindAsync(npc.ImageId.Value))!.Path}" : null;
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "NpcSheet", nome, imageUrl));
        }
        foreach (var a in attachments.Where(a => a.CreatureSheetId is not null && (a.CreatureNomePublico || a.CreatureImagemPublica)))
        {
            var creature = await db.CreatureSheets.FindAsync(a.CreatureSheetId!.Value);
            var nome = a.CreatureNomePublico ? creature!.Nome : null;
            var imageUrl = a.CreatureImagemPublica && creature!.ImageId is not null ? $"/{(await db.Images.FindAsync(creature.ImageId.Value))!.Path}" : null;
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "CreatureSheet", nome, imageUrl));
        }

        return new PlayerCampaignViewResponse(minhasFichas, meusNpcs.Concat(meusCriaturas).ToList(), anexosPublicos);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
