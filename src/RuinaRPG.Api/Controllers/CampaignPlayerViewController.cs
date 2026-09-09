using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId}/player-view")]
public class CampaignPlayerViewController(RuinaRpgDbContext db) : ControllerBase
{
    /// <summary>
    /// R0009's own precondition: a player has no way to discover which campaign(s) they belong to
    /// — GET api/campaigns is GM-only (lists campaigns the caller GMs, a different thing). This is
    /// the "mine" a player actually needs, so it lives here alongside the rest of the player-facing
    /// reads rather than in CampaignsController (which stays entirely GM-only).
    /// </summary>
    [HttpGet("~/api/campaigns/mine")]
    public async Task<ActionResult<List<CampaignResponse>>> ListMine()
    {
        var callerId = CurrentUserId();
        var campaigns = await db.CampaignMembers
            .Where(m => m.UserId == callerId)
            .Join(db.Campaigns, m => m.CampaignId, c => c.Id, (m, c) => c)
            .ToListAsync();

        var responses = new List<CampaignResponse>();
        foreach (var campaign in campaigns)
            responses.Add(await ToResponseAsync(campaign));
        return responses;
    }

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

        var minhasFichasEntities = await db.CharacterSheets
            .Where(s => s.CampaignId == campaignId && s.OwnerId == callerId)
            .ToListAsync();
        var minhasFichas = new List<CharacterSheetSummary>();
        foreach (var s in minhasFichasEntities)
            minhasFichas.Add(new CharacterSheetSummary(s.Id.ToString(), s.Nome, s.Nivel, await ResolveImageUrlAsync(s.ImageId)));

        var meusNpcsEntities = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.NpcSheetId != null)
            .Join(db.NpcSheets, a => a.NpcSheetId!.Value, s => s.Id, (a, s) => s)
            .Where(s => s.OwnerId == callerId)
            .Distinct()
            .ToListAsync();
        var meusCriaturasEntities = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.CreatureSheetId != null)
            .Join(db.CreatureSheets, a => a.CreatureSheetId!.Value, s => s.Id, (a, s) => s)
            .Where(s => s.OwnerId == callerId)
            .Distinct()
            .ToListAsync();

        var meusNpcs = new List<GrantedSheetSummary>();
        foreach (var s in meusNpcsEntities)
            meusNpcs.Add(new GrantedSheetSummary(s.Id.ToString(), "Npc", s.Nome, await ResolveImageUrlAsync(s.ImageId)));
        var meusCriaturas = new List<GrantedSheetSummary>();
        foreach (var s in meusCriaturasEntities)
            meusCriaturas.Add(new GrantedSheetSummary(s.Id.ToString(), "Creature", s.Nome, await ResolveImageUrlAsync(s.ImageId)));

        var attachments = await db.CampaignAttachments.Where(a => a.CampaignId == campaignId).ToListAsync();
        var anexosPublicos = new List<PublicAttachmentSummary>();

        foreach (var a in attachments.Where(a => a.IsPublic && a.ItemId is not null))
        {
            var item = await db.Items.FindAsync(a.ItemId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "Item", item!.Nome, item.ImageId is not null ? $"/images/{(await db.Images.FindAsync(item.ImageId.Value))!.Path}" : null));
        }
        foreach (var a in attachments.Where(a => a.IsPublic && a.SpellAbilityBankEntryId is not null))
        {
            var entry = await db.SpellAbilityBankEntries.FindAsync(a.SpellAbilityBankEntryId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "SpellAbilityBankEntry", entry!.Nome, null));
        }
        foreach (var a in attachments.Where(a => a.IsPublic && a.ImageId is not null))
        {
            var image = await db.Images.FindAsync(a.ImageId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "Image", null, $"/images/{image!.Path}"));
        }
        foreach (var a in attachments.Where(a => a.NpcSheetId is not null && (a.NpcNomePublico || a.NpcImagemPublica)))
        {
            var npc = await db.NpcSheets.FindAsync(a.NpcSheetId!.Value);
            var nome = a.NpcNomePublico ? npc!.Nome : null;
            var imageUrl = a.NpcImagemPublica && npc!.ImageId is not null ? $"/images/{(await db.Images.FindAsync(npc.ImageId.Value))!.Path}" : null;
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "NpcSheet", nome, imageUrl));
        }
        foreach (var a in attachments.Where(a => a.CreatureSheetId is not null && (a.CreatureNomePublico || a.CreatureImagemPublica)))
        {
            var creature = await db.CreatureSheets.FindAsync(a.CreatureSheetId!.Value);
            var nome = a.CreatureNomePublico ? creature!.Nome : null;
            var imageUrl = a.CreatureImagemPublica && creature!.ImageId is not null ? $"/images/{(await db.Images.FindAsync(creature.ImageId.Value))!.Path}" : null;
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "CreatureSheet", nome, imageUrl));
        }

        return new PlayerCampaignViewResponse(minhasFichas, meusNpcs.Concat(meusCriaturas).ToList(), anexosPublicos);
    }

    [HttpGet("~/api/campaigns/{campaignId}/secret-notes")]
    public async Task<ActionResult<List<SecretNoteResponse>>> ListSecretNotes(Guid campaignId)
    {
        var callerId = CurrentUserId();
        var campaign = await db.Campaigns.FindAsync(campaignId);
        if (campaign is null)
            return NotFound();

        var isGm = campaign.GmId == callerId;
        var isMember = isGm || await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == callerId);
        if (!isMember)
            return Forbid();

        var notes = await db.DiaryEntries.Where(d => d.CampaignId == campaignId && d.IsSecretNote).ToListAsync();
        var results = new List<SecretNoteResponse>();
        foreach (var note in notes)
        {
            var recipientIds = await db.DiaryEntryRecipients.Where(r => r.DiaryEntryId == note.Id).Select(r => r.UserId).ToListAsync();
            if (!isGm && !recipientIds.Contains(callerId))
                continue; // R0011: a non-recipient sees nothing, not even that the note exists

            var imageIds = await db.DiaryEntryImages.Where(i => i.DiaryEntryId == note.Id).Select(i => i.ImageId).ToListAsync();
            var images = await db.Images.Where(i => imageIds.Contains(i.Id)).ToListAsync();
            results.Add(new SecretNoteResponse(note.Id.ToString(), note.Texto, note.CreatedAt, recipientIds.Select(id => id.ToString()).ToList(), images.Select(i => $"/images/{i.Path}").ToList()));
        }
        return results;
    }

    private async Task<CampaignResponse> ToResponseAsync(Campaign c)
    {
        string? imageUrl = null;
        if (c.ImageId is not null)
        {
            var image = await db.Images.FindAsync(c.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        return new CampaignResponse(c.Id.ToString(), c.Nome, c.Descricao, imageUrl);
    }

    private async Task<string?> ResolveImageUrlAsync(Guid? imageId)
    {
        if (imageId is null)
            return null;

        var image = await db.Images.FindAsync(imageId.Value);
        return image is not null ? $"/images/{image.Path}" : null;
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
