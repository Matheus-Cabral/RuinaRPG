using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Global (not per-GM) catalog of Equipagem kits — same treatment as HistoricosController. List
/// (GET) stays open; Create/Update/Delete are gated to the Rules Auditor, checked directly
/// against the DB, not a JWT claim.
/// </summary>
[ApiController]
[Route("api/equipment-kits")]
[Authorize]
public class EquipmentKitsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EquipmentKitResponse>>> List()
    {
        var kits = await db.EquipmentKits.Where(k => !k.IsDeleted).OrderBy(k => k.Nome).ToListAsync();
        var responses = new List<EquipmentKitResponse>();
        foreach (var kit in kits)
            responses.Add(await ToResponseAsync(kit));
        return responses;
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentKitResponse>> Create(CreateEquipmentKitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var validationError = ValidateRequest(request.Nome, request.Descricao, request.Ciclos, request.Items, request.ChoiceSlots, out var parsedItems, out var parsedSlots);
        if (validationError is not null)
            return validationError;

        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = request.Nome, Descricao = request.Descricao, Ciclos = request.Ciclos };
        db.EquipmentKits.Add(kit);
        AddChildren(kit.Id, parsedItems!, parsedSlots!);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(kit));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateEquipmentKitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var kit = await db.EquipmentKits.FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted);
        if (kit is null)
            return NotFound();

        var validationError = ValidateRequest(request.Nome, request.Descricao, request.Ciclos, request.Items, request.ChoiceSlots, out var parsedItems, out var parsedSlots);
        if (validationError is not null)
            return validationError;

        kit.Nome = request.Nome;
        kit.Descricao = request.Descricao;
        kit.Ciclos = request.Ciclos;

        db.EquipmentKitItems.RemoveRange(db.EquipmentKitItems.Where(i => i.KitId == id));
        db.EquipmentKitChoiceSlots.RemoveRange(db.EquipmentKitChoiceSlots.Where(s => s.KitId == id));
        AddChildren(kit.Id, parsedItems!, parsedSlots!);

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var kit = await db.EquipmentKits.FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted);
        if (kit is null)
            return NotFound();

        var inUse = await db.CharacterSheets.AnyAsync(s => s.EquipmentKitId == id)
            || await db.NpcSheets.AnyAsync(s => s.EquipmentKitId == id);
        if (inUse)
            return Conflict("Este kit de Equipagem está em uso em pelo menos uma ficha e não pode ser excluído.");

        kit.IsDeleted = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private void AddChildren(Guid kitId, List<EquipmentKitItem> items, List<EquipmentKitChoiceSlot> slots)
    {
        foreach (var item in items)
        {
            item.KitId = kitId;
            db.EquipmentKitItems.Add(item);
        }
        foreach (var slot in slots)
        {
            slot.KitId = kitId;
            db.EquipmentKitChoiceSlots.Add(slot);
        }
    }

    private ActionResult? ValidateRequest(string nome, string descricao, int ciclos,
        List<EquipmentKitItemInput> items, List<EquipmentKitChoiceSlotInput> choiceSlots,
        out List<EquipmentKitItem>? parsedItems, out List<EquipmentKitChoiceSlot>? parsedSlots)
    {
        parsedItems = null;
        parsedSlots = null;

        if (string.IsNullOrWhiteSpace(nome))
            return BadRequest("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(descricao))
            return BadRequest("Descrição é obrigatória.");
        if (ciclos < 0)
            return BadRequest("Ciclos não pode ser negativo.");

        var items2 = new List<EquipmentKitItem>();
        foreach (var item in items)
        {
            if (!Enum.TryParse<ItemTipo>(item.Tipo, out var tipo) || tipo == ItemTipo.Armadura)
                return BadRequest($"Tipo de item inválido: \"{item.Tipo}\". Armadura não é suportada em kits de Equipagem.");
            if (string.IsNullOrWhiteSpace(item.Nome))
                return BadRequest("Nome do item é obrigatório.");
            if (item.Qtd < 1)
                return BadRequest("Qtd do item deve ser pelo menos 1.");
            items2.Add(new EquipmentKitItem { Id = Guid.NewGuid(), Nome = item.Nome, Tipo = tipo, Qtd = item.Qtd, SubcategoriaHint = item.SubcategoriaHint });
        }

        var slots2 = new List<EquipmentKitChoiceSlot>();
        foreach (var slot in choiceSlots)
        {
            // EquipmentKitGrantService.ResolveEligibleOptionsAsync/BuildPlanAsync now dispatch on
            // slot.Tipo to the matching concrete Item subtype's DbSet — Armadura, Escudo, Artefato
            // and Arma are all real, resolvable choices; only Armadura also needs to know WHICH
            // ArmorSlot to fill (validated below), since armor doesn't insert a new row the way the
            // other 3 do.
            if (!TryParseExact<ItemTipo>(slot.Tipo, out var tipo) || tipo == ItemTipo.ItemGeral)
                return BadRequest($"Tipo de slot de escolha inválido: \"{slot.Tipo}\".");

            RuinaRPG.Domain.CharacterSheets.ArmorSlotType? armorSlot = null;
            if (tipo == ItemTipo.Armadura)
            {
                if (string.IsNullOrWhiteSpace(slot.ArmorSlot) || !TryParseExact<RuinaRPG.Domain.CharacterSheets.ArmorSlotType>(slot.ArmorSlot, out var parsedArmorSlot))
                    return BadRequest("Slots de escolha de Tipo=Armadura precisam de um ArmorSlot válido.");
                armorSlot = parsedArmorSlot;
            }
            else if (!string.IsNullOrWhiteSpace(slot.ArmorSlot))
            {
                return BadRequest("ArmorSlot só é aplicável a slots de escolha de Tipo=Armadura.");
            }
            if (string.IsNullOrWhiteSpace(slot.Label))
                return BadRequest("Label do slot de escolha é obrigatório.");
            if (slot.Qtd < 1)
                return BadRequest("Qtd do slot de escolha deve ser pelo menos 1.");
            Tier? tier = null;
            if (!string.IsNullOrWhiteSpace(slot.Tier))
            {
                if (!Enum.TryParse<Tier>(slot.Tier, out var parsedTier))
                    return BadRequest($"Tier inválido: \"{slot.Tier}\".");
                tier = parsedTier;
            }
            if ((slot.BonusSubcategoria is null) != (slot.BonusNome is null))
                return BadRequest("BonusSubcategoria e BonusNome devem ser informados juntos, ou nenhum dos dois.");
            if (slot.BonusNome is not null && (slot.BonusQtd is null || slot.BonusQtd < 1))
                return BadRequest("BonusQtd deve ser pelo menos 1 quando BonusNome é informado.");

            slots2.Add(new EquipmentKitChoiceSlot
            {
                Id = Guid.NewGuid(),
                Label = slot.Label,
                Tipo = tipo,
                SubcategoriasCsv = slot.Subcategorias is null ? null : string.Join(",", slot.Subcategorias),
                Tier = tier,
                Qtd = slot.Qtd,
                BonusSubcategoria = slot.BonusSubcategoria,
                BonusNome = slot.BonusNome,
                BonusQtd = slot.BonusQtd,
                ArmorSlot = armorSlot,
            });
        }

        parsedItems = items2;
        parsedSlots = slots2;
        return null;
    }

    private async Task<EquipmentKitResponse> ToResponseAsync(EquipmentKit kit)
    {
        var items = await db.EquipmentKitItems.Where(i => i.KitId == kit.Id).ToListAsync();
        var slots = await db.EquipmentKitChoiceSlots.Where(s => s.KitId == kit.Id).ToListAsync();

        return new EquipmentKitResponse(kit.Id.ToString(), kit.Nome, kit.Descricao, kit.Ciclos,
            items.Select(i => new EquipmentKitItemResponse(i.Id.ToString(), i.Nome, i.Tipo.ToString(), i.Qtd, i.SubcategoriaHint)).ToList(),
            slots.Select(s => new EquipmentKitChoiceSlotResponse(s.Id.ToString(), s.Label, s.Tipo.ToString(),
                s.SubcategoriasCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                s.Tier?.ToString(), s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd, s.ArmorSlot?.ToString())).ToList());
    }

    // Enum.TryParse alone is lenient — it accepts numeric strings ("99"), comma-joined names
    // ("Arma, Escudo") and stray whitespace, silently mapping them to an underlying int value
    // that may not even be a defined enum member. Choice-slot Tipo and ArmorSlot must only ever
    // accept an exact (ordinal, case-sensitive) match to a defined member name.
    private static bool TryParseExact<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum =>
        Enum.TryParse(value, out result) && Enum.IsDefined(result) && result.ToString() == value;

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
