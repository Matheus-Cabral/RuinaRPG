using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/masteries")]
public sealed class CreatureMasteriesController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITipoDeFicha<CreatureSheet> tipo)
    : MaestriasDaFichaController<CreatureSheet, CreatureMastery, CreatureSkill, CreatureAttribute, AtributoCriatura, CreatureMasteryResponse, AddCreatureMasteryRequest>(db, pericias, tipo)
{

    protected override CreatureMastery Nova(Guid sheetId, string nome, int periciaId, AtributoCriatura atributo, int gastoMaestria) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, Nome = nome, PericiaId = periciaId, Atributo = atributo, GastoMaestria = gastoMaestria };

    protected override (string Nome, string Pericia, string? Atributo, int GastoMaestria) Ler(AddCreatureMasteryRequest request) =>
        (request.Nome, request.Pericia, request.Atributo, request.GastoMaestria);

    protected override CreatureMasteryResponse Resposta(string id, string nome, string pericia, string atributo, int gastoMaestria, int total) =>
        new(id, nome, pericia, atributo, gastoMaestria, total);
}
