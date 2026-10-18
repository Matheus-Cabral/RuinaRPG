using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}")]
public sealed class CreatureTraitsController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CreatureSheet> tipo)
    : CaracteristicasDaFichaController<CreatureSheet, CreatureTrait, CreatureTraitResponse, CreatureTraitsListResponse, AddCreatureTraitRequest>(db, tabelaDeNiveis, tipo)
{
    protected override (string TraitId, string? Especificacao) Ler(AddCreatureTraitRequest request) => (request.TraitId, request.Especificacao);

    // Só o catálogo exclusivo de criatura (Auditoria de Regras R0005). As linhas do catálogo normal concedidas antes da
    // 1.5.0 continuam valendo em ListTraits/SomaDoCustoAsync/DeleteTrait.
    protected override async Task<CaracteristicaDoCatalogo?> CaracteristicaParaAdicionarAsync(Guid traitId) =>
        await Db.Set<CreatureExclusiveTrait>().FirstOrDefaultAsync(t => t.Id == traitId && !t.IsDeleted) is { } trait ? CaracteristicaDoCatalogo.De(trait) : null;

    // Um Id do catálogo normal é recusado com mensagem própria.
    protected override async Task<ActionResult> CaracteristicaNaoEncontradaAsync(Guid traitId) =>
        await Db.Traits.AnyAsync(t => t.Id == traitId)
            ? BadRequest("Esta característica não é do catálogo de criatura.")
            : BadRequest("Trait não encontrado.");

    /// <summary>
    /// Soma o Custo das CreatureTrait da Polaridade, cada linha resolvida no catálogo para onde aponta (TraitId ou
    /// CreatureExclusiveTraitId) — duas buscas em lote em vez de um .Join(), já que um join SQL não atravessa duas tabelas
    /// por um par de FKs anuláveis. N é pequeno (as Características de uma ficha): duas consultas a mais no total.
    /// </summary>
    protected override async Task<int> SomaDoCustoAsync(Guid sheetId, Polaridade polaridade)
    {
        var rows = await Linhas.Where(t => t.SheetId == sheetId && t.Polaridade == polaridade).ToListAsync();
        var normalIds = rows.Where(r => r.TraitId is not null).Select(r => r.TraitId!.Value).ToList();
        var exclusiveIds = rows.Where(r => r.CreatureExclusiveTraitId is not null).Select(r => r.CreatureExclusiveTraitId!.Value).ToList();

        var normalTotal = await Db.Traits.Where(t => normalIds.Contains(t.Id)).SumAsync(t => t.Custo);
        var exclusiveTotal = await Db.Set<CreatureExclusiveTrait>().Where(t => exclusiveIds.Contains(t.Id)).SumAsync(t => t.Custo);
        return normalTotal + exclusiveTotal;
    }

    protected override CreatureTrait Nova(Guid sheetId, CaracteristicaDoCatalogo caracteristica, string? especificacao) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, TraitId = null, CreatureExclusiveTraitId = caracteristica.Id, Polaridade = caracteristica.Polaridade, Especificacao = especificacao };

    protected override async Task<List<(CreatureTrait Linha, CaracteristicaDoCatalogo Caracteristica)>> LinhasComCatalogoAsync(Guid sheetId)
    {
        var creatureTraits = await Linhas.Where(t => t.SheetId == sheetId).ToListAsync();
        var normalIds = creatureTraits.Where(t => t.TraitId is not null).Select(t => t.TraitId!.Value).ToList();
        var exclusiveIds = creatureTraits.Where(t => t.CreatureExclusiveTraitId is not null).Select(t => t.CreatureExclusiveTraitId!.Value).ToList();

        var normalById = await Db.Traits.Where(t => normalIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var exclusiveById = await Db.Set<CreatureExclusiveTrait>().Where(t => exclusiveIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);

        var rows = new List<(CreatureTrait, CaracteristicaDoCatalogo)>();
        foreach (var ct in creatureTraits)
        {
            if (ct.TraitId is not null && normalById.TryGetValue(ct.TraitId.Value, out var normalTrait))
                rows.Add((ct, CaracteristicaDoCatalogo.De(normalTrait)));
            else if (ct.CreatureExclusiveTraitId is not null && exclusiveById.TryGetValue(ct.CreatureExclusiveTraitId.Value, out var exclusiveTrait))
                rows.Add((ct, CaracteristicaDoCatalogo.De(exclusiveTrait)));
            // senão: a entrada do catálogo sumiu (não deveria, pelas FKs) — a linha fica de fora em vez de dar 500.
        }
        return rows;
    }

    protected override CreatureTraitResponse Resposta(CreatureTrait linha, CaracteristicaDoCatalogo caracteristica, int custo) =>
        new(linha.Id.ToString(), caracteristica.Id.ToString(), caracteristica.Nome, caracteristica.Descricao, custo, caracteristica.Polaridade.ToString(), linha.Especificacao, caracteristica.RequerEspecificacao);

    protected override CreatureTraitsListResponse Lista(List<CreatureTraitResponse> positivas, int totalPositivas, List<CreatureTraitResponse> negativas, int totalNegativas, int pontosDisponiveis, int pontosDisponiveisNegativas) =>
        new(positivas, totalPositivas, negativas, totalNegativas, pontosDisponiveis, pontosDisponiveisNegativas);
}
