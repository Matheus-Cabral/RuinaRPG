using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public sealed class CharacterInventoryController(RuinaRpgDbContext db, ITipoDeFicha<CharacterSheet> tipo)
    : InventarioDaFichaController<CharacterSheet, CharacterInventoryItem>(db, tipo)
{
    protected override (string ItemId, int Qtd) Ler(AddInventoryItemRequest request) => (request.ItemId, request.Qtd);

    // Ficha de Personagem 5.a: "Armas, Armaduras e Escudos não aparecem aqui" — só Item Geral, ao contrário da busca
    // em todos os Itens de antes (que aceitava Arma/Armadura/Escudo/Artefato e contava o Peso deles em dois lugares).
    protected override async Task<ActionResult?> ConferirItemAsync(Guid itemId) =>
        await Db.Set<ItemGeral>().FirstOrDefaultAsync(i => i.Id == itemId) is null ? BadRequest("Item Geral não encontrado.") : null;

    protected override InventoryItemResponse Resposta(CharacterInventoryItem linha, Item item, string? imageUrl) =>
        new(linha.Id.ToString(), item.Id.ToString(), item.Nome, item.Peso, linha.Qtd, item.Peso * linha.Qtd, imageUrl, item.Descricao);
}
