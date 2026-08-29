using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public class CharacterArsenalController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost("weapons")]
    public async Task<ActionResult<CharacterWeaponResponse>> AddWeapon(Guid sheetId, AddCharacterWeaponRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Set<Arma>().FirstOrDefaultAsync(a => a.Id == itemId);
        if (item is null)
            return BadRequest("Item de arma não encontrado.");

        var weapon = new CharacterWeapon { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = item.DurabilidadeMaxima ?? 0 };
        db.CharacterWeapons.Add(weapon);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToWeaponResponseAsync(weapon));
    }

    [HttpGet("weapons")]
    public async Task<ActionResult<List<CharacterWeaponResponse>>> ListWeapons(Guid sheetId)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapons = await db.CharacterWeapons.Where(w => w.CharacterSheetId == sheetId).ToListAsync();
        var responses = new List<CharacterWeaponResponse>();
        foreach (var weapon in weapons)
            responses.Add(await ToWeaponResponseAsync(weapon));
        return responses;
    }

    [HttpPut("weapons/{id}")]
    public async Task<IActionResult> UpdateWeapon(Guid sheetId, Guid id, [FromBody] bool isEquipped)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapon = await db.CharacterWeapons.FirstOrDefaultAsync(w => w.Id == id && w.CharacterSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        if (isEquipped)
        {
            // Default rule (no Ambidestria exception — see plan out-of-scope): only 1 weapon equipped at a time.
            var currentlyEquipped = await db.CharacterWeapons.Where(w => w.CharacterSheetId == sheetId && w.IsEquipped).ToListAsync();
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
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapon = await db.CharacterWeapons.FirstOrDefaultAsync(w => w.Id == id && w.CharacterSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        // "Atual ... não pode exceder o Máximo" (Ficha de Personagem 3.a).
        var item = await db.Set<Arma>().SingleAsync(a => a.Id == weapon.ItemId);
        weapon.DurabilidadeAtual = Math.Min(durabilidadeAtual, item.DurabilidadeMaxima ?? 0);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("weapons/{id}")]
    public async Task<IActionResult> DeleteWeapon(Guid sheetId, Guid id)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapon = await db.CharacterWeapons.FirstOrDefaultAsync(w => w.Id == id && w.CharacterSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        db.CharacterWeapons.Remove(weapon);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("armor-slots")]
    public async Task<ActionResult<List<CharacterArmorSlotResponse>>> ListArmorSlots(Guid sheetId)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var slots = await db.CharacterArmorSlots.Where(a => a.CharacterSheetId == sheetId).ToListAsync();
        var responses = new List<CharacterArmorSlotResponse>();
        foreach (var slot in slots)
            responses.Add(await ToArmorSlotResponseAsync(slot));
        return responses;
    }

    [HttpPut("armor-slots/{slot}")]
    public async Task<IActionResult> UpdateArmorSlot(Guid sheetId, ArmorSlotType slot, UpdateCharacterArmorSlotRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var armorSlot = await db.CharacterArmorSlots.SingleAsync(a => a.CharacterSheetId == sheetId && a.Slot == slot);

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
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var armorSlot = await db.CharacterArmorSlots.SingleAsync(a => a.CharacterSheetId == sheetId && a.Slot == slot);
        if (armorSlot.ItemId is null)
            return BadRequest("Nenhuma armadura equipada nesse slot.");

        // "Atual ... não pode exceder o Máximo" (Ficha de Personagem 3.b).
        var item = await db.Set<Armadura>().SingleAsync(a => a.Id == armorSlot.ItemId);
        armorSlot.DurabilidadeAtual = Math.Min(durabilidadeAtual, item.DurabilidadeMaxima ?? 0);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("shields")]
    public async Task<ActionResult<CharacterShieldResponse>> AddShield(Guid sheetId, AddCharacterShieldRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Set<Escudo>().FirstOrDefaultAsync(e => e.Id == itemId);
        if (item is null)
            return BadRequest("Item de escudo não encontrado.");

        var shield = new CharacterShield { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = item.DurabilidadeMaxima ?? 0 };
        db.CharacterShields.Add(shield);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToShieldResponseAsync(shield));
    }

    [HttpGet("shields")]
    public async Task<ActionResult<List<CharacterShieldResponse>>> ListShields(Guid sheetId)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shields = await db.CharacterShields.Where(s => s.CharacterSheetId == sheetId).ToListAsync();
        var responses = new List<CharacterShieldResponse>();
        foreach (var shield in shields)
            responses.Add(await ToShieldResponseAsync(shield));
        return responses;
    }

    [HttpPut("shields/{id}")]
    public async Task<IActionResult> UpdateShield(Guid sheetId, Guid id, [FromBody] bool isEquipped)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shield = await db.CharacterShields.FirstOrDefaultAsync(s => s.Id == id && s.CharacterSheetId == sheetId);
        if (shield is null)
            return NotFound();

        if (isEquipped)
        {
            // Same "1 equipped at a time" default rule as weapons (UpdateWeapon) — no exception modeled yet.
            var currentlyEquipped = await db.CharacterShields.Where(s => s.CharacterSheetId == sheetId && s.IsEquipped).ToListAsync();
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
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shield = await db.CharacterShields.FirstOrDefaultAsync(s => s.Id == id && s.CharacterSheetId == sheetId);
        if (shield is null)
            return NotFound();

        // "Atual ... não pode exceder o Máximo" (Ficha de Personagem 3.c).
        var item = await db.Set<Escudo>().SingleAsync(e => e.Id == shield.ItemId);
        shield.DurabilidadeAtual = Math.Min(durabilidadeAtual, item.DurabilidadeMaxima ?? 0);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("shields/{id}")]
    public async Task<IActionResult> DeleteShield(Guid sheetId, Guid id)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var shield = await db.CharacterShields.FirstOrDefaultAsync(s => s.Id == id && s.CharacterSheetId == sheetId);
        if (shield is null)
            return NotFound();

        db.CharacterShields.Remove(shield);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> CheckEditAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        return null;
    }

    private async Task<CharacterWeaponResponse> ToWeaponResponseAsync(CharacterWeapon weapon)
    {
        var item = await db.Set<Arma>().SingleAsync(a => a.Id == weapon.ItemId);
        return new CharacterWeaponResponse(weapon.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeDano?.ToString(), item.Alcance, item.Dados, item.Dano, item.Critico, item.Tier?.ToString(), item.Peso, weapon.IsEquipped, weapon.DurabilidadeAtual, item.DurabilidadeMaxima ?? 0);
    }

    private async Task<CharacterArmorSlotResponse> ToArmorSlotResponseAsync(CharacterArmorSlot slot)
    {
        if (slot.ItemId is null)
            return new CharacterArmorSlotResponse(slot.Slot.ToString(), null, null, null, null, null, null, null, null, null, null, null);

        var item = await db.Set<Armadura>().SingleAsync(a => a.Id == slot.ItemId);
        return new CharacterArmorSlotResponse(slot.Slot.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.Defesa, item.RF, item.RM, item.Penalidade, item.RequisitoVigor, item.Peso, slot.DurabilidadeAtual, item.DurabilidadeMaxima);
    }

    private async Task<CharacterShieldResponse> ToShieldResponseAsync(CharacterShield shield)
    {
        var item = await db.Set<Escudo>().SingleAsync(e => e.Id == shield.ItemId);
        return new CharacterShieldResponse(shield.Id.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.BonusDefesa, item.Penalidade, item.RequisitoVigor, item.Peso, shield.IsEquipped, shield.DurabilidadeAtual, item.DurabilidadeMaxima ?? 0);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
