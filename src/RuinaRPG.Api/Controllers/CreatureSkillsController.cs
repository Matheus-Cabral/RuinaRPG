using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/skills")]
public sealed class CreatureSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CreatureSheet> tipo, RuinaRPG.Api.Services.CreatureSheetStats stats)
    : PericiasDaFichaController<CreatureSheet, CreatureSkill, AtributoCriatura, CreatureSkillResponse, UpdateCreatureSkillRequest>(db, pericias, tabelaDeNiveis, tipo)
{
    protected override DbSet<CreatureSkill> Linhas => Db.CreatureSkills;

    protected override (int Gasto, string? AtributoEscolhido) Ler(UpdateCreatureSkillRequest request) => (request.Gasto, request.AtributoEscolhido);

    protected override async Task<List<CreatureSkillResponse>> ListarAsync(CreatureSheet ficha) =>
        (await stats.PericiasAsync(ficha, await Tipo.CondicoesAsync(ficha)))
            .Select(p => new CreatureSkillResponse(p.Chave, p.Gasto, p.Modificador, p.Atributo, p.Total, p.Nome, p.Descricao))
            .ToList();
}
