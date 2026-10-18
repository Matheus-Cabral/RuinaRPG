using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/skills")]
public sealed class NpcSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<NpcSheet> tipo, RuinaRPG.Api.Services.NpcSheetStats stats)
    : PericiasDaFichaController<NpcSheet, NpcSkill, Atributo, NpcSkillResponse, UpdateNpcSkillRequest>(db, pericias, tabelaDeNiveis, tipo)
{
    protected override DbSet<NpcSkill> Linhas => Db.NpcSkills;

    protected override (int Gasto, string? AtributoEscolhido) Ler(UpdateNpcSkillRequest request) => (request.Gasto, request.AtributoEscolhido);

    protected override async Task<List<NpcSkillResponse>> ListarAsync(NpcSheet ficha) =>
        (await stats.PericiasAsync(ficha, await Tipo.CondicoesAsync(ficha)))
            .Select(p => new NpcSkillResponse(p.Chave, p.Gasto, p.Modificador, p.Atributo, p.Total, p.BonusDeHistorico, p.Nome, p.Descricao))
            .ToList();
}
