using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/npc-sheets")]
[Authorize(Roles = "GM")]
public class NpcSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NpcSheetResponse>> Create()
    {
        var gmId = CurrentGmId();

        var sheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gmId };
        db.NpcSheets.Add(sheet);

        foreach (var atributo in Enum.GetValues<Atributo>())
            db.NpcAttributes.Add(new NpcAttribute { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Atributo = atributo });
        foreach (var pericia in Enum.GetValues<Pericia>())
            db.NpcSkills.Add(new NpcSkill { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Pericia = pericia });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.NpcArmorSlots.Add(new NpcArmorSlot { Id = Guid.NewGuid(), NpcSheetId = sheet.Id, Slot = slot });

        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NpcSheetResponse>> Get(Guid id)
    {
        var sheet = await db.NpcSheets.FirstOrDefaultAsync(s => s.Id == id && s.GmId == CurrentGmId());
        if (sheet is null)
            return NotFound();

        return await ToResponseAsync(sheet);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateNpcSheetRequest request)
    {
        var sheet = await db.NpcSheets.FirstOrDefaultAsync(s => s.Id == id && s.GmId == CurrentGmId());
        if (sheet is null)
            return NotFound();

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

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var sheet = await db.NpcSheets.FirstOrDefaultAsync(s => s.Id == id && s.GmId == CurrentGmId());
        if (sheet is null)
            return NotFound();

        db.NpcSheets.Remove(sheet);
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
    /// throwing. Mirrors CharacterSheetsController's TryParseImageId.
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
    /// Vocacao's C# member names are unaccented (Campeao, Cacador, ...) but IRulesDataProvider.Vocacoes
    /// is parsed straight from the real Tabela de Vocação markdown, which uses the accented Portuguese
    /// names (Campeão, Caçador). A raw .ToString() lookup would never match those two, silently
    /// returning 0 for statusVida/statusFoco. Used ONLY for that lookup — mirrors
    /// CharacterSheetsController.VocacaoTabelaName.
    /// </summary>
    private static string VocacaoTabelaName(Vocacao vocacao) => vocacao switch
    {
        Vocacao.Campeao => "Campeão",
        Vocacao.Cacador => "Caçador",
        _ => vocacao.ToString()
    };

    /// <summary>
    /// Same shape as CharacterSheetsController.ToResponseAsync's Graduação/máximo computation,
    /// but there is no NpcAttribute table yet (a later task adds it) — vigorTotal/astuciaTotal
    /// are hardcoded to 0 rather than queried. A known, temporary approximation.
    /// </summary>
    private async Task<NpcSheetResponse> ToResponseAsync(NpcSheet s)
    {
        string? imageUrl = null;
        if (s.ImageId is not null)
        {
            var image = await db.Images.FindAsync(s.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        var vocacao = s.Vocacao ?? Vocacao.Campeao; // no vocação chosen yet → Graduacao is meaningless but must not throw
        var graduacao = s.Vocacao is null ? 0 : GraduacaoCalculator.Compute(vocacao, s.EAPAtual, s.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);
        var graduacaoLabel = vocacao is Vocacao.Campeao or Vocacao.Cacador ? "Grau" : "Círculo";

        var vigorTotal = 0;
        var astuciaTotal = 0;
        var statusVida = s.Vocacao is not null ? rules.Vocacoes.Where(v => v.Vocacao == VocacaoTabelaName(s.Vocacao.Value) && v.Nivel == s.Nivel).Select(v => v.Vida).FirstOrDefault() : 0;
        var statusFoco = s.Vocacao is not null ? rules.Vocacoes.Where(v => v.Vocacao == VocacaoTabelaName(s.Vocacao.Value) && v.Nivel == s.Nivel).Select(v => v.Arcana).FirstOrDefault() : 0;
        var artefatoBonusParaAdrenalina = 0; // Artefatos com TipoDeAlvo=SubAtributo/Alvo="Adrenalina" — não modelado ainda

        var vitalidadeMaximo = ResourceMaximumCalculator.Vitalidade(vigorTotal, statusVida);
        var focoMaximo = ResourceMaximumCalculator.Foco(astuciaTotal, statusFoco);
        var adrenalinaMaximo = ResourceMaximumCalculator.Adrenalina(artefatoBonusParaAdrenalina);
        var estresseMaximo = ResourceMaximumCalculator.Estresse();

        return new NpcSheetResponse(
            s.Id.ToString(), s.OwnerId?.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade?.ToString(), s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, s.EAPAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, s.PontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos, graduacao, graduacaoLabel,
            vitalidadeMaximo, focoMaximo, adrenalinaMaximo, estresseMaximo);
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
