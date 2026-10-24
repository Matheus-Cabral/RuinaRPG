using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public sealed class NpcInventoryController(RuinaRpgDbContext db, ITipoDeFicha<NpcSheet> tipo)
    : InventarioDaFichaController<NpcSheet, NpcInventoryItem>(db, tipo)
{
    protected override (string ItemId, int Qtd) Ler(AddInventoryItemRequest request) => (request.ItemId, request.Qtd);

    // O NPC aceita qualquer Item do Catálogo; só o Personagem se restringe a Item Geral (Divergências encontradas).
    protected override async Task<ActionResult?> ConferirItemAsync(Guid itemId) =>
        await Db.Items.FirstOrDefaultAsync(i => i.Id == itemId) is null ? BadRequest("Item não encontrado.") : null;

    protected override InventoryItemResponse Resposta(NpcInventoryItem linha, Item item, string? imageUrl) =>
        new(linha.Id.ToString(), item.Id.ToString(), item.Nome, item.Peso, linha.Qtd, item.Peso * linha.Qtd, imageUrl, item.Descricao);
}
