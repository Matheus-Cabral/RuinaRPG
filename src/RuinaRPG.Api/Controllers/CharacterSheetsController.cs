using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

// Not route-attributed at the class level (unlike every previous controller in this codebase)
// because its actions live under two different route prefixes:
// api/campaigns/{campaignId}/character-sheets and api/character-sheets/{id}.
[ApiController]
[Authorize]
public class CharacterSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules, IHubContext<EncounterHub> hub, ILogger<CharacterSheetsController> logger) : ControllerBase
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

        foreach (var atributo in Enum.GetValues<Atributo>())
            db.CharacterAttributes.Add(new CharacterAttribute { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Atributo = atributo });
        foreach (var pericia in Enum.GetValues<Pericia>())
            db.CharacterSkills.Add(new CharacterSkill { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Pericia = pericia });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.CharacterArmorSlots.Add(new CharacterArmorSlot { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Slot = slot });

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

    /// <summary>
    /// Painel do Jogador (canvas "01 - Visão Geral" — the login/signup/panel flow diagram):
    /// a player lands on a list of their own character sheets across every campaign, not scoped
    /// to one — unlike ListForCampaign above, which is the GM's per-campaign roster view.
    /// </summary>
    [HttpGet("~/api/character-sheets/mine")]
    [Authorize(Roles = "Jogador")]
    public async Task<ActionResult<List<MyCharacterSheetSummaryResponse>>> ListMine()
    {
        var playerId = CurrentUserId();

        var sheets = await db.CharacterSheets
            .Where(s => s.OwnerId == playerId)
            .GroupJoin(db.Campaigns, s => s.CampaignId, c => c.Id, (s, campaigns) => new { Sheet = s, Campaign = campaigns.FirstOrDefault() })
            .ToListAsync();

        var responses = new List<MyCharacterSheetSummaryResponse>();
        foreach (var x in sheets)
        {
            string? imageUrl = null;
            if (x.Sheet.ImageId is not null)
            {
                var image = await db.Images.FindAsync(x.Sheet.ImageId.Value);
                imageUrl = image is not null ? $"/images/{image.Path}" : null;
            }

            responses.Add(new MyCharacterSheetSummaryResponse(
                x.Sheet.Id.ToString(), imageUrl, x.Sheet.Nome, x.Sheet.Nivel,
                x.Sheet.CampaignId.ToString(), x.Campaign?.Nome ?? ""));
        }

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

    [HttpGet("api/character-sheets/{id}/racial-ability")]
    public async Task<ActionResult<RacialAbilityResponse>> RacialAbility(Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        if (sheet.Variante is null)
            return new RacialAbilityResponse(null, null);

        var ability = RacialAbilityLookup.For(sheet.Variante.Value);
        return new RacialAbilityResponse(ability.Nome, ability.Descricao);
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
        // Nível is a pure function of Experiência Atual now (1.b, "Para o próximo") — no more
        // GM-editable override for Ficha de Personagem, unlike NPC/Criatura sheets.
        var nivel = NivelCalculator.Compute(request.ExperienciaAtual, rules.XpPorNivel);
        sheet.Nivel = nivel;
        sheet.PossuiCoracaoDeMana = request.PossuiCoracaoDeMana;
        sheet.ExperienciaAtual = request.ExperienciaAtual;
        // sheet.EAPAtual is intentionally never written from here on — EAPAtual is now a pure
        // function of Nivel + NucleosRank* (EapCalculator, used in ToResponseAsync), same
        // treatment already given to Círculo/Grau. request.EAPAtual is accepted-but-ignored to
        // avoid a wider positional-record rewrite across every existing call site.
        sheet.NucleosRankF = request.NucleosRankF;
        sheet.NucleosRankE = request.NucleosRankE;
        sheet.NucleosRankD = request.NucleosRankD;
        sheet.NucleosRankC = request.NucleosRankC;
        sheet.NucleosRankB = request.NucleosRankB;
        sheet.NucleosRankA = request.NucleosRankA;
        sheet.NucleosRankS = request.NucleosRankS;
        sheet.PontosDeIgnicaoAtual = request.PontosDeIgnicaoAtual;
        // PontosDeIgnicaoTotal is a pure function of Nível + PontosDeIgnicaoBonusManual now (1.b,
        // "os bônus de PI por nível constam na Tabela de Níveis") — same treatment already given
        // to EAPAtual. Only the manual bonus on top is ever written from here on.
        sheet.PontosDeIgnicaoBonusManual = request.PontosDeIgnicaoBonusManual;

        // "Atual não pode exceder o máximo" (1.c, all 4 resources) — clamped rather than
        // rejected, since a Máximo can legitimately shrink (e.g. unequipping an Artefato) out
        // from under an Atual that was valid a moment ago.
        var maximos = await ComputeResourceMaximumsAsync(id, vocacao, nivel);
        sheet.VitalidadeAtual = Math.Min(request.VitalidadeAtual, maximos.Vitalidade);
        sheet.FocoAtual = Math.Min(request.FocoAtual, maximos.Foco);
        sheet.AdrenalinaAtual = Math.Min(request.AdrenalinaAtual, maximos.Adrenalina);
        sheet.EstresseAtual = Math.Min(request.EstresseAtual, maximos.Estresse);
        sheet.Cobertura = cobertura;
        sheet.Ciclos = request.Ciclos;
        sheet.PontosDePericiaBonusCritico = request.PontosDePericiaBonusCritico;

        await db.SaveChangesAsync();

        var affectedEncounterIds = await db.EncounterParticipants
            .Where(p => p.SourceCharacterSheetId == id)
            .Select(p => p.EncounterId)
            .Distinct()
            .ToListAsync();
        foreach (var encounterId in affectedEncounterIds)
        {
            // Item 3 of the gap audit: the sheet is already saved at this point — a hub failure
            // (e.g. the SignalR backplane being briefly unreachable) must not surface as a 500 to
            // a caller whose save genuinely succeeded. Live sync just falls behind until the next
            // change; it doesn't lose data.
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
    /// Read-only, everything derived live — nothing here is persisted. "Bruto [Perícia]" terms
    /// (Prontidão, Reflexos, Fortitude) mean that Perícia's Modificador alone (Sistema Básico §2 /
    /// SkillFormulas.Modificador), sourced from the sheet's own CharacterSkill rows below.
    /// </summary>
    [HttpGet("api/character-sheets/{id}/sub-attributes")]
    public async Task<ActionResult<SubAttributesResponse>> SubAttributes(Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var artefatos = await GetArtifactBonusInputsAsync(id);

        var agilidade = await GetAttributeTotalAsync(id, Atributo.Agilidade, artefatos);
        var vigor = await GetAttributeTotalAsync(id, Atributo.Vigor, artefatos);
        var forca = await GetAttributeTotalAsync(id, Atributo.Forca, artefatos);

        var brutoSkills = await db.CharacterSkills
            .Where(s => s.CharacterSheetId == id && (s.Pericia == Pericia.Prontidao || s.Pericia == Pericia.Reflexos || s.Pericia == Pericia.Fortitude))
            .ToListAsync();
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(brutoSkills.Single(s => s.Pericia == pericia).Gasto);

        var brutoProntidao = BrutoOf(Pericia.Prontidao);
        var brutoReflexos = BrutoOf(Pericia.Reflexos);
        var brutoFortitude = BrutoOf(Pericia.Fortitude);

        var weapons = await db.CharacterWeapons.Where(w => w.CharacterSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var armorSlots = await db.CharacterArmorSlots.Where(a => a.CharacterSheetId == id && a.ItemId != null).Join(db.Items, a => a.ItemId!.Value, i => i.Id, (a, i) => i.Peso).ToListAsync();
        var shields = await db.CharacterShields.Where(s => s.CharacterSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => i.Peso).ToListAsync();
        var pesoTotalCarregado = weapons.Sum(w => w.Peso) + armorSlots.Sum() + shields.Sum();

        var equippedShield = await db.CharacterShields
            .Where(s => s.CharacterSheetId == id && s.IsEquipped)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => i.BonusDefesa)
            .FirstOrDefaultAsync();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Every CharacterArmorSlot with an ItemId is inherently worn (no separate IsEquipped
        // flag, unlike weapons/shields) — so, unlike Peso Total Carregado, this is already
        // scoped to equipped armor only.
        var armorRfRm = await db.CharacterArmorSlots
            .Where(a => a.CharacterSheetId == id && a.ItemId != null)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { i.RF, i.RM })
            .ToListAsync();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), (int)pesoTotalCarregado, forca, vigor),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number, so it can't be summed into this
            // numeric formula term today. Unlike Bruto/Artefatos above, this is a real, still-open gap.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm));
    }

    /// <summary>
    /// Every CharacterArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses 5.b
    /// has no separate equip/unequip toggle for Artefatos, so being on the sheet is being "equipped".
    /// Loaded once per top-level action and threaded through, rather than re-querying per formula term.
    /// </summary>
    private async Task<List<ArtifactBonusInput>> GetArtifactBonusInputsAsync(Guid sheetId) =>
        await db.CharacterArtifacts
            .Where(a => a.CharacterSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    /// <summary>
    /// Shared by SubAttributes and the máximo computation in ToResponseAsync — a single-row
    /// lookup + AttributeTotalCalculator.Total, rather than duplicating that logic a third time.
    /// </summary>
    private async Task<int> GetAttributeTotalAsync(Guid sheetId, Atributo atributo, IReadOnlyList<ArtifactBonusInput> artefatos)
    {
        var attribute = await db.CharacterAttributes.SingleAsync(a => a.CharacterSheetId == sheetId && a.Atributo == atributo);
        var artefatoBonus = ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString());
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: artefatoBonus);
    }

    /// <summary>
    /// Vocacao's C# member names are unaccented (Campeao, Cacador, ...) but IRulesDataProvider.Vocacoes
    /// is parsed straight from the real Tabela de Vocação markdown, which uses the accented Portuguese
    /// names (Campeão, Caçador). A raw .ToString() lookup would never match those two, silently
    /// returning 0 for statusVida/statusFoco. Used ONLY for that lookup — the existing, unrelated
    /// convention elsewhere in this file exposing s.Vocacao?.ToString() unaccented to API clients is
    /// separate and correct as-is.
    /// </summary>
    private static string VocacaoTabelaName(RuinaRPG.Domain.CharacterSheets.Vocacao vocacao) => vocacao switch
    {
        RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao => "Campeão",
        RuinaRPG.Domain.CharacterSheets.Vocacao.Cacador => "Caçador",
        _ => vocacao.ToString()
    };

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

        // EAPAtual is no longer a stored/editable value — same treatment already given to
        // Círculo/Grau, which became a pure function instead of a directly-set column. "Segue a
        // tabela [XP/EAP por Nível] e é somado pelo resultado de Âmbares Absorvidos" (1.b).
        var eapAtual = EapCalculator.Compute(s.Nivel, s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS, rules.EapPorNivel);

        var vocacao = s.Vocacao ?? RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao; // no vocação chosen yet → Graduacao is meaningless but must not throw
        var graduacao = s.Vocacao is null ? 0 : GraduacaoCalculator.Compute(vocacao, eapAtual, s.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);
        var graduacaoLabel = vocacao is RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao or RuinaRPG.Domain.CharacterSheets.Vocacao.Cacador ? "Grau" : "Círculo";

        var maximos = await ComputeResourceMaximumsAsync(s.Id, s.Vocacao, s.Nivel);
        var xpParaProximoNivel = NivelCalculator.XpParaProximoNivel(s.ExperienciaAtual, rules.XpPorNivel);
        var pontosDeIgnicaoTotal = PontosDeIgnicaoCalculator.ComputeTotal(s.Nivel, s.PontosDeIgnicaoBonusManual, rules.Niveis);

        return new CharacterSheetResponse(
            s.Id.ToString(), s.CampaignId.ToString(), s.OwnerId.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade?.ToString(), s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, eapAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, pontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos, graduacao, graduacaoLabel,
            maximos.Vitalidade, maximos.Foco, maximos.Adrenalina, maximos.Estresse, xpParaProximoNivel,
            s.PontosDeIgnicaoBonusManual, s.PontosDePericiaBonusCritico, s.ImageId?.ToString());
    }

    /// <summary>
    /// Shared by ToResponseAsync (display) and Update (clamping "Atual não pode exceder o
    /// máximo", 1.c) — "Status de classe Vida/Foco" comes from Tabela de Vocação (Vocação ×
    /// Nível); that table's rows are keyed by the 5 base Vocação names, not by Sub-Vocação/Classe,
    /// so vocacao (not SubVocacao) is the lookup key — an accepted approximation (see plan's
    /// Explicitly out of scope).
    /// </summary>
    private async Task<(int Vitalidade, int Foco, int Adrenalina, int Estresse)> ComputeResourceMaximumsAsync(Guid sheetId, RuinaRPG.Domain.CharacterSheets.Vocacao? vocacao, int nivel)
    {
        var artefatos = await GetArtifactBonusInputsAsync(sheetId);
        var vigorTotal = await GetAttributeTotalAsync(sheetId, Atributo.Vigor, artefatos);
        var astuciaTotal = await GetAttributeTotalAsync(sheetId, Atributo.Astucia, artefatos);
        var statusVida = vocacao is not null ? rules.Vocacoes.Where(v => v.Vocacao == VocacaoTabelaName(vocacao.Value) && v.Nivel == nivel).Select(v => v.Vida).FirstOrDefault() : 0;
        var statusFoco = vocacao is not null ? rules.Vocacoes.Where(v => v.Vocacao == VocacaoTabelaName(vocacao.Value) && v.Nivel == nivel).Select(v => v.Arcana).FirstOrDefault() : 0;
        var artefatoBonusParaAdrenalina = ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Adrenalina);

        return (
            ResourceMaximumCalculator.Vitalidade(vigorTotal, statusVida),
            ResourceMaximumCalculator.Foco(astuciaTotal, statusFoco),
            ResourceMaximumCalculator.Adrenalina(artefatoBonusParaAdrenalina),
            ResourceMaximumCalculator.Estresse());
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
