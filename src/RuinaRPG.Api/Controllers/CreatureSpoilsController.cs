using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// R0006 3.d: Espólios (Butim) da Criatura, no lugar do inventário do Personagem/NPC — Custo e CustoTotal vêm ao vivo
/// do Preco do Item ligado (nunca guardados), e há um campo DT que o item de inventário não tem. Só a Criatura tem,
/// então não há base genérica.
/// </summary>
[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}")]
public sealed class CreatureSpoilsController(RuinaRpgDbContext db, ITipoDeFicha<CreatureSheet> tipo) : FichaControllerBase<CreatureSheet>(tipo)
{
    [HttpPost("spoils")]
    public async Task<ActionResult<CreatureSpoilResponse>> AddSpoil(Guid sheetId, AddCreatureSpoilRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        if (!Guid.TryParse(request.ItemId, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
            return BadRequest("Item não encontrado.");

        var spoil = new CreatureSpoil { Id = Guid.NewGuid(), SheetId = sheetId, ItemId = itemId, Qtd = request.Qtd, DT = request.DT };
        db.CreatureSpoils.Add(spoil);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToSpoilResponseAsync(spoil));
    }

    [HttpGet("spoils")]
    public async Task<ActionResult<List<CreatureSpoilResponse>>> ListSpoils(Guid sheetId)
    {
        if ((await FichaParaLeituraAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var spoils = await db.CreatureSpoils.Where(i => i.SheetId == sheetId).ToListAsync();
        var responses = new List<CreatureSpoilResponse>();
        foreach (var spoil in spoils)
            responses.Add(await ToSpoilResponseAsync(spoil));
        return responses;
    }

    [HttpPut("spoils/{id}/qtd")]
    public async Task<IActionResult> UpdateSpoilQtd(Guid sheetId, Guid id, [FromBody] int qtd)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var spoil = await db.CreatureSpoils.FirstOrDefaultAsync(i => i.Id == id && i.SheetId == sheetId);
        if (spoil is null)
            return NotFound();

        spoil.Qtd = qtd;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("spoils/{id}")]
    public async Task<IActionResult> DeleteSpoil(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var spoil = await db.CreatureSpoils.FirstOrDefaultAsync(i => i.Id == id && i.SheetId == sheetId);
        if (spoil is null)
            return NotFound();

        db.CreatureSpoils.Remove(spoil);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<CreatureSpoilResponse> ToSpoilResponseAsync(CreatureSpoil spoil)
    {
        var item = await db.Items.SingleAsync(i => i.Id == spoil.ItemId);
        var imageUrl = await UrlDaImagemAsync(db, item.ImageId);
        return new CreatureSpoilResponse(spoil.Id.ToString(), item.Id.ToString(), item.Nome, item.Preco, spoil.Qtd, item.Preco * spoil.Qtd, spoil.DT, imageUrl, item.Descricao);
    }
}
