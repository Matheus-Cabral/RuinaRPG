using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}")]
public sealed class CreatureAffectionsController(RuinaRpgDbContext db, ITipoDeFicha<CreatureSheet> tipo)
    : AfeicoesDaFichaController<CreatureSheet, CreatureAffection, CreatureAffectionResponse, AddCreatureAffectionRequest>(db, tipo)
{
    protected override CreatureAffection Nova(Guid sheetId, AddCreatureAffectionRequest request) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, Nome = request.Nome, Favorabilidade = request.Favorabilidade };

    protected override CreatureAffectionResponse Resposta(CreatureAffection affection) => new(affection.Id.ToString(), affection.Nome, affection.Favorabilidade);
}
