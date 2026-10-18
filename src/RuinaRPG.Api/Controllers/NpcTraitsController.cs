using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public sealed class NpcTraitsController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<NpcSheet> tipo)
    : CaracteristicasComRaciaisDaFichaController<NpcSheet, NpcTrait, NpcTraitResponse, NpcTraitsListResponse, AddNpcTraitRequest>(db, tabelaDeNiveis, tipo)
{
    protected override (string TraitId, string? Especificacao) Ler(AddNpcTraitRequest request) => (request.TraitId, request.Especificacao);

    protected override NpcTraitResponse Resposta(NpcTrait linha, CaracteristicaDoCatalogo caracteristica, int custo) =>
        new(linha.Id.ToString(), caracteristica.Id.ToString(), caracteristica.Nome, caracteristica.Descricao, custo, caracteristica.Polaridade.ToString(), linha.Especificacao,
            caracteristica.RequerEspecificacao, linha.IsRacial);

    protected override NpcTraitsListResponse Lista(List<NpcTraitResponse> positivas, int totalPositivas, List<NpcTraitResponse> negativas, int totalNegativas, int pontosDisponiveis, int pontosDisponiveisNegativas) =>
        new(positivas, totalPositivas, negativas, totalNegativas, pontosDisponiveis, pontosDisponiveisNegativas);
}
