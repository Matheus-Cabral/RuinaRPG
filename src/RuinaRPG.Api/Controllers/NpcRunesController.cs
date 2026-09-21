using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Runes;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/runes")]
public class NpcRunesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NpcRuneResponse>> Add(Guid sheetId, AddNpcRuneRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var callerId = CurrentUserId();
        if (!GrantedSheetAuthorization.CanEdit(callerId, sheet.OwnerId, sheet.GmId))
            return NotFound();

        var fromScratch = request.Nome is not null && request.Descricao is not null && request.Grau is not null;
        var fromBank = request.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // both or neither
            return BadRequest("Informe exatamente um: Nome, Descricao e Grau para montar do zero, ou SourceBankEntryId.");

        // A NPC sheet has no CampaignId column: the campaign of a granted NPC is resolved through the
        // grant-link CampaignAttachment (same rule NpcSpellAbilitiesController uses). Only a Jogador
        // (caller != GM) ever needs it.
        Guid? campaignId = null;
        if (callerId != sheet.GmId)
        {
            campaignId = await db.CampaignAttachments
                .Where(a => a.NpcSheetId == sheetId && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == sheet.OwnerId))
                .Select(a => (Guid?)a.CampaignId)
                .FirstOrDefaultAsync();
        }

        string nome, descricao;
        int grau;
        Guid? sourceBankEntryId = null;

        if (fromBank)
        {
            if (!Guid.TryParse(request.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            var bankEntry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == bankEntryId && e.GmId == sheet.GmId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            // Um jogador só alcança as entradas anexadas como públicas à campanha da concessão.
            if (callerId != sheet.GmId
                && (campaignId is null
                    || !await db.CampaignAttachments.AnyAsync(a => a.CampaignId == campaignId && a.IsPublic && a.RuneBankEntryId == bankEntryId)))
                return BadRequest("Entrada do banco não encontrada.");

            nome = bankEntry.Nome; descricao = bankEntry.Descricao; grau = bankEntry.Grau;
            sourceBankEntryId = bankEntryId;
        }
        else
        {
            nome = request.Nome!; descricao = request.Descricao!; grau = request.Grau!.Value;
        }

        var rune = new NpcRune { Id = Guid.NewGuid(), NpcSheetId = sheetId, Nome = nome, Descricao = descricao, Grau = grau, SourceBankEntryId = sourceBankEntryId };
        db.NpcRunes.Add(rune);

        var bankCopy = new RuneBankEntry { Id = Guid.NewGuid(), GmId = sheet.GmId, Nome = nome, Descricao = descricao, Grau = grau };
        db.RuneBankEntries.Add(bankCopy);

        if (callerId != sheet.GmId && campaignId is not null)
        {
            db.CampaignAttachments.Add(new CampaignAttachment
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId.Value,
                RuneBankEntryId = bankCopy.Id,
                IsPublic = true
            });
        }

        await db.SaveChangesAsync();
        return Created(string.Empty, ToResponse(rune));
    }

    [HttpGet]
    public async Task<ActionResult<List<NpcRuneResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var runes = await db.NpcRunes.Where(r => r.NpcSheetId == sheetId).ToListAsync();
        return runes.Select(ToResponse).ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var rune = await db.NpcRunes.FirstOrDefaultAsync(r => r.Id == id && r.NpcSheetId == sheetId);
        if (rune is null)
            return NotFound();

        db.NpcRunes.Remove(rune);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static NpcRuneResponse ToResponse(NpcRune r) =>
        new(r.Id.ToString(), r.Nome, r.Descricao, r.Grau);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
