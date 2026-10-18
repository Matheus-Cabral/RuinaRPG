using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/attributes")]
public sealed class NpcAttributesController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<NpcSheet> tipo, RuinaRPG.Api.Services.NpcSheetStats stats)
    : AtributosDaFichaController<NpcSheet, NpcAttribute, Atributo, NpcAttributeResponse, UpdateNpcAttributeRequest>(db, tabelaDeNiveis, tipo)
{
    protected override DbSet<NpcAttribute> Linhas => Db.NpcAttributes;

    protected override (int Gasto, int Bonus, bool TemMaestria) Ler(UpdateNpcAttributeRequest request) => (request.Gasto, request.Bonus, request.TemMaestria);

    // Artefatos (Posses 5.b: estar na ficha é estar equipado), penalidades de equipamento ativas e Condições: NpcSheetStats.
    protected override async Task<List<NpcAttributeResponse>> ListarAsync(NpcSheet ficha) =>
        (await stats.AtributosAsync(ficha, await Tipo.CondicoesAsync(ficha)))
            .Select(a => new NpcAttributeResponse(a.Atributo, a.Gasto, a.Bonus, a.TemMaestria, a.Total))
            .ToList();
}
