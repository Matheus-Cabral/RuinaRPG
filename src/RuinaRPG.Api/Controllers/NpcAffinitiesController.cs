using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/afinidades")]
public sealed class NpcAffinitiesController(RuinaRpgDbContext db, ITipoDeFicha<NpcSheet> tipo, AfinidadesDaFicha afinidades, CatalogoDeAfinidadesService catalogo)
    : AfinidadesDaFichaController<NpcSheet, NpcPontosDeAfinidade>(db, tipo, afinidades, catalogo);
