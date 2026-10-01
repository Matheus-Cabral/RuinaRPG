using RuinaRPG.Domain.Rules.ReferenceData;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/creature-sheets")]
[Authorize]
public class CreatureSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules, ITabelaDeNiveis tabelaDeNiveis, IHubContext<EncounterHub> hub, ILogger<CreatureSheetsController> logger, CreatureSheetStats stats, IPericiaCatalogo pericias) : ControllerBase
{
    // Creating a fresh (un-granted) Creature is GM roster curation, not something a player who's
    // been granted one already does — same reasoning as List below.
    [HttpPost]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<CreatureSheetResponse>> Create()
    {
        var gmId = CurrentUserId();

        var sheet = new CreatureSheet { Id = Guid.NewGuid(), GmId = gmId };
        db.CreatureSheets.Add(sheet);

        foreach (var atributo in Enum.GetValues<AtributoCriatura>())
            db.CreatureAttributes.Add(new CreatureAttribute { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Atributo = atributo });
        // R0005's "lista fixa mais curta" — only the Perícias flagged DisponivelParaCriaturas, not
        // every active one (unlike Ficha de NPCs). The suggested Atributo only applies when the
        // Criatura has it (AtributoCriatura is a shorter enum).
        foreach (var pericia in (await pericias.TodasAsync()).Where(p => !p.IsDeleted && p.DisponivelParaCriaturas))
            db.CreatureSkills.Add(new CreatureSkill { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, PericiaId = pericia.Id, AtributoEscolhido = Enum.TryParse<AtributoCriatura>(pericia.AtributoSugerido?.ToString(), out var sugerido) ? sugerido : null });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.CreatureArmorSlots.Add(new CreatureArmorSlot { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Slot = slot });

        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CreatureSheetResponse>> Get(Guid id)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await ToResponseAsync(sheet);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateCreatureSheetRequest request)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var tabela = await tabelaDeNiveis.ObterAsync();
        if (request.Nivel < 1 || request.Nivel > tabela.UltimoNivel)
            return BadRequest($"O nível deve estar entre 1 e {tabela.UltimoNivel}.");

        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (!TryParseEnum<Arquetipo>(request.Arquetipo, out var arquetipo))
            return BadRequest("Arquetipo inválido.");
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
        if (afinidade is not null && afinidade != sheet.Afinidade && CaminhoSubElementoRules.EhCaminho(afinidade.Value))
            return BadRequest("Alma e Vida são Caminhos, não Afinidades.");
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
        sheet.Rank = rank;
        sheet.Nivel = request.Nivel;
        sheet.ExperienciaAtual = request.ExperienciaAtual;
        sheet.PontosDeIgnicao = request.PontosDeIgnicao;
        sheet.VitalidadeAtual = request.VitalidadeAtual;
        sheet.FocoAtual = request.FocoAtual;
        sheet.AdrenalinaAtual = request.AdrenalinaAtual;
        sheet.Cobertura = cobertura;

        await db.SaveChangesAsync();
        await NotifyAffectedEncountersAsync(id);

        return NoContent();
    }

    // Criaturas (unlike Ficha de Personagem, where Nível is calculated-only from Experiência
    // Atual) keep Nível directly GM-editable — but the two fields still need to land on the same
    // pair the XP-threshold table would derive from either alone (Requisitos - Ficha de
    // Criaturas). The generic Update above can't apply that rule itself: it always receives both
    // fields in one full-form submission, with no reliable way to tell "the GM just edited Nível"
    // apart from "Nível simply wasn't touched and still reads its old value" (a resend that
    // happens to equal the old value either way). These two endpoints exist so the client can say
    // which field it actually means, by calling one or the other — see FichaDeCriatura.razor's
    // Nível/Experiência Atual fields.
    [HttpPut("{id}/nivel")]
    public async Task<IActionResult> UpdateNivel(Guid id, [FromBody] int nivel)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var tabela = await tabelaDeNiveis.ObterAsync();
        if (nivel < 1 || nivel > tabela.UltimoNivel)
            return BadRequest($"O nível deve estar entre 1 e {tabela.UltimoNivel}.");

        sheet.Nivel = nivel;
        sheet.ExperienciaAtual = NivelCalculator.MinXpParaNivel(nivel, tabela.ComoXpPorNivel());
        await db.SaveChangesAsync();
        await NotifyAffectedEncountersAsync(id);

        return NoContent();
    }

    [HttpPut("{id}/experiencia-atual")]
    public async Task<IActionResult> UpdateExperienciaAtual(Guid id, [FromBody] int experienciaAtual)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        sheet.ExperienciaAtual = experienciaAtual;
        var tabela = await tabelaDeNiveis.ObterAsync();
        sheet.Nivel = NivelCalculator.Compute(experienciaAtual, tabela.ComoXpPorNivel());
        await db.SaveChangesAsync();
        await NotifyAffectedEncountersAsync(id);

        return NoContent();
    }

    [HttpGet("{id}/level-up-notice")]
    public async Task<ActionResult<LevelUpNoticeResponse>> LevelUpNotice(Guid id)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var tabela = await tabelaDeNiveis.ObterAsync();
        var pending = LevelUpNoticeCalculator.PendingBonuses(sheet.LastDismissedLevelUpLevel, sheet.Nivel, tabela.ComoLevelBonus());
        return new LevelUpNoticeResponse(LevelUpNoticeCalculator.FlattenBonusLines(pending));
    }

    [HttpPost("{id}/dismiss-level-up-notice")]
    public async Task<IActionResult> DismissLevelUpNotice(Guid id)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        sheet.LastDismissedLevelUpLevel = sheet.Nivel;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task NotifyAffectedEncountersAsync(Guid creatureSheetId)
    {
        var affectedEncounterIds = await db.EncounterParticipants
            .Where(p => p.SourceCreatureSheetId == creatureSheetId)
            .Select(p => p.EncounterId)
            .Distinct()
            .ToListAsync();
        foreach (var encounterId in affectedEncounterIds)
        {
            // Item 3 of the gap audit: the sheet is already saved at this point — a hub failure
            // must not surface as a 500 to a caller whose save genuinely succeeded.
            try
            {
                await hub.Clients.Group($"encounter-{encounterId}").SendAsync("ParticipantsChanged");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify encounter {EncounterId} of a ParticipantsChanged update.", encounterId);
            }
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

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
    /// Shared by ToResponseAsync's máximo computation — a single-row lookup +
    /// AttributeTotalCalculator.Total, rather than duplicating that logic. Mirrors
    /// NpcSheetsController.GetAttributeTotalAsync, scoped to CreatureAttributes/AtributoCriatura.
    /// </summary>
    private async Task<int> GetAttributeTotalAsync(Guid sheetId, AtributoCriatura atributo, IReadOnlyList<ArtifactBonusInput> artefatos)
    {
        var attribute = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetId && a.Atributo == atributo);
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString()));
    }

    /// <summary>
    /// Ficha de Personagem 3.f (inherited by Criatura): one read-only value per Tipo de Dano, each
    /// the sum of equipped Artefatos whose Tipo de alvo is Dano and whose Alvo is that Tipo de
    /// Dano. Mirrors CharacterSheetsController.ModificadorDeDano.
    /// </summary>
    [HttpGet("{id}/modificador-de-dano")]
    public async Task<ActionResult<ModificadorDeDanoResponse>> ModificadorDeDano(Guid id)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var artefatos = await GetArtifactBonusInputsAsync(id);
        return new ModificadorDeDanoResponse(
            Cortante: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Dano, TipoDeDano.Cortante.ToString()),
            Perfurante: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Dano, TipoDeDano.Perfurante.ToString()),
            Contundente: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Dano, TipoDeDano.Contundente.ToString()),
            Arcano: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Dano, TipoDeDano.Arcano.ToString()));
    }

    /// <summary>
    /// Every CreatureArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses
    /// 5.b has no equip/unequip toggle for Artefatos, so simply being on the sheet counts as
    /// equipped. Mirrors CharacterSheetsController.GetArtifactBonusInputsAsync.
    /// </summary>
    private async Task<List<ArtifactBonusInput>> GetArtifactBonusInputsAsync(Guid sheetId) =>
        await db.CreatureArtifacts
            .Where(a => a.CreatureSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    /// <summary>
    /// Read-only, everything derived live — nothing here is persisted. The computation itself
    /// lives in CreatureSheetStats.SubAtributosAsync, shared with the FichaParaRequisitos
    /// snapshot used by Passiva requisitos.
    /// </summary>
    [HttpGet("{id}/sub-attributes")]
    public async Task<ActionResult<SubAttributesResponse>> SubAttributes(Guid id)
    {
        var sheet = await db.CreatureSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await stats.SubAtributosAsync(sheet);
    }

    // The GM's whole Creature roster/library, not scoped to any one player — GM-only, same reasoning as Create.
    [HttpGet]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<List<CreatureSheetSummaryResponse>>> List(
        [FromQuery] string? nome, [FromQuery] string? raca, [FromQuery] string? arquetipo, [FromQuery] string? rank,
        /// <summary>No-op placeholder until CampaignAttachments lands in the Campanha — Anexos plan.</summary>
        [FromQuery] string? campaignId)
    {
        var gmId = CurrentUserId();
        var query = db.CreatureSheets.Where(s => s.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(s => s.Nome != null && EF.Functions.ILike(s.Nome, $"%{nome}%"));
        if (!string.IsNullOrWhiteSpace(raca))
            query = query.Where(s => s.Raca != null && EF.Functions.ILike(s.Raca, $"%{raca}%"));
        if (arquetipo is not null && Enum.TryParse<Arquetipo>(arquetipo, out var arquetipoParsed))
            query = query.Where(s => s.Arquetipo == arquetipoParsed);
        if (rank is not null && Enum.TryParse<Rank>(rank, out var rankParsed))
            query = query.Where(s => s.Rank == rankParsed);

        // Two chained GroupJoins over an already-anonymous-typed source don't translate on this
        // EF Core/Npgsql version ("could not be translated") — a correlated FirstOrDefault
        // subquery per join is the reliable equivalent and translates to a plain LEFT JOIN each.
        return await query
            .Select(s => new
            {
                Sheet = s,
                Owner = db.Users.FirstOrDefault(u => u.Id == s.OwnerId),
                Image = db.Images.FirstOrDefault(i => i.Id == s.ImageId)
            })
            .Select(x => new CreatureSheetSummaryResponse(x.Sheet.Id.ToString(), x.Sheet.Nome ?? "", x.Sheet.Raca, x.Sheet.Arquetipo == null ? null : x.Sheet.Arquetipo.ToString(), x.Sheet.Rank == null ? null : x.Sheet.Rank.ToString(), x.Sheet.Nivel, x.Owner != null ? x.Owner.Nickname : null, x.Image != null ? "/images/" + x.Image.Path : null))
            .ToListAsync();
    }

    /// <summary>
    /// Abate/Assistencia are computed live via XpAwardCalculator, never persisted — same pattern
    /// as CharacterSheetResponse.Graduacao/NpcSheetResponse.Graduacao.
    ///
    /// VitalidadeMaximo/FocoMaximo/AdrenalinaMaximo mirror NpcSheetsController.ToResponseAsync's
    /// máximo computation, via ResourceMaximumCalculator and IRulesDataProvider.Arquetipos (looked
    /// up by Arquetipo/Nivel — unlike Vocacao, Arquetipo's C# enum member names (Fisico/Arcano)
    /// match the real Tabela de Arquetipos.md's labels exactly, so a plain .ToString() lookup is
    /// correct here — no accent-mapping helper needed).
    ///
    /// vigorTotal/astuciaTotal are queried via GetAttributeTotalAsync now that CreatureAttribute
    /// exists (Task 3) — resolves the earlier hardcoded-0 gap (mirrors NpcSheetsController's
    /// equivalent fix once NpcAttribute existed).
    /// </summary>
    private async Task<CreatureSheetResponse> ToResponseAsync(CreatureSheet s)
    {
        string? imageUrl = null;
        if (s.ImageId is not null)
        {
            var image = await db.Images.FindAsync(s.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        var abate = XpAwardCalculator.Abate(s.ExperienciaAtual);
        var assistencia = XpAwardCalculator.Assistencia(s.ExperienciaAtual);

        var artefatosParaMaximos = await GetArtifactBonusInputsAsync(s.Id);
        var vigorTotal = await GetAttributeTotalAsync(s.Id, AtributoCriatura.Vigor, artefatosParaMaximos);
        var astuciaTotal = await GetAttributeTotalAsync(s.Id, AtributoCriatura.Astucia, artefatosParaMaximos);
        var (statusVida, statusFoco) = s.Arquetipo is not null ? VidaEArcanaPorNivel.Arquetipo(rules.Arquetipos, s.Arquetipo.Value.ToString(), s.Nivel) : (0, 0);
        var artefatoBonusParaAdrenalina = ArtifactBonusCalculator.Sum(artefatosParaMaximos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Adrenalina);

        var vitalidadeMaximo = ResourceMaximumCalculator.Vitalidade(vigorTotal, statusVida);
        var focoMaximo = ResourceMaximumCalculator.Foco(astuciaTotal, statusFoco);
        var adrenalinaMaximo = ResourceMaximumCalculator.Adrenalina(artefatoBonusParaAdrenalina);

        var campaignId = await db.CampaignAttachments
            .Where(a => a.CreatureSheetId == s.Id && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == s.OwnerId))
            .Select(a => (Guid?)a.CampaignId)
            .FirstOrDefaultAsync();

        return new CreatureSheetResponse(
            s.Id.ToString(), s.OwnerId?.ToString(), imageUrl,
            s.Nome, s.Raca, s.Arquetipo?.ToString(), s.SubArquetipo, s.Afinidade?.ToString(),
            s.Rank?.ToString(), s.Nivel, s.ExperienciaAtual, abate, assistencia,
            s.PontosDeIgnicao, s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.Cobertura.ToString(),
            vitalidadeMaximo, focoMaximo, adrenalinaMaximo,
            campaignId?.ToString(), s.ImageId?.ToString());
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
