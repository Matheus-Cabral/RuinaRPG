using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}")]
public class CreatureArsenalController(RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades, RuinaRPG.Api.Services.EquipmentPenaltyService penalidades, RuinaRPG.Api.Services.CreatureSheetStats stats) : ControllerBase
{
    /// <summary>
    /// R0006 3.a: a Criatura weapon is EITHER a Catálogo-linked Arma (ItemId) OR a natural attack
    /// described inline (the 4 Manual* fields) — exactly one, never both/neither. Armor slots and
    /// shields (below) are unchanged from the Ficha de NPCs shape — R0006 says so explicitly.
    /// </summary>
    [HttpPost("weapons")]
    public async Task<ActionResult<CreatureWeaponResponse>> AddWeapon(Guid sheetId, AddCreatureWeaponRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var isFromCatalogo = request.ItemId is not null;
        var isManual = request.ManualNome is not null && request.ManualTipoDeDano is not null && request.ManualDados is not null && request.ManualDano is not null;
        if (isFromCatalogo == isManual)
            return BadRequest("Informe exatamente um: ItemId (vincular ao Catálogo) ou os 4 campos manuais (ataque natural).");

        var weapon = new CreatureWeapon { Id = Guid.NewGuid(), CreatureSheetId = sheetId, IsEquipped = false };
        if (isFromCatalogo)
        {
            if (!Guid.TryParse(request.ItemId, out var itemId))
                return BadRequest("ItemId inválido.");

            var item = await db.Set<Arma>().FirstOrDefaultAsync(a => a.Id == itemId);
            if (item is null)
                return BadRequest("Item de arma não encontrado.");

            weapon.ItemId = itemId;
            weapon.DurabilidadeAtual = (await durabilidades.ResolverAsync(item.Rank)).Maxima ?? 0;
        }
        else
        {
            if (!Enum.TryParse<TipoDeDano>(request.ManualTipoDeDano, out var tipoDeDano))
                return BadRequest("Tipo de Dano desconhecido.");

            weapon.ManualNome = request.ManualNome;
            weapon.ManualTipoDeDano = tipoDeDano;
            weapon.ManualDados = request.ManualDados;
            weapon.ManualDano = request.ManualDano;
        }

        db.CreatureWeapons.Add(weapon);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToWeaponResponseAsync(weapon, await durabilidades.TabelaAsync()));
    }

    [HttpGet("weapons")]
    public async Task<ActionResult<List<CreatureWeaponResponse>>> ListWeapons(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapons = await db.CreatureWeapons.Where(w => w.CreatureSheetId == sheetId).ToListAsync();
        var tabela = await durabilidades.TabelaAsync();
        var responses = new List<CreatureWeaponResponse>();
        foreach (var weapon in weapons)
            responses.Add(await ToWeaponResponseAsync(weapon, tabela));
        return responses;
    }

    [HttpPut("weapons/{id}")]
    public async Task<IActionResult> UpdateWeapon(Guid sheetId, Guid id, [FromBody] bool isEquipped)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapon = await db.CreatureWeapons.FirstOrDefaultAsync(w => w.Id == id && w.CreatureSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        if (isEquipped)
        {
            // Default rule (no Ambidestria exception — see plan out-of-scope): only 1 weapon equipped at a time.
            var currentlyEquipped = await db.CreatureWeapons.Where(w => w.CreatureSheetId == sheetId && w.IsEquipped).ToListAsync();
            foreach (var other in currentlyEquipped)
                other.IsEquipped = false;
        }
        weapon.IsEquipped = isEquipped;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("weapons/{id}/durabilidade")]
    public async Task<IActionResult> UpdateWeaponDurability(Guid sheetId, Guid id, [FromBody] int durabilidadeAtual)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapon = await db.CreatureWeapons.FirstOrDefaultAsync(w => w.Id == id && w.CreatureSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        // A manual (natural attack) weapon has no ItemId, so no catalog Durabilidade to track at all.
        if (weapon.ItemId is null)
            return BadRequest("Ataques naturais (manuais) não têm Durabilidade.");

        var item = await db.Set<Arma>().SingleAsync(a => a.Id == weapon.ItemId);
        weapon.DurabilidadeAtual = DurabilidadeDeItem.LimitarAtual(durabilidadeAtual, (await durabilidades.ResolverAsync(item.Rank)).Maxima);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("weapons/{id}")]
    public async Task<IActionResult> DeleteWeapon(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapon = await db.CreatureWeapons.FirstOrDefaultAsync(w => w.Id == id && w.CreatureSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        db.CreatureWeapons.Remove(weapon);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("armor-slots")]
    public async Task<ActionResult<List<CreatureArmorSlotResponse>>> ListArmorSlots(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var slots = await db.CreatureArmorSlots.Where(a => a.CreatureSheetId == sheetId).ToListAsync();
        var tabela = await durabilidades.TabelaAsync();
        var responses = new List<CreatureArmorSlotResponse>();
        foreach (var slot in slots)
            responses.Add(await ToArmorSlotResponseAsync(slot, tabela));
        return responses;
    }

    [HttpPut("armor-slots/{slot}")]
    public async Task<IActionResult> UpdateArmorSlot(Guid sheetId, ArmorSlotType slot, UpdateCreatureArmorSlotRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var armorSlot = await db.CreatureArmorSlots.SingleAsync(a => a.CreatureSheetId == sheetId && a.Slot == slot);

        if (request.ItemId is null)
        {
            armorSlot.ItemId = null;
            armorSlot.DurabilidadeAtual = null;
        }
        else
        {
            if (!Guid.TryParse(request.ItemId, out var itemId))
                return BadRequest("ItemId inválido.");

            var item = await db.Set<Armadura>().FirstOrDefaultAsync(a => a.Id == itemId);
            if (item is null)
                return BadRequest("Item de armadura não encontrado.");

            armorSlot.ItemId = itemId;
            armorSlot.DurabilidadeAtual = (await durabilidades.ResolverAsync(item.Rank)).Maxima ?? 0;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("armor-slots/{slot}/durabilidade")]
    public async Task<IActionResult> UpdateArmorSlotDurability(Guid sheetId, ArmorSlotType slot, [FromBody] int durabilidadeAtual)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var armorSlot = await db.CreatureArmorSlots.SingleAsync(a => a.CreatureSheetId == sheetId && a.Slot == slot);
        if (armorSlot.ItemId is null)
            return BadRequest("Nenhuma armadura equipada nesse slot.");

        var item = await db.Set<Armadura>().SingleAsync(a => a.Id == armorSlot.ItemId);
        armorSlot.DurabilidadeAtual = DurabilidadeDeItem.LimitarAtual(durabilidadeAtual, (await durabilidades.ResolverAsync(item.Rank)).Maxima);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("shields")]
    public async Task<ActionResult<CreatureShieldResponse>> AddShield(Guid sheetId, AddCreatureShieldRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Set<Escudo>().FirstOrDefaultAsync(e => e.Id == itemId);
        if (item is null)
            return BadRequest("Item de escudo não encontrado.");

        var shield = new CreatureShield { Id = Guid.NewGuid(), CreatureSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = (await durabilidades.ResolverAsync(item.Rank)).Maxima ?? 0 };
        db.CreatureShields.Add(shield);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToShieldResponseAsync(shield, await durabilidades.TabelaAsync()));
    }

    [HttpGet("shields")]
    public async Task<ActionResult<List<CreatureShieldResponse>>> ListShields(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shields = await db.CreatureShields.Where(s => s.CreatureSheetId == sheetId).ToListAsync();
        var tabela = await durabilidades.TabelaAsync();
        var responses = new List<CreatureShieldResponse>();
        foreach (var shield in shields)
            responses.Add(await ToShieldResponseAsync(shield, tabela));
        return responses;
    }

    [HttpPut("shields/{id}")]
    public async Task<IActionResult> UpdateShield(Guid sheetId, Guid id, [FromBody] bool isEquipped)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shield = await db.CreatureShields.FirstOrDefaultAsync(s => s.Id == id && s.CreatureSheetId == sheetId);
        if (shield is null)
            return NotFound();

        if (isEquipped)
        {
            var currentlyEquipped = await db.CreatureShields.Where(s => s.CreatureSheetId == sheetId && s.IsEquipped).ToListAsync();
            foreach (var other in currentlyEquipped)
                other.IsEquipped = false;
        }
        shield.IsEquipped = isEquipped;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("shields/{id}/durabilidade")]
    public async Task<IActionResult> UpdateShieldDurability(Guid sheetId, Guid id, [FromBody] int durabilidadeAtual)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shield = await db.CreatureShields.FirstOrDefaultAsync(s => s.Id == id && s.CreatureSheetId == sheetId);
        if (shield is null)
            return NotFound();

        var item = await db.Set<Escudo>().SingleAsync(e => e.Id == shield.ItemId);
        shield.DurabilidadeAtual = DurabilidadeDeItem.LimitarAtual(durabilidadeAtual, (await durabilidades.ResolverAsync(item.Rank)).Maxima);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("shields/{id}")]
    public async Task<IActionResult> DeleteShield(Guid sheetId, Guid id)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shield = await db.CreatureShields.FirstOrDefaultAsync(s => s.Id == id && s.CreatureSheetId == sheetId);
        if (shield is null)
            return NotFound();

        db.CreatureShields.Remove(shield);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> CheckAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        return null;
    }

    // O retrato da ficha SEM penalidades é calculado uma vez por requisição, e só se algum item tiver Requisitos.
    private Task<RuinaRPG.Domain.SpellsAndAbilities.FichaParaRequisitos>? _fichaSemPenalidades;

    private Func<Task<RuinaRPG.Domain.SpellsAndAbilities.FichaParaRequisitos>> FichaSemPenalidades(Guid sheetId) =>
        () => _fichaSemPenalidades ??= CalcularFichaAsync(sheetId);

    private async Task<RuinaRPG.Domain.SpellsAndAbilities.FichaParaRequisitos> CalcularFichaAsync(Guid sheetId) =>
        await stats.FichaParaRequisitosAsync((await db.CreatureSheets.FindAsync(sheetId))!);

    private async Task<CreatureWeaponResponse> ComAvaliacaoAsync(CreatureWeaponResponse r, Item item, Guid sheetId)
    {
        var a = await penalidades.AvaliarAsync(item, FichaSemPenalidades(sheetId));
        return r with { Requisitos = a.Requisitos, RequisitosPendentes = a.RequisitosPendentes, Penalidade = a.Penalidade };
    }

    private async Task<CreatureArmorSlotResponse> ComAvaliacaoAsync(CreatureArmorSlotResponse r, Item item, Guid sheetId)
    {
        var a = await penalidades.AvaliarAsync(item, FichaSemPenalidades(sheetId));
        return r with { Requisitos = a.Requisitos, RequisitosPendentes = a.RequisitosPendentes, Penalidade = a.Penalidade };
    }

    private async Task<CreatureShieldResponse> ComAvaliacaoAsync(CreatureShieldResponse r, Item item, Guid sheetId)
    {
        var a = await penalidades.AvaliarAsync(item, FichaSemPenalidades(sheetId));
        return r with { Requisitos = a.Requisitos, RequisitosPendentes = a.RequisitosPendentes, Penalidade = a.Penalidade };
    }

    /// <summary>
    /// A Catálogo-linked weapon reads its display fields live from the Arma; a natural attack
    /// (ItemId null) has no catalog row to join, so it reads its own Manual* fields instead and
    /// reports Alcance/Critico/Rank/Durabilidade as null — it has none of those.
    /// </summary>
    private async Task<CreatureWeaponResponse> ToWeaponResponseAsync(CreatureWeapon weapon, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        if (weapon.ItemId is not null)
        {
            var item = await db.Set<Arma>().SingleAsync(a => a.Id == weapon.ItemId);
            var imageUrl = await ResolveImageUrlAsync(item.ImageId);
            var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(item.Rank, tabela);
            var resposta = new CreatureWeaponResponse(weapon.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeDano?.ToString(), item.Dados, item.Dano, item.Alcance, item.Critico, item.Rank?.ToString(), weapon.IsEquipped, weapon.DurabilidadeAtual is null ? null : DurabilidadeDeItem.LimitarAtual(weapon.DurabilidadeAtual.Value, maxima), maxima, imageUrl, item.Descricao, inquebravel);
            return await ComAvaliacaoAsync(resposta, item, weapon.CreatureSheetId);
        }

        // A manual (natural attack) weapon has no catalog Item to read an ImageUrl/Descricao from.
        return new CreatureWeaponResponse(weapon.Id.ToString(), null, weapon.ManualNome!, weapon.ManualTipoDeDano?.ToString(), weapon.ManualDados, weapon.ManualDano, null, null, null, weapon.IsEquipped, null, null, null, null);
    }

    private async Task<CreatureArmorSlotResponse> ToArmorSlotResponseAsync(CreatureArmorSlot slot, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        if (slot.ItemId is null)
            return new CreatureArmorSlotResponse(slot.Slot.ToString(), null, null, null, null, null, null, null, null, null, null, null);

        var item = await db.Set<Armadura>().SingleAsync(a => a.Id == slot.ItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(item.Rank, tabela);
        var resposta = new CreatureArmorSlotResponse(slot.Slot.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.Defesa, item.RF, item.RM, item.Peso, slot.DurabilidadeAtual is null ? null : DurabilidadeDeItem.LimitarAtual(slot.DurabilidadeAtual.Value, maxima), maxima, imageUrl, item.Descricao, inquebravel);
        return await ComAvaliacaoAsync(resposta, item, slot.CreatureSheetId);
    }

    private async Task<CreatureShieldResponse> ToShieldResponseAsync(CreatureShield shield, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        var item = await db.Set<Escudo>().SingleAsync(e => e.Id == shield.ItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(item.Rank, tabela);
        var resposta = new CreatureShieldResponse(shield.Id.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.BonusDefesa, item.Peso, shield.IsEquipped, DurabilidadeDeItem.LimitarAtual(shield.DurabilidadeAtual, maxima), maxima ?? 0, imageUrl, item.Descricao, inquebravel);
        return await ComAvaliacaoAsync(resposta, item, shield.CreatureSheetId);
    }

    private async Task<string?> ResolveImageUrlAsync(Guid? imageId)
    {
        if (imageId is null)
            return null;

        var image = await db.Images.FindAsync(imageId.Value);
        return image is not null ? $"/images/{image.Path}" : null;
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
