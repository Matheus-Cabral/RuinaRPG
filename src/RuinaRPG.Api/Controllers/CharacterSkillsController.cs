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
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/skills")]
public sealed class CharacterSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CharacterSheet> tipo, RuinaRPG.Api.Services.CharacterSheetStats stats)
    : PericiasDaFichaController<CharacterSheet, CharacterSkill, Atributo, SkillResponse>(db, pericias, tabelaDeNiveis, tipo)
{
    protected override (int Gasto, string? AtributoEscolhido) Ler(UpdateSkillRequest request) => (request.Gasto, request.AtributoEscolhido);

    protected override async Task<List<SkillResponse>> ListarAsync(CharacterSheet ficha) =>
        (await stats.PericiasAsync(ficha, await Tipo.CondicoesAsync(ficha)))
            .Select(p => new SkillResponse(p.Chave, p.Gasto, p.Modificador, p.Atributo, p.Total, p.BonusDeHistorico, p.Nome, p.Descricao))
            .ToList();

    [HttpGet("budget")]
    public async Task<ActionResult<SkillPointBudgetResponse>> Budget(Guid sheetId)
    {
        if ((await FichaParaLeituraAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var gastoBruto = await Db.CharacterSkills
            .Where(s => s.SheetId == sheetId && !Db.Pericias.Any(p => p.Id == s.PericiaId && p.IsDeleted))
            .SumAsync(s => s.Gasto);
        // Pontos ganhos por Acerto Crítico não vêm do orçamento por Nível — subtraídos do Gasto
        // Total para não acusar "acima do máximo" por um ganho legítimo (2.d).
        var gastoTotal = gastoBruto - sheet.PontosDePericiaBonusCritico;
        var tabela = await TabelaDeNiveis.ObterAsync();
        var pontosDisponiveis = SkillPointBudgetCalculator.Compute(sheet.Nivel, tabela);
        return new SkillPointBudgetResponse(gastoTotal, pontosDisponiveis);
    }
}
