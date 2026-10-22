using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/attributes")]
public sealed class CreatureAttributesController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CreatureSheet> tipo, RuinaRPG.Api.Services.CreatureSheetStats stats)
    : AtributosDaFichaController<CreatureSheet, CreatureAttribute, AtributoCriatura>(db, tabelaDeNiveis, tipo)
{
    protected override (int Gasto, int Bonus, bool TemMaestria) Ler(UpdateAttributeRequest request) => (request.Gasto, request.Bonus, request.TemMaestria);

    protected override async Task<List<AttributeResponse>> ListarAsync(CreatureSheet ficha) =>
        (await stats.AtributosAsync(ficha, await Tipo.CondicoesAsync(ficha)))
            .Select(a => new AttributeResponse(a.Atributo, a.Gasto, a.Bonus, a.TemMaestria, a.Total))
            .ToList();

    /// <summary>Rank no lugar do Nível 1 da Tabela de Níveis (Requisitos - Ficha de Criaturas R0005 2.a).</summary>
    protected override int PontosDisponiveis(CreatureSheet ficha, ProgressaoDeNivel tabela) =>
        CreatureAttributePointBudgetCalculator.Compute(ficha.Rank, ficha.Nivel, tabela);
}
