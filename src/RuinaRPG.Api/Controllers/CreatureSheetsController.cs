using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
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

        foreach (var atributo in Enum.GetValues<AtributoCriatura>())
            db.CreatureAttributes.Add(new CreatureAttribute { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Atributo = atributo });
        // R0005's "lista fixa mais curta" — only the 20 allowed Pericia values, not all 39
        // (unlike Ficha de NPCs' Enum.GetValues<Pericia>()).
        foreach (var pericia in CreatureSkillAllowList.AllowedPericias)
            db.CreatureSkills.Add(new CreatureSkill { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Pericia = pericia });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.CreatureArmorSlots.Add(new CreatureArmorSlot { Id = Guid.NewGuid(), CreatureSheetId = sheet.Id, Slot = slot });

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
    /// Shared by ToResponseAsync's máximo computation — a single-row lookup +
    /// AttributeTotalCalculator.Total, rather than duplicating that logic. Mirrors
    /// NpcSheetsController.GetAttributeTotalAsync, scoped to CreatureAttributes/AtributoCriatura.
    /// </summary>
    private async Task<int> GetAttributeTotalAsync(Guid sheetId, AtributoCriatura atributo)
    {
        var attribute = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetId && a.Atributo == atributo);
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
    }

    /// <summary>
    /// R0005 §2.b: "Sub-Atributos — mesmos campos e fórmulas do Personagem" for 6 of its 9 listed
    /// sub-attributes (Iniciativa, Movimentação, Esquiva Natural, Defesa Natural, Redução Física,
    /// Redução Mágica); Resistência Física/Arcana and Dano Cortante are explicitly "pendente" — no
    /// formula defined yet, so they're not implemented here. Read-only, everything derived live —
    /// nothing here is persisted. Mirrors NpcSheetsController.SubAttributes, table-for-table, scoped
    /// to CreatureAttributes/CreatureSkills/CreatureWeapons/CreatureArmorSlots/CreatureShields.
    /// </summary>
    [HttpGet("{id}/sub-attributes")]
    public async Task<ActionResult<SubAttributesResponse>> SubAttributes(Guid id)
    {
        var sheet = await db.CreatureSheets.FirstOrDefaultAsync(s => s.Id == id && s.GmId == CurrentGmId());
        if (sheet is null)
            return NotFound();

        var agilidade = await GetAttributeTotalAsync(id, AtributoCriatura.Agilidade);
        var vigor = await GetAttributeTotalAsync(id, AtributoCriatura.Vigor);
        var forca = await GetAttributeTotalAsync(id, AtributoCriatura.Forca);

        var brutoSkills = await db.CreatureSkills
            .Where(s => s.CreatureSheetId == id && (s.Pericia == Pericia.Prontidao || s.Pericia == Pericia.Reflexos || s.Pericia == Pericia.Fortitude))
            .ToListAsync();
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(brutoSkills.Single(s => s.Pericia == pericia).Gasto);

        var brutoProntidao = BrutoOf(Pericia.Prontidao);
        var brutoReflexos = BrutoOf(Pericia.Reflexos);
        var brutoFortitude = BrutoOf(Pericia.Fortitude);

        // Natural attacks (ItemId null) carry no weight of their own — only Catálogo-linked
        // weapons/armor/shields contribute to Peso Total Carregado.
        var weapons = await db.CreatureWeapons.Where(w => w.CreatureSheetId == id && w.ItemId != null).Join(db.Items, w => w.ItemId!.Value, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var armorSlots = await db.CreatureArmorSlots.Where(a => a.CreatureSheetId == id && a.ItemId != null).Join(db.Items, a => a.ItemId!.Value, i => i.Id, (a, i) => i.Peso).ToListAsync();
        var shields = await db.CreatureShields.Where(s => s.CreatureSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => i.Peso).ToListAsync();
        var pesoTotalCarregado = weapons.Sum(w => w.Peso) + armorSlots.Sum() + shields.Sum();

        var equippedShield = await db.CreatureShields
            .Where(s => s.CreatureSheetId == id && s.IsEquipped)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => i.BonusDefesa)
            .FirstOrDefaultAsync();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Every CreatureArmorSlot with an ItemId is inherently worn (no separate IsEquipped
        // flag, unlike weapons/shields) — so, unlike Peso Total Carregado, this is already
        // scoped to equipped armor only.
        var armorRfRm = await db.CreatureArmorSlots
            .Where(a => a.CreatureSheetId == id && a.ItemId != null)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { i.RF, i.RM })
            .ToListAsync();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: 0),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: 0, (int)pesoTotalCarregado, forca, vigor),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number, so it can't be summed into this
            // numeric formula term today. Same real, still-open gap as the Ficha de NPCs version.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: 0, penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: 0, cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: 0, armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: 0, armaduraMagica: armaduraRm));
    }

    [HttpGet]
    public async Task<ActionResult<List<CreatureSheetSummaryResponse>>> List(
        [FromQuery] string? nome, [FromQuery] string? raca, [FromQuery] string? arquetipo, [FromQuery] string? rank,
        /// <summary>No-op placeholder until CampaignAttachments lands in the Campanha — Anexos plan.</summary>
        [FromQuery] string? campaignId)
    {
        var gmId = CurrentGmId();
        var query = db.CreatureSheets.Where(s => s.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(s => s.Nome != null && EF.Functions.ILike(s.Nome, $"%{nome}%"));
        if (!string.IsNullOrWhiteSpace(raca))
            query = query.Where(s => s.Raca != null && EF.Functions.ILike(s.Raca, $"%{raca}%"));
        if (arquetipo is not null && Enum.TryParse<Arquetipo>(arquetipo, out var arquetipoParsed))
            query = query.Where(s => s.Arquetipo == arquetipoParsed);
        if (rank is not null && Enum.TryParse<Rank>(rank, out var rankParsed))
            query = query.Where(s => s.Rank == rankParsed);

        return await query.Select(s => new CreatureSheetSummaryResponse(s.Id.ToString(), s.Nome ?? "", s.Raca, s.Arquetipo == null ? null : s.Arquetipo.ToString(), s.Rank == null ? null : s.Rank.ToString(), s.Nivel)).ToListAsync();
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

        var kill = XpAwardCalculator.Kill(s.ExperienciaAtual);
        var assistencia = XpAwardCalculator.Assistencia(s.ExperienciaAtual);

        var vigorTotal = await GetAttributeTotalAsync(s.Id, AtributoCriatura.Vigor);
        var astuciaTotal = await GetAttributeTotalAsync(s.Id, AtributoCriatura.Astucia);
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
