using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public sealed class CharacterRacialTraitsController(RuinaRpgDbContext db, ITipoDeFicha<CharacterSheet> tipo)
    : TracosRaciaisDaFichaController<CharacterSheet, CharacterTrait>(db, tipo)
{
}
