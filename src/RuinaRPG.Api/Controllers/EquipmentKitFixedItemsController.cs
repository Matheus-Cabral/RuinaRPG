using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Base global de itens fixos dos kits de Equipagem (itens completos, não por GM). Os kits apontam para
/// ela por FixedItemId; ao aplicar um kit, o item vira uma cópia independente no catálogo do GM. Os
/// quatro endpoints são só do Auditor de Regras, checado direto no banco (como EquipmentKitsController).
/// </summary>
[ApiController]
[Route("api/equipment-kit-fixed-items")]
[Authorize]
public class EquipmentKitFixedItemsController(RuinaRpgDbContext db, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EquipmentKitFixedItemResponse>>> List([FromQuery] string? tipo, [FromQuery] string? q)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var query = db.EquipmentKitFixedItems.AsQueryable();
        if (!string.IsNullOrEmpty(tipo))
        {
            if (!Enum.TryParse<ItemTipo>(tipo, out var tipoFiltro) || !Enum.IsDefined(tipoFiltro))
                return BadRequest("Tipo de item desconhecido.");
            query = query.Where(f => f.Tipo == tipoFiltro);
        }
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(f => EF.Functions.ILike(f.Nome, $"%{q}%"));

        var fixos = await query.OrderBy(f => f.Nome).ToListAsync();
        var porId = await pericias.PorIdAsync();
        var kits = await KitsPorItemAsync(fixos.Select(f => f.Id).ToList());
        return fixos.Select(f => ToResponse(f, porId, kits.GetValueOrDefault(f.Id, []))).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentKitFixedItemResponse>> Create(CreateItemRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        if (!Enum.TryParse<ItemTipo>(request.Tipo, out var tipo) || !Enum.IsDefined(tipo))
            return BadRequest("Tipo de item desconhecido.");

        var (erro, requisitos, penalidade) = await ValidarAsync(tipo, request);
        if (erro is not null)
            return BadRequest(erro);

        var nome = request.Nome.Trim();
        if (await db.EquipmentKitFixedItems.AnyAsync(f => f.Nome == nome && f.Tipo == tipo))
            return Conflict("Já existe um item fixo com este nome e tipo.");

        var fixo = new EquipmentKitFixedItem
        {
            Id = Guid.NewGuid(), Nome = nome, Tipo = tipo,
            Dados = ItemFactory.Serializar(request with { Nome = nome }),
            Requisitos = requisitos, PenalidadeDeRequisitos = penalidade,
        };
        db.EquipmentKitFixedItems.Add(fixo);
        await db.SaveChangesAsync();
        return Created(string.Empty, ToResponse(fixo, await pericias.PorIdAsync(), []));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, CreateItemRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var fixo = await db.EquipmentKitFixedItems.FirstOrDefaultAsync(f => f.Id == id);
        if (fixo is null)
            return NotFound();
        if (!Enum.TryParse<ItemTipo>(request.Tipo, out var tipo) || !Enum.IsDefined(tipo))
            return BadRequest("Tipo de item desconhecido.");
        if (tipo != fixo.Tipo)
            return BadRequest("O Tipo de um item fixo não pode ser alterado.");

        var (erro, requisitos, penalidade) = await ValidarAsync(tipo, request);
        if (erro is not null)
            return BadRequest(erro);

        var nome = request.Nome.Trim();
        if (await db.EquipmentKitFixedItems.AnyAsync(f => f.Id != id && f.Nome == nome && f.Tipo == tipo))
            return Conflict("Já existe um item fixo com este nome e tipo.");

        fixo.Nome = nome;
        fixo.Dados = ItemFactory.Serializar(request with { Nome = nome });
        fixo.Requisitos = requisitos;
        fixo.PenalidadeDeRequisitos = penalidade;
        fixo.DetalhesIncompletos = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var fixo = await db.EquipmentKitFixedItems.FirstOrDefaultAsync(f => f.Id == id);
        if (fixo is null)
            return NotFound();

        var kits = await KitsPorItemAsync([id]);
        if (kits.TryGetValue(id, out var nomes))
            return Conflict($"Este item fixo é usado nos kits: {string.Join(", ", nomes)}.");

        // Kits excluídos (soft delete) ainda guardam a linha; soltar o vínculo evita a violação da FK Restrict.
        var linhasDeKitsExcluidos = await db.EquipmentKitItems.Where(i => i.FixedItemId == id).ToListAsync();
        foreach (var linha in linhasDeKitsExcluidos)
            linha.FixedItemId = null;
        var bonusDeKitsExcluidos = await db.EquipmentKitChoiceSlots.Where(s => s.BonusFixedItemId == id).ToListAsync();
        foreach (var slot in bonusDeKitsExcluidos)
            slot.BonusFixedItemId = null;

        db.EquipmentKitFixedItems.Remove(fixo);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Mesma validação do Catálogo (ItemsController): nome, Rank e Requisitos/Penalidade.</summary>
    private async Task<(string? Erro, RequisitosDePassiva? Requisitos, PenalidadeDeEquipamento? Penalidade)> ValidarAsync(ItemTipo tipo, CreateItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return ("Nome é obrigatório.", null, null);
        if (ItemFactory.TemRank(tipo) && !ItemFactory.TryParseRank(request.Rank, out _))
            return ($"Rank inválido: \"{request.Rank}\".", null, null);
        var porId = await pericias.PorIdAsync();
        return EquipamentoRequisitosMapper.TryParse(tipo, request.Requisitos, request.PenalidadeDeRequisitos, porId.Values.ToList(), out var requisitos, out var penalidade, out var erro)
            ? (null, requisitos, penalidade)
            : (erro, null, null);
    }

    private static EquipmentKitFixedItemResponse ToResponse(EquipmentKitFixedItem f, IReadOnlyDictionary<int, PericiaDefinicao> porId, List<string> kits) =>
        new(f.Id.ToString(), f.Nome, f.Tipo.ToString(), f.DetalhesIncompletos,
            ItemFactory.Desserializar(f.Dados) with
            {
                Requisitos = RequisitosDePassivaMapper.ToDto(f.Requisitos, porId),
                PenalidadeDeRequisitos = EquipamentoRequisitosMapper.ToDto(f.PenalidadeDeRequisitos, porId),
            },
            kits);

    /// <summary>Nome dos kits (não excluídos) que usam cada item fixo, como item fixo ou como bônus de slot.</summary>
    private async Task<Dictionary<Guid, List<string>>> KitsPorItemAsync(List<Guid> ids)
    {
        var porItem = await db.EquipmentKitItems.Where(i => i.FixedItemId != null && ids.Contains(i.FixedItemId.Value))
            .Join(db.EquipmentKits.Where(k => !k.IsDeleted), i => i.KitId, k => k.Id, (i, k) => new { Id = i.FixedItemId!.Value, k.Nome }).ToListAsync();
        var porBonus = await db.EquipmentKitChoiceSlots.Where(s => s.BonusFixedItemId != null && ids.Contains(s.BonusFixedItemId.Value))
            .Join(db.EquipmentKits.Where(k => !k.IsDeleted), s => s.KitId, k => k.Id, (s, k) => new { Id = s.BonusFixedItemId!.Value, k.Nome }).ToListAsync();
        return porItem.Concat(porBonus).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Select(x => x.Nome).Distinct().OrderBy(n => n).ToList());
    }

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
