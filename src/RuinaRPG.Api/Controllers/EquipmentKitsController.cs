using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using static RuinaRPG.Api.Controllers.EnumParsing;

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

        var (validationError, parsedItems, parsedSlots) = await ValidateRequestAsync(request.Nome, request.Descricao, request.Ciclos, request.Items, request.ChoiceSlots);
        if (validationError is not null)
            return validationError;

        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = request.Nome, Descricao = request.Descricao, Ciclos = request.Ciclos };
        db.EquipmentKits.Add(kit);
        AddChildren(kit.Id, parsedItems, parsedSlots);
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

        var (validationError, parsedItems, parsedSlots) = await ValidateRequestAsync(request.Nome, request.Descricao, request.Ciclos, request.Items, request.ChoiceSlots);
        if (validationError is not null)
            return validationError;

        kit.Nome = request.Nome;
        kit.Descricao = request.Descricao;
        kit.Ciclos = request.Ciclos;

        db.EquipmentKitItems.RemoveRange(db.EquipmentKitItems.Where(i => i.KitId == id));
        db.EquipmentKitChoiceSlots.RemoveRange(db.EquipmentKitChoiceSlots.Where(s => s.KitId == id));
        AddChildren(kit.Id, parsedItems, parsedSlots);

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

    private async Task<(ActionResult? Error, List<EquipmentKitItem> Items, List<EquipmentKitChoiceSlot> Slots)> ValidateRequestAsync(
        string nome, string descricao, int ciclos, List<EquipmentKitItemInput> items, List<EquipmentKitChoiceSlotInput> choiceSlots)
    {
        (ActionResult? Error, List<EquipmentKitItem> Items, List<EquipmentKitChoiceSlot> Slots) Fail(string message) => (BadRequest(message), [], []);

        if (string.IsNullOrWhiteSpace(nome))
            return Fail("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(descricao))
            return Fail("Descrição é obrigatória.");
        if (ciclos < 0)
            return Fail("Ciclos não pode ser negativo.");

        var referenciados = items.Select(i => i.FixedItemId).Concat(choiceSlots.Select(s => s.BonusFixedItemId))
            .Select(raw => Guid.TryParse(raw, out var id) ? id : Guid.Empty).ToList();
        var fixos = await db.EquipmentKitFixedItems.Where(f => referenciados.Contains(f.Id)).ToDictionaryAsync(f => f.Id);

        // Uma Armadura de kit (item fixo ou slot de escolha) substitui o que já estiver no ArmorSlot ao aplicar o kit;
        // duas apontando para o mesmo ArmorSlot fariam a segunda sobrescrever a primeira em silêncio, então o conjunto é
        // compartilhado pelos dois laços e a repetição é rejeitada aqui.
        var seenArmorSlots = new HashSet<ArmorSlotType>();

        var items2 = new List<EquipmentKitItem>();
        foreach (var item in items)
        {
            if (!Guid.TryParse(item.FixedItemId, out var fixedId) || !fixos.TryGetValue(fixedId, out var fixo))
                return Fail("Item fixo não encontrado na base de itens fixos.");
            if (item.Qtd < 1)
                return Fail("Qtd do item deve ser pelo menos 1.");

            ArmorSlotType? armorSlot = null;
            if (fixo.Tipo == ItemTipo.Armadura)
            {
                if (string.IsNullOrWhiteSpace(item.ArmorSlot) || !TryParseExact<ArmorSlotType>(item.ArmorSlot, out var parsed))
                    return Fail("Um item fixo de Tipo=Armadura precisa de um ArmorSlot válido.");
                if (!seenArmorSlots.Add(parsed))
                    return Fail($"Mais de uma Armadura do kit aponta para o mesmo ArmorSlot \"{item.ArmorSlot}\".");
                armorSlot = parsed;
            }
            else if (!string.IsNullOrWhiteSpace(item.ArmorSlot))
            {
                return Fail("ArmorSlot só é aplicável a itens fixos de Tipo=Armadura.");
            }
            // Nome/Tipo legados seguem preenchidos a partir do item fixo para a linha continuar legível; a fonte é FixedItemId.
            items2.Add(new EquipmentKitItem { Id = Guid.NewGuid(), FixedItemId = fixo.Id, Nome = fixo.Nome, Tipo = fixo.Tipo, Qtd = item.Qtd, ArmorSlot = armorSlot });
        }

        var slots2 = new List<EquipmentKitChoiceSlot>();
        foreach (var slot in choiceSlots)
        {
            // EquipmentKitGrantService.ResolveEligibleOptionsAsync/BuildPlanAsync despacham por slot.Tipo para o DbSet do
            // subtipo concreto — Armadura, Escudo, Artefato e Arma são escolhas reais; só a Armadura também precisa saber QUAL
            // ArmorSlot preencher (validado abaixo), já que armadura não insere uma linha nova como os outros 3.
            if (!TryParseExact<ItemTipo>(slot.Tipo, out var tipo) || tipo == ItemTipo.ItemGeral)
                return Fail($"Tipo de slot de escolha inválido: \"{slot.Tipo}\".");

            ArmorSlotType? armorSlot = null;
            if (tipo == ItemTipo.Armadura)
            {
                if (string.IsNullOrWhiteSpace(slot.ArmorSlot) || !TryParseExact<ArmorSlotType>(slot.ArmorSlot, out var parsedArmorSlot))
                    return Fail("Slots de escolha de Tipo=Armadura precisam de um ArmorSlot válido.");
                if (!seenArmorSlots.Add(parsedArmorSlot))
                    return Fail($"Mais de um slot de escolha de Tipo=Armadura aponta para o mesmo ArmorSlot \"{slot.ArmorSlot}\".");
                armorSlot = parsedArmorSlot;
            }
            else if (!string.IsNullOrWhiteSpace(slot.ArmorSlot))
            {
                return Fail("ArmorSlot só é aplicável a slots de escolha de Tipo=Armadura.");
            }
            if (string.IsNullOrWhiteSpace(slot.Label))
                return Fail("Label do slot de escolha é obrigatório.");
            if (slot.Qtd < 1)
                return Fail("Qtd do slot de escolha deve ser pelo menos 1.");
            // Rank só significa algo num slot de Arma (ResolveEligibleOptionsAsync só aplica o filtro no ramo Arma) — um Rank
            // preenchido nos outros seria ignorado em silêncio ao resolver, então é rejeitado aqui.
            if (tipo != ItemTipo.Arma && !string.IsNullOrWhiteSpace(slot.Rank))
                return Fail("Rank só é aplicável a slots de escolha de Tipo=Arma.");
            RankDeItem? rank = null;
            if (!string.IsNullOrWhiteSpace(slot.Rank))
            {
                // Nome exato do enum: Enum.TryParse também aceitaria "3"/"99" (valores numéricos).
                if (!Enum.GetNames<RankDeItem>().Contains(slot.Rank) || !Enum.TryParse<RankDeItem>(slot.Rank, out var parsedRank))
                    return Fail($"Rank inválido: \"{slot.Rank}\".");
                rank = parsedRank;
            }

            var (bonusError, bonusFixo) = ValidateBonus(slot, fixos);
            if (bonusError is not null)
                return Fail(bonusError);

            slots2.Add(new EquipmentKitChoiceSlot
            {
                Id = Guid.NewGuid(),
                Label = slot.Label,
                Tipo = tipo,
                SubcategoriasCsv = slot.Subcategorias is null ? null : string.Join(",", slot.Subcategorias),
                Rank = rank,
                Qtd = slot.Qtd,
                BonusSubcategoria = slot.BonusSubcategoria,
                BonusFixedItemId = bonusFixo?.Id,
                BonusNome = bonusFixo?.Nome,
                BonusQtd = slot.BonusQtd,
                ArmorSlot = armorSlot,
            });
        }

        return (null, items2, slots2);
    }

    /// <summary>O bônus condicional de um slot: BonusSubcategoria e BonusFixedItemId juntos (ou nenhum), um item fixo do tipo Item Geral, com BonusQtd ≥ 1.</summary>
    private static (string? Error, EquipmentKitFixedItem? Fixo) ValidateBonus(EquipmentKitChoiceSlotInput slot, Dictionary<Guid, EquipmentKitFixedItem> fixos)
    {
        if ((slot.BonusSubcategoria is null) != (slot.BonusFixedItemId is null))
            return ("BonusSubcategoria e BonusFixedItemId devem ser informados juntos, ou nenhum dos dois.", null);
        if (slot.BonusFixedItemId is null)
            return (null, null);

        if (!Guid.TryParse(slot.BonusFixedItemId, out var bonusId) || !fixos.TryGetValue(bonusId, out var fixo))
            return ("Item fixo não encontrado na base de itens fixos.", null);
        if (fixo.Tipo != ItemTipo.ItemGeral)
            return ("O bônus de um slot precisa ser um item fixo do tipo Item Geral.", null);
        if (slot.BonusQtd is null || slot.BonusQtd < 1)
            return ("BonusQtd deve ser pelo menos 1 quando o bônus é informado.", null);
        return (null, fixo);
    }

    private async Task<EquipmentKitResponse> ToResponseAsync(EquipmentKit kit)
    {
        var items = await db.EquipmentKitItems.Where(i => i.KitId == kit.Id).ToListAsync();
        var slots = await db.EquipmentKitChoiceSlots.Where(s => s.KitId == kit.Id).ToListAsync();
        var fixoIds = items.Select(i => i.FixedItemId).Concat(slots.Select(s => s.BonusFixedItemId)).OfType<Guid>().ToList();
        var fixos = await db.EquipmentKitFixedItems.AsNoTracking().Where(f => fixoIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id);

        return new EquipmentKitResponse(kit.Id.ToString(), kit.Nome, kit.Descricao, kit.Ciclos,
            items.Select(i => ToItemResponse(i, fixos)).ToList(),
            slots.Select(s => new EquipmentKitChoiceSlotResponse(s.Id.ToString(), s.Label, s.Tipo.ToString(),
                s.SubcategoriasCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                s.Rank?.ToString(), s.Qtd, s.BonusSubcategoria,
                s.BonusFixedItemId is { } bonusId && fixos.TryGetValue(bonusId, out var bonus) ? bonus.Nome : s.BonusNome,
                s.BonusQtd, s.ArmorSlot?.ToString(), s.BonusFixedItemId?.ToString())).ToList());
    }

    /// <summary>Uma linha legada (sem FixedItemId, antes de rodar a conversão) sai com o Nome/Tipo antigos e DetalhesIncompletos = true.</summary>
    private static EquipmentKitItemResponse ToItemResponse(EquipmentKitItem i, Dictionary<Guid, EquipmentKitFixedItem> fixos) =>
        i.FixedItemId is { } fixedId && fixos.TryGetValue(fixedId, out var fixo)
            ? new EquipmentKitItemResponse(i.Id.ToString(), fixedId.ToString(), fixo.Nome, fixo.Tipo.ToString(), i.Qtd, i.ArmorSlot?.ToString(), fixo.DetalhesIncompletos)
            : new EquipmentKitItemResponse(i.Id.ToString(), "", i.Nome ?? "", i.Tipo.ToString(), i.Qtd, i.ArmorSlot?.ToString(), true);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
