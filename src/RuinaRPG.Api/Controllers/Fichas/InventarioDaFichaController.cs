using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Inventário da ficha (Personagem e NPC; a Criatura tem Espólios no lugar, em <see cref="CreatureSpoilsController"/>).
/// A rota (<c>api/&lt;tipo&gt;-sheets/{sheetId}</c>) fica na subclasse de cada tipo.
/// </summary>
public abstract class InventarioDaFichaController<TSheet, TLinha, TResponse, TAdd>(RuinaRpgDbContext db, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TLinha : class, IItemDoInventarioDaFicha, new()
{
    protected RuinaRpgDbContext Db { get; } = db;

    private DbSet<TLinha> Linhas => Db.Set<TLinha>();

    protected abstract (string ItemId, int Qtd) Ler(TAdd request);

    /// <summary>Confere se o Item do Catálogo pedido pode entrar no inventário; o 400 se não existe ou não serve.</summary>
    protected abstract Task<ActionResult?> ConferirItemAsync(Guid itemId);

    protected abstract TResponse Resposta(TLinha linha, Item item, string? imageUrl);

    [HttpPost("inventory")]
    public async Task<ActionResult<TResponse>> AddInventoryItem(Guid sheetId, TAdd request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var (itemIdPedido, qtd) = Ler(request);
        if (!Guid.TryParse(itemIdPedido, out var itemId))
            return BadRequest("ItemId inválido.");

        if (await ConferirItemAsync(itemId) is { } erroDoItem)
            return erroDoItem;

        var inventoryItem = new TLinha { Id = Guid.NewGuid(), SheetId = sheetId, ItemId = itemId, Qtd = qtd };
        Linhas.Add(inventoryItem);
        await Db.SaveChangesAsync();

        return Created(string.Empty, await RespostaAsync(inventoryItem));
    }

    [HttpGet("inventory")]
    public async Task<ActionResult<List<TResponse>>> ListInventory(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var items = await Linhas.Where(i => i.SheetId == sheetId).ToListAsync();
        var responses = new List<TResponse>();
        foreach (var item in items)
            responses.Add(await RespostaAsync(item));
        return responses;
    }

    [HttpPut("inventory/{id}/qtd")]
    public async Task<IActionResult> UpdateInventoryItemQtd(Guid sheetId, Guid id, [FromBody] int qtd)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var item = await Linhas.FirstOrDefaultAsync(i => i.Id == id && i.SheetId == sheetId);
        if (item is null)
            return NotFound();

        item.Qtd = qtd;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("inventory/{id}")]
    public async Task<IActionResult> DeleteInventoryItem(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var item = await Linhas.FirstOrDefaultAsync(i => i.Id == id && i.SheetId == sheetId);
        if (item is null)
            return NotFound();

        Linhas.Remove(item);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<TResponse> RespostaAsync(TLinha linha)
    {
        var item = await Db.Items.SingleAsync(i => i.Id == linha.ItemId);
        return Resposta(linha, item, await UrlDaImagemAsync(Db, item.ImageId));
    }
}
