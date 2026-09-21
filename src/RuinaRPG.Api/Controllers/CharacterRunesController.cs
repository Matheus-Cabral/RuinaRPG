using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Runes;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/runes")]
public class CharacterRunesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterRuneResponse>> Add(Guid sheetId, AddCharacterRuneRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        var callerId = CurrentUserId();
        if (!CharacterSheetAuthorization.CanEdit(callerId, sheet.OwnerId, campaignGmId))
            return Forbid();

        var fromScratch = request.Nome is not null && request.Descricao is not null && request.Grau is not null;
        var fromBank = request.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // both or neither
            return BadRequest("Informe exatamente um: Nome, Descricao e Grau para montar do zero, ou SourceBankEntryId.");

        string nome, descricao;
        int grau;
        Guid? sourceBankEntryId = null;
        Guid? imageId = null;

        if (fromBank)
        {
            if (!Guid.TryParse(request.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            var bankEntry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == bankEntryId && e.GmId == campaignGmId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            // O GM escolhe qualquer entrada do próprio banco; um jogador só alcança as que o GM anexou
            // à campanha da ficha como públicas (Requisitos - Banco de Runas R0003/R0008).
            if (callerId != campaignGmId
                && !await db.CampaignAttachments.AnyAsync(a => a.CampaignId == sheet.CampaignId && a.IsPublic && a.RuneBankEntryId == bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            // Ao partir do banco a imagem vem da entrada (Requisitos - Banco de Runas R0005).
            if (!string.IsNullOrWhiteSpace(request.ImageId))
                return BadRequest("Ao escolher do banco, a imagem vem da entrada.");

            nome = bankEntry.Nome; descricao = bankEntry.Descricao; grau = bankEntry.Grau;
            sourceBankEntryId = bankEntryId;
            imageId = bankEntry.ImageId;
        }
        else
        {
            if (!RuneImageAccess.TryParseImageId(request.ImageId, out imageId))
                return BadRequest("ImageId inválido.");

            if (imageId is not null && !await RuneImageAccess.CanUseAsync(db, imageId.Value, callerId, campaignGmId, sheet.CampaignId))
                return BadRequest("Imagem não encontrada.");

            nome = request.Nome!; descricao = request.Descricao!; grau = request.Grau!.Value;
        }

        var rune = new CharacterRune { Id = Guid.NewGuid(), CharacterSheetId = sheetId, Nome = nome, Descricao = descricao, Grau = grau, SourceBankEntryId = sourceBankEntryId, ImageId = imageId };
        db.CharacterRunes.Add(rune);

        // Requisitos - Banco de Runas R0001: toda criação — do zero ou a partir do banco — grava também
        // uma cópia independente no banco do GM, seja o GM ou o jogador quem criou.
        var bankCopy = new RuneBankEntry { Id = Guid.NewGuid(), GmId = campaignGmId, Nome = nome, Descricao = descricao, Grau = grau, ImageId = imageId };
        db.RuneBankEntries.Add(bankCopy);

        // R0007: quando quem cria é o jogador dono (não o GM), a cópia vira anexo público da campanha
        // da ficha (Requisitos - Campanha R0012).
        if (callerId != campaignGmId)
        {
            db.CampaignAttachments.Add(new CampaignAttachment
            {
                Id = Guid.NewGuid(),
                CampaignId = sheet.CampaignId,
                RuneBankEntryId = bankCopy.Id,
                IsPublic = true
            });
        }

        await db.SaveChangesAsync();
        return Created(string.Empty, ToResponse(rune, await RuneImageAccess.UrlsAsync(db, [rune.ImageId])));
    }

    [HttpGet]
    public async Task<ActionResult<List<CharacterRuneResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var runes = await db.CharacterRunes.Where(r => r.CharacterSheetId == sheetId).ToListAsync();
        var urls = await RuneImageAccess.UrlsAsync(db, runes.Select(r => r.ImageId));
        return runes.Select(r => ToResponse(r, urls)).ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var rune = await db.CharacterRunes.FirstOrDefaultAsync(r => r.Id == id && r.CharacterSheetId == sheetId);
        if (rune is null)
            return NotFound();

        db.CharacterRunes.Remove(rune);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CharacterRuneResponse ToResponse(CharacterRune r, Dictionary<Guid, string> imageUrls) =>
        new(r.Id.ToString(), r.Nome, r.Descricao, r.Grau, RuneImageAccess.UrlOf(imageUrls, r.ImageId));

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
