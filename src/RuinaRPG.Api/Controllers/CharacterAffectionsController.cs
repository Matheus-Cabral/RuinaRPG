using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public sealed class CharacterAffectionsController(RuinaRpgDbContext db, ITipoDeFicha<CharacterSheet> tipo)
    : AfeicoesDaFichaController<CharacterSheet, CharacterAffection, CharacterAffectionResponse, AddCharacterAffectionRequest>(db, tipo)
{
    protected override CharacterAffection Nova(Guid sheetId, AddCharacterAffectionRequest request) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, Nome = request.Nome, Favorabilidade = request.Favorabilidade };

    protected override CharacterAffectionResponse Resposta(CharacterAffection affection) => new(affection.Id.ToString(), affection.Nome, affection.Favorabilidade);

    // Só o Personagem edita uma Afeição existente; NPC e Criatura só criam e apagam.
    [HttpPut("affections/{id}")]
    public async Task<ActionResult<CharacterAffectionResponse>> UpdateAffection(Guid sheetId, Guid id, UpdateCharacterAffectionRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var affection = await Linhas.FirstOrDefaultAsync(a => a.Id == id && a.SheetId == sheetId);
        if (affection is null)
            return NotFound();

        affection.Nome = request.Nome;
        affection.Favorabilidade = request.Favorabilidade;
        await Db.SaveChangesAsync();

        return Ok(Resposta(affection));
    }
}
