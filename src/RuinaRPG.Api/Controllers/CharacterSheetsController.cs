using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

// Not route-attributed at the class level (unlike every previous controller in this codebase)
// because its actions live under two different route prefixes:
// api/campaigns/{campaignId}/character-sheets and api/character-sheets/{id}.
[ApiController]
[Authorize]
public class CharacterSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    [HttpPost("api/campaigns/{campaignId}/character-sheets")]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<CharacterSheetResponse>> Create(Guid campaignId, CreateCharacterSheetRequest request)
    {
        var gmId = CurrentUserId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        if (!Guid.TryParse(request.OwnerId, out var ownerId))
            return BadRequest("O jogador informado não é membro desta campanha.");

        var isMember = await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == ownerId);
        if (!isMember)
            return BadRequest("O jogador informado não é membro desta campanha.");

        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaignId, OwnerId = ownerId };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpDelete("api/character-sheets/{id}")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var gmId = CurrentUserId();
        var sheet = await db.CharacterSheets
            .Join(db.Campaigns, s => s.CampaignId, c => c.Id, (s, c) => new { Sheet = s, c.GmId })
            .Where(x => x.Sheet.Id == id && x.GmId == gmId)
            .Select(x => x.Sheet)
            .FirstOrDefaultAsync();
        if (sheet is null)
            return NotFound();

        db.CharacterSheets.Remove(sheet);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("api/campaigns/{campaignId}/character-sheets")]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<List<CharacterSheetResponse>>> ListForCampaign(Guid campaignId)
    {
        var gmId = CurrentUserId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var sheets = await db.CharacterSheets.Where(s => s.CampaignId == campaignId).ToListAsync();
        var responses = new List<CharacterSheetResponse>();
        foreach (var sheet in sheets)
            responses.Add(await ToResponseAsync(sheet));
        return responses;
    }

    [HttpGet("api/character-sheets/{id}")]
    public async Task<ActionResult<CharacterSheetResponse>> Get(Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await ToResponseAsync(sheet);
    }

    [HttpPut("api/character-sheets/{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateCharacterSheetRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (!TryParseEnum<Linhagem>(request.Linhagem, out var linhagem))
            return BadRequest("Linhagem inválida.");
        if (!TryParseEnum<Variante>(request.Variante, out var variante))
            return BadRequest("Variante inválida.");
        if (linhagem is not null && variante is not null && !LinhagemVarianteValidator.IsValidCombination(linhagem.Value, variante.Value))
            return BadRequest("A Variante escolhida não pertence à Linhagem escolhida.");
        if (!TryParseEnum<Vocacao>(request.Vocacao, out var vocacao))
            return BadRequest("Vocação inválida.");
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
        if (!Enum.TryParse<Cobertura>(request.Cobertura, out var cobertura))
            return BadRequest("Cobertura inválida.");

        sheet.ImageId = imageId;
        sheet.Nome = request.Nome;
        sheet.Linhagem = linhagem;
        sheet.Variante = variante;
        sheet.Vocacao = vocacao;
        sheet.SubVocacao = request.SubVocacao;
        sheet.Afinidade = afinidade;
        sheet.Propriedade = request.Propriedade;
        sheet.Nivel = request.Nivel;
        sheet.PossuiCoracaoDeMana = request.PossuiCoracaoDeMana;
        sheet.ExperienciaAtual = request.ExperienciaAtual;
        sheet.EAPAtual = request.EAPAtual;
        sheet.NucleosRankF = request.NucleosRankF;
        sheet.NucleosRankE = request.NucleosRankE;
        sheet.NucleosRankD = request.NucleosRankD;
        sheet.NucleosRankC = request.NucleosRankC;
        sheet.NucleosRankB = request.NucleosRankB;
        sheet.NucleosRankA = request.NucleosRankA;
        sheet.NucleosRankS = request.NucleosRankS;
        sheet.PontosDeIgnicaoAtual = request.PontosDeIgnicaoAtual;
        sheet.PontosDeIgnicaoTotal = request.PontosDeIgnicaoTotal;
        sheet.VitalidadeAtual = request.VitalidadeAtual;
        sheet.FocoAtual = request.FocoAtual;
        sheet.AdrenalinaAtual = request.AdrenalinaAtual;
        sheet.EstresseAtual = request.EstresseAtual;
        sheet.Cobertura = cobertura;
        sheet.Ciclos = request.Ciclos;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("api/character-sheets/{id}/level-up-notice")]
    public async Task<ActionResult<LevelUpNoticeResponse>> LevelUpNotice(Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var pending = LevelUpNoticeCalculator.PendingBonuses(sheet.LastDismissedLevelUpLevel, sheet.Nivel, rules.Niveis);
        return new LevelUpNoticeResponse(pending.Select(b => b.BonusText).ToList());
    }

    [HttpPost("api/character-sheets/{id}/dismiss-level-up-notice")]
    public async Task<IActionResult> DismissLevelUpNotice(Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        sheet.LastDismissedLevelUpLevel = sheet.Nivel;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// A null request value means "not set" and maps to null on the entity. A non-null value
    /// that fails to parse is malformed input, not an absent one — the caller must 400 rather
    /// than silently persisting null (e.g. a garbage Variante silently skipping
    /// LinhagemVarianteValidator).
    /// </summary>
    private static bool TryParseEnum<TEnum>(string? value, out TEnum? parsed) where TEnum : struct, Enum
    {
        if (value is null)
        {
            parsed = null;
            return true;
        }

        if (Enum.TryParse<TEnum>(value, out var result))
        {
            parsed = result;
            return true;
        }

        parsed = null;
        return false;
    }

    /// <summary>
    /// Treats null, empty, or whitespace-only as "no image" (returns true with imageId null).
    /// A non-empty string that isn't a valid Guid is rejected (returns false) rather than
    /// throwing. Mirrors ItemsController's TryParseImageId.
    /// </summary>
    private static bool TryParseImageId(string? raw, out Guid? imageId)
    {
        imageId = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (!Guid.TryParse(raw, out var parsed))
            return false;

        imageId = parsed;
        return true;
    }

    private async Task<CharacterSheetResponse> ToResponseAsync(CharacterSheet s)
    {
        string? imageUrl = null;
        if (s.ImageId is not null)
        {
            var image = await db.Images.FindAsync(s.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        var vocacao = s.Vocacao ?? RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao; // no vocação chosen yet → Graduacao is meaningless but must not throw
        var graduacao = s.Vocacao is null ? 0 : GraduacaoCalculator.Compute(vocacao, s.EAPAtual, s.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);
        var graduacaoLabel = vocacao is RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao or RuinaRPG.Domain.CharacterSheets.Vocacao.Cacador ? "Grau" : "Círculo";

        return new CharacterSheetResponse(
            s.Id.ToString(), s.CampaignId.ToString(), s.OwnerId.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade?.ToString(), s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, s.EAPAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, s.PontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos, graduacao, graduacaoLabel);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
