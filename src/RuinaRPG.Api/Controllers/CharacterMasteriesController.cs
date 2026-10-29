using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/masteries")]
public sealed class CharacterMasteriesController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITipoDeFicha<CharacterSheet> tipo)
    : MaestriasDaFichaController<CharacterSheet, CharacterMastery, CharacterSkill, CharacterAttribute, Atributo>(db, pericias, tipo)
{
    protected override CharacterMastery Nova(Guid sheetId, string nome, int periciaId, Atributo atributo, int gastoMaestria) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, Nome = nome, PericiaId = periciaId, Atributo = atributo, GastoMaestria = gastoMaestria };

    protected override (string Nome, string Pericia, string? Atributo, int GastoMaestria) Ler(AddMasteryRequest request) =>
        (request.Nome, request.Pericia, request.Atributo, request.GastoMaestria);

    protected override MasteryResponse Resposta(string id, string nome, string pericia, string atributo, int gastoMaestria, int total) =>
        new(id, nome, pericia, atributo, gastoMaestria, total);
}
