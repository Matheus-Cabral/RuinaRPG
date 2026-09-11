using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/npc-sheets")]
[Authorize]
public class NpcSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules, IHubContext<EncounterHub> hub, ILogger<NpcSheetsController> logger) : ControllerBase
{
    // Creating a fresh (un-granted) NPC is GM roster curation, not something a player who's been
    // granted one already does — same reasoning as List below.
    [HttpPost]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<NpcSheetResponse>> Create()
    {
        var gmId = CurrentUserId();

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
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await ToResponseAsync(sheet);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateNpcSheetRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

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
        if (request.ArcaRolada is < 1 or > 18)
            return BadRequest("ArcaRolada deve estar entre 1 e 18.");

        sheet.Cobertura = cobertura;
        sheet.Ciclos = request.Ciclos;
        sheet.ArcaRolada = request.ArcaRolada;

        await db.SaveChangesAsync();

        var affectedEncounterIds = await db.EncounterParticipants
            .Where(p => p.SourceNpcSheetId == id)
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

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        db.NpcSheets.Remove(sheet);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/racial-ability")]
    public async Task<ActionResult<RacialAbilityResponse>> RacialAbility(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        if (sheet.Variante is null)
            return new RacialAbilityResponse(null, null, null, null, null);

        var over = await db.RacialAbilityOverrides.FirstOrDefaultAsync(o => o.GmId == sheet.GmId && o.Variante == sheet.Variante.Value);
        var (nome, descricao) = over is not null
            ? (over.Nome, over.Descricao)
            : (RacialAbilityLookup.For(sheet.Variante.Value).Nome, RacialAbilityLookup.For(sheet.Variante.Value).Descricao);

        string? arcaNome = null;
        string? arcaDescricao = null;
        if (sheet.Linhagem == Linhagem.Humano && sheet.ArcaRolada is not null)
        {
            var arca = await db.ArcaEntries.FirstOrDefaultAsync(a => a.GmId == sheet.GmId && a.Roll == sheet.ArcaRolada.Value);
            arcaNome = arca?.Nome;
            arcaDescricao = arca?.Descricao;
        }

        return new RacialAbilityResponse(nome, descricao, sheet.ArcaRolada, arcaNome, arcaDescricao);
    }

    /// <summary>
    /// Read-only, everything derived live — nothing here is persisted. "Bruto [Perícia]" terms
    /// (Prontidão, Reflexos, Fortitude) mean that Perícia's Modificador alone (Sistema Básico §2 /
    /// SkillFormulas.Modificador), sourced from the sheet's own NpcSkill rows below. Mirrors
    /// CharacterSheetsController.SubAttributes exactly, table-for-table.
    /// </summary>
    [HttpGet("{id}/sub-attributes")]
    public async Task<ActionResult<SubAttributesResponse>> SubAttributes(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var artefatos = await GetArtifactBonusInputsAsync(id);
        var agilidade = await GetAttributeTotalAsync(id, Atributo.Agilidade, artefatos);
        var vigor = await GetAttributeTotalAsync(id, Atributo.Vigor, artefatos);
        var forca = await GetAttributeTotalAsync(id, Atributo.Forca, artefatos);

        var brutoSkills = await db.NpcSkills
            .Where(s => s.NpcSheetId == id && (s.Pericia == Pericia.Prontidao || s.Pericia == Pericia.Reflexos || s.Pericia == Pericia.Fortitude))
            .ToListAsync();
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(brutoSkills.Single(s => s.Pericia == pericia).Gasto);

        var brutoProntidao = BrutoOf(Pericia.Prontidao);
        var brutoReflexos = BrutoOf(Pericia.Reflexos);
        var brutoFortitude = BrutoOf(Pericia.Fortitude);

        var weapons = await db.NpcWeapons.Where(w => w.NpcSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var shields = await db.NpcShields.Where(s => s.NpcSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => new { s.IsEquipped, i.Peso }).ToListAsync();
        var inventoryItems = await db.NpcInventoryItems.Where(i => i.NpcSheetId == id)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>(), i => i.ItemId, g => g.Id, (i, g) => new { i.Qtd, g.Peso, g.CapacidadeExtra })
            .ToListAsync();

        // Peso Total 2.b (corrected — see docs/superpowers/specs/2026-09-08-inventory-weight-and-
        // capacity-design.md): Inventário (5.a) + Armas/Escudos DESequipados. Armaduras never count
        // — an NpcArmorSlot with an ItemId is inherently worn (no unequipped state exists for armor
        // in this schema). A Capacidade Extra ("mochila") item doesn't add its own Peso here — it
        // raises pesoMaximo instead.
        var pesoAtual = inventoryItems.Where(i => CarryWeightCalculator.CountsTowardPesoAtual(i.CapacidadeExtra)).Sum(i => i.Peso * i.Qtd)
            + weapons.Where(w => !w.IsEquipped).Sum(w => w.Peso)
            + shields.Where(s => !s.IsEquipped).Sum(s => s.Peso);
        var capacidadeExtraTotal = inventoryItems.Sum(i => (i.CapacidadeExtra ?? 0m) * i.Qtd);
        var pesoMaximo = CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal);

        var equippedShield = await db.NpcShields
            .Where(s => s.NpcSheetId == id && s.IsEquipped)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => i.BonusDefesa)
            .FirstOrDefaultAsync();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Every NpcArmorSlot with an ItemId is inherently worn (no separate IsEquipped
        // flag, unlike weapons/shields) — so, unlike Peso Total Carregado, this is already
        // scoped to equipped armor only.
        var armorRfRm = await db.NpcArmorSlots
            .Where(a => a.NpcSheetId == id && a.ItemId != null)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { i.RF, i.RM })
            .ToListAsync();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number, so it can't be summed into this
            // numeric formula term today. Unlike Bruto above, this is a real, still-open gap.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo);
    }

    /// <summary>
    /// Shared by SubAttributes and the máximo computation in ToResponseAsync — a single-row
    /// lookup + AttributeTotalCalculator.Total, rather than duplicating that logic a third time.
    /// Mirrors CharacterSheetsController.GetAttributeTotalAsync, scoped to NpcAttributes.
    /// </summary>
    private async Task<int> GetAttributeTotalAsync(Guid sheetId, Atributo atributo, IReadOnlyList<ArtifactBonusInput> artefatos)
    {
        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString()));
    }

    /// <summary>
    /// Every NpcArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses 5.b has
    /// no equip/unequip toggle for Artefatos, so simply being on the sheet counts as equipped.
    /// Mirrors CharacterSheetsController.GetArtifactBonusInputsAsync.
    /// </summary>
    private async Task<List<ArtifactBonusInput>> GetArtifactBonusInputsAsync(Guid sheetId) =>
        await db.NpcArtifacts
            .Where(a => a.NpcSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    // The GM's whole NPC roster/library, not scoped to any one player — GM-only, same reasoning as Create.
    [HttpGet]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<List<NpcSheetSummaryResponse>>> List(
        [FromQuery] string? nome, [FromQuery] string? linhagem, [FromQuery] string? variante, [FromQuery] string? vocacao, [FromQuery] string? subVocacao, [FromQuery] int? nivel,
        /// <summary>No-op placeholder until CampaignAttachments lands in the Campanha — Anexos plan.</summary>
        [FromQuery] string? campaignId)
    {
        var gmId = CurrentUserId();
        var query = db.NpcSheets.Where(s => s.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(s => s.Nome != null && EF.Functions.ILike(s.Nome, $"%{nome}%"));
        if (linhagem is not null && Enum.TryParse<Linhagem>(linhagem, out var linhagemParsed))
            query = query.Where(s => s.Linhagem == linhagemParsed);
        if (variante is not null && Enum.TryParse<Variante>(variante, out var variantesParsed))
            query = query.Where(s => s.Variante == variantesParsed);
        if (vocacao is not null && Enum.TryParse<Vocacao>(vocacao, out var vocacaoParsed))
            query = query.Where(s => s.Vocacao == vocacaoParsed);
        if (!string.IsNullOrWhiteSpace(subVocacao))
            query = query.Where(s => s.SubVocacao == subVocacao);
        if (nivel is not null)
            query = query.Where(s => s.Nivel == nivel);

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
            .Select(x => new NpcSheetSummaryResponse(x.Sheet.Id.ToString(), x.Sheet.Nome ?? "", x.Sheet.Linhagem.ToString(), x.Sheet.Vocacao.ToString(), x.Sheet.SubVocacao, x.Sheet.Nivel, x.Owner != null ? x.Owner.Nickname : null, x.Image != null ? "/images/" + x.Image.Path : null))
            .ToListAsync();
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
    /// now that NpcAttribute exists — vigorTotal/astuciaTotal are queried via GetAttributeTotalAsync,
    /// same as SubAttributes uses.
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

        var artefatosParaMaximos = await GetArtifactBonusInputsAsync(s.Id);
        var vigorTotal = await GetAttributeTotalAsync(s.Id, Atributo.Vigor, artefatosParaMaximos);
        var astuciaTotal = await GetAttributeTotalAsync(s.Id, Atributo.Astucia, artefatosParaMaximos);
        var statusVida = s.Vocacao is not null ? rules.Vocacoes.Where(v => v.Vocacao == VocacaoTabelaName(s.Vocacao.Value) && v.Nivel == s.Nivel).Select(v => v.Vida).FirstOrDefault() : 0;
        var statusFoco = s.Vocacao is not null ? rules.Vocacoes.Where(v => v.Vocacao == VocacaoTabelaName(s.Vocacao.Value) && v.Nivel == s.Nivel).Select(v => v.Arcana).FirstOrDefault() : 0;
        var artefatoBonusParaAdrenalina = ArtifactBonusCalculator.Sum(artefatosParaMaximos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Adrenalina);

        var vitalidadeMaximo = ResourceMaximumCalculator.Vitalidade(vigorTotal, statusVida);
        var focoMaximo = ResourceMaximumCalculator.Foco(astuciaTotal, statusFoco);
        var adrenalinaMaximo = ResourceMaximumCalculator.Adrenalina(artefatoBonusParaAdrenalina);
        var estresseMaximo = ResourceMaximumCalculator.Estresse();

        var campaignId = await db.CampaignAttachments
            .Where(a => a.NpcSheetId == s.Id && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == s.OwnerId))
            .Select(a => (Guid?)a.CampaignId)
            .FirstOrDefaultAsync();

        return new NpcSheetResponse(
            s.Id.ToString(), s.OwnerId?.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade?.ToString(), s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, s.EAPAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, s.PontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos, graduacao, graduacaoLabel,
            vitalidadeMaximo, focoMaximo, adrenalinaMaximo, estresseMaximo,
            campaignId?.ToString(), s.ImageId?.ToString(), s.ArcaRolada);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
