using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public class NpcArsenalController(RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades, RuinaRPG.Api.Services.EquipmentPenaltyService penalidades, RuinaRPG.Api.Services.NpcSheetStats stats) : ControllerBase
{
    [HttpPost("weapons")]
    public async Task<ActionResult<NpcWeaponResponse>> AddWeapon(Guid sheetId, AddNpcWeaponRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Set<Arma>().FirstOrDefaultAsync(a => a.Id == itemId);
        if (item is null)
            return BadRequest("Item de arma não encontrado.");

        var weapon = new NpcWeapon { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = (await durabilidades.ResolverAsync(item.Rank)).Maxima ?? 0 };
        db.NpcWeapons.Add(weapon);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToWeaponResponseAsync(weapon, await durabilidades.TabelaAsync()));
    }

    [HttpGet("weapons")]
    public async Task<ActionResult<List<NpcWeaponResponse>>> ListWeapons(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapons = await db.NpcWeapons.Where(w => w.NpcSheetId == sheetId).ToListAsync();
        var tabela = await durabilidades.TabelaAsync();
        var responses = new List<NpcWeaponResponse>();
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

        var weapon = await db.NpcWeapons.FirstOrDefaultAsync(w => w.Id == id && w.NpcSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        if (isEquipped)
        {
            // Default rule (no Ambidestria exception — see plan out-of-scope): only 1 weapon equipped at a time.
            var currentlyEquipped = await db.NpcWeapons.Where(w => w.NpcSheetId == sheetId && w.IsEquipped).ToListAsync();
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

        var weapon = await db.NpcWeapons.FirstOrDefaultAsync(w => w.Id == id && w.NpcSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        // "Atual ... não pode exceder o Máximo" (Ficha de Personagem 3.a, aplicado a NPC por R0002).
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

        var weapon = await db.NpcWeapons.FirstOrDefaultAsync(w => w.Id == id && w.NpcSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        db.NpcWeapons.Remove(weapon);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("armor-slots")]
    public async Task<ActionResult<List<NpcArmorSlotResponse>>> ListArmorSlots(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var slots = await db.NpcArmorSlots.Where(a => a.NpcSheetId == sheetId).ToListAsync();
        var tabela = await durabilidades.TabelaAsync();
        var responses = new List<NpcArmorSlotResponse>();
        foreach (var slot in slots)
            responses.Add(await ToArmorSlotResponseAsync(slot, tabela));
        return responses;
    }

    [HttpPut("armor-slots/{slot}")]
    public async Task<IActionResult> UpdateArmorSlot(Guid sheetId, ArmorSlotType slot, UpdateNpcArmorSlotRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var armorSlot = await db.NpcArmorSlots.SingleAsync(a => a.NpcSheetId == sheetId && a.Slot == slot);

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

        var armorSlot = await db.NpcArmorSlots.SingleAsync(a => a.NpcSheetId == sheetId && a.Slot == slot);
        if (armorSlot.ItemId is null)
            return BadRequest("Nenhuma armadura equipada nesse slot.");

        var item = await db.Set<Armadura>().SingleAsync(a => a.Id == armorSlot.ItemId);
        armorSlot.DurabilidadeAtual = DurabilidadeDeItem.LimitarAtual(durabilidadeAtual, (await durabilidades.ResolverAsync(item.Rank)).Maxima);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("shields")]
    public async Task<ActionResult<NpcShieldResponse>> AddShield(Guid sheetId, AddNpcShieldRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Set<Escudo>().FirstOrDefaultAsync(e => e.Id == itemId);
        if (item is null)
            return BadRequest("Item de escudo não encontrado.");

        var shield = new NpcShield { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = (await durabilidades.ResolverAsync(item.Rank)).Maxima ?? 0 };
        db.NpcShields.Add(shield);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToShieldResponseAsync(shield, await durabilidades.TabelaAsync()));
    }

    [HttpGet("shields")]
    public async Task<ActionResult<List<NpcShieldResponse>>> ListShields(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shields = await db.NpcShields.Where(s => s.NpcSheetId == sheetId).ToListAsync();
        var tabela = await durabilidades.TabelaAsync();
        var responses = new List<NpcShieldResponse>();
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

        var shield = await db.NpcShields.FirstOrDefaultAsync(s => s.Id == id && s.NpcSheetId == sheetId);
        if (shield is null)
            return NotFound();

        if (isEquipped)
        {
            var currentlyEquipped = await db.NpcShields.Where(s => s.NpcSheetId == sheetId && s.IsEquipped).ToListAsync();
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

        var shield = await db.NpcShields.FirstOrDefaultAsync(s => s.Id == id && s.NpcSheetId == sheetId);
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

        var shield = await db.NpcShields.FirstOrDefaultAsync(s => s.Id == id && s.NpcSheetId == sheetId);
        if (shield is null)
            return NotFound();

        db.NpcShields.Remove(shield);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> CheckAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
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
        await stats.FichaParaRequisitosAsync((await db.NpcSheets.FindAsync(sheetId))!);

    private async Task<NpcWeaponResponse> ComAvaliacaoAsync(NpcWeaponResponse r, Item item, Guid sheetId)
    {
        var a = await penalidades.AvaliarAsync(item, FichaSemPenalidades(sheetId));
        return r with { Requisitos = a.Requisitos, RequisitosPendentes = a.RequisitosPendentes, Penalidade = a.Penalidade, OutrasPenalidades = a.OutrasPenalidades };
    }

    private async Task<NpcArmorSlotResponse> ComAvaliacaoAsync(NpcArmorSlotResponse r, Item item, Guid sheetId)
    {
        var a = await penalidades.AvaliarAsync(item, FichaSemPenalidades(sheetId));
        return r with { Requisitos = a.Requisitos, RequisitosPendentes = a.RequisitosPendentes, Penalidade = a.Penalidade, OutrasPenalidades = a.OutrasPenalidades };
    }

    private async Task<NpcShieldResponse> ComAvaliacaoAsync(NpcShieldResponse r, Item item, Guid sheetId)
    {
        var a = await penalidades.AvaliarAsync(item, FichaSemPenalidades(sheetId));
        return r with { Requisitos = a.Requisitos, RequisitosPendentes = a.RequisitosPendentes, Penalidade = a.Penalidade, OutrasPenalidades = a.OutrasPenalidades };
    }

    private async Task<NpcWeaponResponse> ToWeaponResponseAsync(NpcWeapon weapon, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        var item = await db.Set<Arma>().SingleAsync(a => a.Id == weapon.ItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(item.Rank, tabela);
        var resposta = new NpcWeaponResponse(weapon.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeDano?.ToString(), item.Alcance, item.Dados, item.Dano, item.Critico, item.Rank?.ToString(), item.Peso, weapon.IsEquipped, DurabilidadeDeItem.LimitarAtual(weapon.DurabilidadeAtual, maxima), maxima ?? 0, imageUrl, item.Descricao, inquebravel);
        return await ComAvaliacaoAsync(resposta, item, weapon.NpcSheetId);
    }

    private async Task<NpcArmorSlotResponse> ToArmorSlotResponseAsync(NpcArmorSlot slot, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        if (slot.ItemId is null)
            return new NpcArmorSlotResponse(slot.Slot.ToString(), null, null, null, null, null, null, null, null, null, null, null);

        var item = await db.Set<Armadura>().SingleAsync(a => a.Id == slot.ItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(item.Rank, tabela);
        var resposta = new NpcArmorSlotResponse(slot.Slot.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.Defesa, item.RF, item.RM, item.Peso, slot.DurabilidadeAtual is null ? null : DurabilidadeDeItem.LimitarAtual(slot.DurabilidadeAtual.Value, maxima), maxima, imageUrl, item.Descricao, inquebravel);
        return await ComAvaliacaoAsync(resposta, item, slot.NpcSheetId);
    }

    private async Task<NpcShieldResponse> ToShieldResponseAsync(NpcShield shield, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        var item = await db.Set<Escudo>().SingleAsync(e => e.Id == shield.ItemId);
        var imageUrl = await ResolveImageUrlAsync(item.ImageId);
        var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(item.Rank, tabela);
        var resposta = new NpcShieldResponse(shield.Id.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.BonusDefesa, item.Peso, shield.IsEquipped, DurabilidadeDeItem.LimitarAtual(shield.DurabilidadeAtual, maxima), maxima ?? 0, imageUrl, item.Descricao, inquebravel);
        return await ComAvaliacaoAsync(resposta, item, shield.NpcSheetId);
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
