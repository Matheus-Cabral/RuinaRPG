using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/afinidades")]
public sealed class CharacterAffinitiesController(RuinaRpgDbContext db, ITipoDeFicha<CharacterSheet> tipo, AfinidadesDaFicha afinidades, CatalogoDeAfinidadesService catalogo)
    : AfinidadesDaFichaController<CharacterSheet, CharacterPontosDeAfinidade>(db, tipo, afinidades, catalogo);
