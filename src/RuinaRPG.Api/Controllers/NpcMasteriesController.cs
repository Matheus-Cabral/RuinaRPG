using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/masteries")]
public sealed class NpcMasteriesController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITipoDeFicha<NpcSheet> tipo)
    : MaestriasDaFichaController<NpcSheet, NpcMastery, NpcSkill, NpcAttribute, Atributo, NpcMasteryResponse, AddNpcMasteryRequest>(db, pericias, tipo)
{
    protected override NpcMastery Nova(Guid sheetId, string nome, int periciaId, Atributo atributo, int gastoMaestria) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, Nome = nome, PericiaId = periciaId, Atributo = atributo, GastoMaestria = gastoMaestria };

    protected override (string Nome, string Pericia, string? Atributo, int GastoMaestria) Ler(AddNpcMasteryRequest request) =>
        (request.Nome, request.Pericia, request.Atributo, request.GastoMaestria);

    protected override NpcMasteryResponse Resposta(string id, string nome, string pericia, string atributo, int gastoMaestria, int total) =>
        new(id, nome, pericia, atributo, gastoMaestria, total);
}
