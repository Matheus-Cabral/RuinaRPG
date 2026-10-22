using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public sealed class NpcAffectionsController(RuinaRpgDbContext db, ITipoDeFicha<NpcSheet> tipo)
    : AfeicoesDaFichaController<NpcSheet, NpcAffection>(db, tipo)
{
    protected override NpcAffection Nova(Guid sheetId, AddAffectionRequest request) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, Nome = request.Nome, Favorabilidade = request.Favorabilidade };

    protected override AffectionResponse Resposta(NpcAffection affection) => new(affection.Id.ToString(), affection.Nome, affection.Favorabilidade);
}
