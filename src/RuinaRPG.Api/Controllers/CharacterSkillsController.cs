using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/skills")]
public sealed class CharacterSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CharacterSheet> tipo, RuinaRPG.Api.Services.CharacterSheetStats stats)
    : PericiasDaFichaController<CharacterSheet, CharacterSkill, Atributo, CharacterSkillResponse, UpdateCharacterSkillRequest>(db, pericias, tabelaDeNiveis, tipo)
{
    protected override (int Gasto, string? AtributoEscolhido) Ler(UpdateCharacterSkillRequest request) => (request.Gasto, request.AtributoEscolhido);

    // Cálculo próprio do Personagem, mantido como estava até o núcleo de stats compartilhado (spec D.4).
    protected override async Task<List<CharacterSkillResponse>> ListarAsync(CharacterSheet sheet)
    {
        var sheetId = sheet.Id;
        var porId = await Pericias.PorIdAsync();
        var ativas = porId.Values.Where(p => !p.IsDeleted).ToList();
        var linhas = await Db.CharacterSkills.Where(s => s.SheetId == sheetId).ToDictionaryAsync(s => s.PericiaId);

        // Artefatos (Posses 5.b: estar na ficha é estar equipado) + penalidades de equipamento ativas.
        var efeitos = await Tipo.CondicoesAsync(sheet);
        var artefatos = await stats.ModificadoresAsync(sheet, efeitos);

        var attributeTotals = await Db.CharacterAttributes
            .Where(a => a.SheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => efeitos.NoAtributo(a.Atributo.ToString(), AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString()))));

        var historico = sheet.HistoricoId is null ? null : await Db.Historicos.FindAsync(sheet.HistoricoId.Value);

        return ativas
            .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p =>
            {
                linhas.TryGetValue(p.Id, out var s);
                var gasto = s?.Gasto ?? 0;
                var atributo = s?.AtributoEscolhido ?? p.AtributoSugerido;
                var historicoBonus = HistoricoBonusCalculator.For(p.Id, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId);
                var modificador = SkillFormulas.Modificador(gasto, historicoBonus);
                var total = atributo is not null && attributeTotals.TryGetValue(atributo.Value, out var atributoTotal)
                    ? efeitos.NaPericia(p.Chave, SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave)))
                    : (int?)null;
                return new CharacterSkillResponse(p.Chave, gasto, modificador, atributo?.ToString(), total, historicoBonus > 0, p.Nome, p.Descricao);
            })
            .ToList();
    }

    [HttpGet("budget")]
    public async Task<ActionResult<SkillPointBudgetResponse>> Budget(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
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
