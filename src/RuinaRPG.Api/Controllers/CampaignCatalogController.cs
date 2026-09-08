using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Requisitos - Ficha de Personagem R0003 / Requisitos - Catálogo R0011: a Jogador editing their
/// own sheet (or a NPC/Criatura sheet granted to them) may only pick an Item, Magia/Habilidade or
/// Imagem that's attached to that sheet's campaign AND toggled public (Requisitos - Campanha
/// R0008/R0009) — not their linked GM's entire catalog/bank/image library. This controller is
/// that scoped read surface; it returns the exact same response contracts the GM-facing
/// ItemsController/SpellAbilityBankController/ImagesController already use, just filtered through
/// CampaignAttachments instead of GmId/UploadedByUserId. Membership-checked the same way as
/// CampaignPlayerViewController.Get — a GM can call these too (they just see their own campaign's
/// public subset), which is harmless and kept for symmetry.
/// </summary>
[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId}")]
public class CampaignCatalogController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet("available-items")]
    public async Task<ActionResult<List<ItemResponse>>> AvailableItems(Guid campaignId, [FromQuery] string? nome, [FromQuery] string? tipo)
    {
        if (await MembershipErrorAsync(campaignId) is { } error)
            return error;

        var publicItemIds = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.IsPublic && a.ItemId != null)
            .Select(a => a.ItemId!.Value)
            .ToListAsync();

        var query = db.Items.Where(i => publicItemIds.Contains(i.Id));
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(i => EF.Functions.ILike(i.Nome, $"%{nome}%"));
        if (tipo is not null && Enum.TryParse<ItemTipo>(tipo, out var tipoParsed))
            query = query.Where(i => EF.Property<string>(i, "Tipo") == tipoParsed.ToString());

        var items = await query.ToListAsync();
        var responses = new List<ItemResponse>();
        foreach (var item in items)
            responses.Add(await ToItemResponseAsync(item));
        return responses;
    }

    [HttpGet("available-spell-abilities")]
    public async Task<ActionResult<List<SpellAbilityEntryResponse>>> AvailableSpellAbilities(Guid campaignId, [FromQuery] string? nome)
    {
        if (await MembershipErrorAsync(campaignId) is { } error)
            return error;

        var publicEntryIds = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.IsPublic && a.SpellAbilityBankEntryId != null)
            .Select(a => a.SpellAbilityBankEntryId!.Value)
            .ToListAsync();

        var query = db.SpellAbilityBankEntries.Include(e => e.Efeitos).Where(e => publicEntryIds.Contains(e.Id));
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(e => EF.Functions.ILike(e.Nome, $"%{nome}%"));

        var entries = await query.ToListAsync();
        return entries.Select(ToSpellAbilityResponse).ToList();
    }

    [HttpGet("available-images")]
    public async Task<ActionResult<List<ImageSummaryResponse>>> AvailableImages(Guid campaignId)
    {
        if (await MembershipErrorAsync(campaignId) is { } error)
            return error;

        var publicImageIds = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.IsPublic && a.ImageId != null)
            .Select(a => a.ImageId!.Value)
            .ToListAsync();

        return await db.Images
            .Where(i => publicImageIds.Contains(i.Id))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ImageSummaryResponse(i.Id.ToString(), $"/images/{i.Path}", i.CreatedAt))
            .ToListAsync();
    }

    /// <summary>Null when the caller may proceed; otherwise the ActionResult to return as-is
    /// (404 if the campaign doesn't exist, 403 if the caller isn't the GM or a member).</summary>
    private async Task<ActionResult?> MembershipErrorAsync(Guid campaignId)
    {
        var campaign = await db.Campaigns.FindAsync(campaignId);
        if (campaign is null)
            return NotFound();

        var callerId = CurrentUserId();
        var isMember = campaign.GmId == callerId || await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == callerId);
        return isMember ? null : Forbid();
    }

    // Mirrors ItemsController.ToResponseAsync exactly (see its comment for why this isn't
    // factored into a shared helper — every controller in this codebase owns its own mapping).
    private async Task<ItemResponse> ToItemResponseAsync(Item item)
    {
        string? imageUrl = null;
        if (item.ImageId is not null)
        {
            var image = await db.Images.FindAsync(item.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        return item switch
        {
            ItemGeral g => new ItemResponse(g.Id.ToString(), "ItemGeral", g.Nome, g.Peso, g.Preco, imageUrl,
                g.Subcategoria, g.Descricao, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, g.CapacidadeExtra),
            Arma a => new ItemResponse(a.Id.ToString(), "Arma", a.Nome, a.Peso, a.Preco, imageUrl,
                a.Subcategoria, null, a.Tier?.ToString(), a.Empunhadura?.ToString(), a.Dados, a.Dano, a.Critico, a.Alcance, a.TipoDeDano?.ToString(), a.RequisitoAtributo,
                a.DurabilidadeMaxima, null, null, null, null, null, null, null, null, null, null, null),
            Armadura ar => new ItemResponse(ar.Id.ToString(), "Armadura", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, ar.DurabilidadeMaxima,
                ar.Categoria?.ToString(), ar.Defesa, ar.RF, ar.RM, ar.Penalidade, ar.RequisitoVigor, null, null, null, null, null),
            Escudo e => new ItemResponse(e.Id.ToString(), "Escudo", e.Nome, e.Peso, e.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, e.DurabilidadeMaxima,
                e.Categoria?.ToString(), null, null, null, e.Penalidade, e.RequisitoVigor, e.BonusDefesa, null, null, null, null),
            Artefato ar => new ItemResponse(ar.Id.ToString(), "Artefato", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, ar.TipoDeAlvo?.ToString(), ar.Alvo, ar.Valor, null),
            _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
        };
    }

    private static SpellAbilityEntryResponse ToSpellAbilityResponse(RuinaRPG.Infrastructure.SpellsAndAbilities.SpellAbilityBankEntry entry) => new(
        entry.Id.ToString(), entry.Nome, entry.Tipo.ToString(), entry.Grau, entry.GastoEmPI, entry.Custo, entry.Descricao,
        entry.Efeitos.Select(e => new SpellAbilityEffectResponse(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList());

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
