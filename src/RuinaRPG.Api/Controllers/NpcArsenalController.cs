using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/npc-sheets/{sheetId}")]
public class NpcArsenalController(RuinaRpgDbContext db) : ControllerBase
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

        var weapon = new NpcWeapon { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = item.DurabilidadeMaxima ?? 0 };
        db.NpcWeapons.Add(weapon);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToWeaponResponseAsync(weapon));
    }

    [HttpGet("weapons")]
    public async Task<ActionResult<List<NpcWeaponResponse>>> ListWeapons(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapons = await db.NpcWeapons.Where(w => w.NpcSheetId == sheetId).ToListAsync();
        var responses = new List<NpcWeaponResponse>();
        foreach (var weapon in weapons)
            responses.Add(await ToWeaponResponseAsync(weapon));
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
        weapon.DurabilidadeAtual = Math.Min(durabilidadeAtual, item.DurabilidadeMaxima ?? 0);
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
        var responses = new List<NpcArmorSlotResponse>();
        foreach (var slot in slots)
            responses.Add(await ToArmorSlotResponseAsync(slot));
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
            armorSlot.DurabilidadeAtual = item.DurabilidadeMaxima ?? 0;
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
        armorSlot.DurabilidadeAtual = Math.Min(durabilidadeAtual, item.DurabilidadeMaxima ?? 0);
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

        var shield = new NpcShield { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = item.DurabilidadeMaxima ?? 0 };
        db.NpcShields.Add(shield);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToShieldResponseAsync(shield));
    }

    [HttpGet("shields")]
    public async Task<ActionResult<List<NpcShieldResponse>>> ListShields(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shields = await db.NpcShields.Where(s => s.NpcSheetId == sheetId).ToListAsync();
        var responses = new List<NpcShieldResponse>();
        foreach (var shield in shields)
            responses.Add(await ToShieldResponseAsync(shield));
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
        shield.DurabilidadeAtual = Math.Min(durabilidadeAtual, item.DurabilidadeMaxima ?? 0);
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

        if (sheet.GmId != CurrentGmId())
            return NotFound();

        return null;
    }

    private async Task<NpcWeaponResponse> ToWeaponResponseAsync(NpcWeapon weapon)
    {
        var item = await db.Set<Arma>().SingleAsync(a => a.Id == weapon.ItemId);
        return new NpcWeaponResponse(weapon.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeDano?.ToString(), item.Alcance, item.Dados, item.Dano, item.Critico, item.Tier?.ToString(), item.Peso, weapon.IsEquipped, weapon.DurabilidadeAtual, item.DurabilidadeMaxima ?? 0);
    }

    private async Task<NpcArmorSlotResponse> ToArmorSlotResponseAsync(NpcArmorSlot slot)
    {
        if (slot.ItemId is null)
            return new NpcArmorSlotResponse(slot.Slot.ToString(), null, null, null, null, null, null, null, null, null, null, null);

        var item = await db.Set<Armadura>().SingleAsync(a => a.Id == slot.ItemId);
        return new NpcArmorSlotResponse(slot.Slot.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.Defesa, item.RF, item.RM, item.Penalidade, item.RequisitoVigor, item.Peso, slot.DurabilidadeAtual, item.DurabilidadeMaxima);
    }

    private async Task<NpcShieldResponse> ToShieldResponseAsync(NpcShield shield)
    {
        var item = await db.Set<Escudo>().SingleAsync(e => e.Id == shield.ItemId);
        return new NpcShieldResponse(shield.Id.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.BonusDefesa, item.Penalidade, item.RequisitoVigor, item.Peso, shield.IsEquipped, shield.DurabilidadeAtual, item.DurabilidadeMaxima ?? 0);
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
