using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/creature-sheets")]
[Authorize(Roles = "GM")]
public class CreatureSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreatureSheetResponse>> Create()
    {
        var gmId = CurrentGmId();

        var sheet = new CreatureSheet { Id = Guid.NewGuid(), GmId = gmId };
        db.CreatureSheets.Add(sheet);

        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CreatureSheetResponse>> Get(Guid id)
    {
        var sheet = await db.CreatureSheets.FirstOrDefaultAsync(s => s.Id == id && s.GmId == CurrentGmId());
        if (sheet is null)
            return NotFound();

        return await ToResponseAsync(sheet);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateCreatureSheetRequest request)
    {
        var sheet = await db.CreatureSheets.FirstOrDefaultAsync(s => s.Id == id && s.GmId == CurrentGmId());
        if (sheet is null)
            return NotFound();

        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (!TryParseEnum<Arquetipo>(request.Arquetipo, out var arquetipo))
            return BadRequest("Arquetipo inválido.");
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
        if (!TryParseEnum<Rank>(request.Rank, out var rank))
            return BadRequest("Rank inválido.");
        if (!Enum.TryParse<Cobertura>(request.Cobertura, out var cobertura))
            return BadRequest("Cobertura inválida.");

        sheet.ImageId = imageId;
        sheet.Nome = request.Nome;
        sheet.Raca = request.Raca;
        sheet.Arquetipo = arquetipo;
        sheet.SubArquetipo = request.SubArquetipo;
        sheet.Afinidade = afinidade;
        sheet.Propriedade = request.Propriedade;
        sheet.Rank = rank;
        sheet.Nivel = request.Nivel;
        sheet.ExperienciaAtual = request.ExperienciaAtual;
        sheet.PontosDeIgnicao = request.PontosDeIgnicao;
        sheet.VitalidadeAtual = request.VitalidadeAtual;
        sheet.FocoAtual = request.FocoAtual;
        sheet.AdrenalinaAtual = request.AdrenalinaAtual;
        sheet.Cobertura = cobertura;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var sheet = await db.CreatureSheets.FirstOrDefaultAsync(s => s.Id == id && s.GmId == CurrentGmId());
        if (sheet is null)
            return NotFound();

        db.CreatureSheets.Remove(sheet);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// A null request value means "not set" and maps to null on the entity. A non-null value
    /// that fails to parse is malformed input, not an absent one — the caller must 400 rather
    /// than silently persisting null. Mirrors NpcSheetsController.TryParseEnum.
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
    /// throwing. Mirrors NpcSheetsController.TryParseImageId.
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

    /// <summary>
    /// Kill/Assistencia are computed live via XpAwardCalculator, never persisted — same pattern
    /// as CharacterSheetResponse.Graduacao/NpcSheetResponse.Graduacao.
    ///
    /// VitalidadeMaximo/FocoMaximo/AdrenalinaMaximo mirror NpcSheetsController.ToResponseAsync's
    /// máximo computation, via ResourceMaximumCalculator and IRulesDataProvider.Arquetipos (looked
    /// up by Arquetipo/Nivel — unlike Vocacao, Arquetipo's C# enum member names (Fisico/Arcano)
    /// match the real Tabela de Arquetipos.md's labels exactly, so a plain .ToString() lookup is
    /// correct here — no accent-mapping helper needed).
    ///
    /// vigorTotal/astuciaTotal are temporarily hardcoded to 0: CreatureAttribute doesn't exist
    /// until Task 3, so there is no Vigor/Astúcia total to compute yet. A later task wires the
    /// real computation in (mirrors NpcSheetsController's equivalent gap when NpcAttribute didn't
    /// exist yet).
    /// </summary>
    private async Task<CreatureSheetResponse> ToResponseAsync(CreatureSheet s)
    {
        string? imageUrl = null;
        if (s.ImageId is not null)
        {
            var image = await db.Images.FindAsync(s.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        var kill = XpAwardCalculator.Kill(s.ExperienciaAtual);
        var assistencia = XpAwardCalculator.Assistencia(s.ExperienciaAtual);

        var vigorTotal = 0; // TODO(Task 3): CreatureAttribute doesn't exist yet — real Vigor total pending.
        var astuciaTotal = 0; // TODO(Task 3): CreatureAttribute doesn't exist yet — real Astúcia total pending.
        var statusVida = s.Arquetipo is not null ? rules.Arquetipos.Where(v => v.Arquetipo == s.Arquetipo.Value.ToString() && v.Nivel == s.Nivel).Select(v => v.Vida).FirstOrDefault() : 0;
        var statusFoco = s.Arquetipo is not null ? rules.Arquetipos.Where(v => v.Arquetipo == s.Arquetipo.Value.ToString() && v.Nivel == s.Nivel).Select(v => v.Arcana).FirstOrDefault() : 0;
        var artefatoBonusParaAdrenalina = 0; // Artefatos com TipoDeAlvo=SubAtributo/Alvo="Adrenalina" — não modelado ainda

        var vitalidadeMaximo = ResourceMaximumCalculator.Vitalidade(vigorTotal, statusVida);
        var focoMaximo = ResourceMaximumCalculator.Foco(astuciaTotal, statusFoco);
        var adrenalinaMaximo = ResourceMaximumCalculator.Adrenalina(artefatoBonusParaAdrenalina);

        return new CreatureSheetResponse(
            s.Id.ToString(), s.OwnerId?.ToString(), imageUrl,
            s.Nome, s.Raca, s.Arquetipo?.ToString(), s.SubArquetipo, s.Afinidade?.ToString(), s.Propriedade,
            s.Rank?.ToString(), s.Nivel, s.ExperienciaAtual, kill, assistencia,
            s.PontosDeIgnicao, s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.Cobertura.ToString(),
            vitalidadeMaximo, focoMaximo, adrenalinaMaximo);
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
