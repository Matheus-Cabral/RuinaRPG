using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public sealed class NpcRacialTraitsController(RuinaRpgDbContext db, ITipoDeFicha<NpcSheet> tipo)
    : TracosRaciaisDaFichaController<NpcSheet, NpcTrait>(db, tipo)
{
    protected override Task<Guid> MestreDaFichaAsync(NpcSheet ficha) => Task.FromResult(ficha.GmId);
}
