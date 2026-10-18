using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public sealed class NpcArtifactsController(RuinaRpgDbContext db, EquipmentPenaltyService penalidades, NpcSheetStats stats, ITipoDeFicha<NpcSheet> tipo)
    : ArtefatosDaFichaController<NpcSheet, NpcArtifact, NpcArtifactResponse, AddNpcArtifactRequest>(db, penalidades, tipo)
{
    protected override string ArtifactItemIdDe(AddNpcArtifactRequest request) => request.ArtifactItemId;

    protected override NpcArtifactResponse Resposta(NpcArtifact linha, Artefato item, string? imageUrl, AvaliacaoDeEquipamento avaliacao, string? desabilitadoPor) =>
        new(linha.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeAlvo?.ToString() ?? string.Empty, item.Alvo ?? string.Empty, item.Valor ?? 0, imageUrl, item.Descricao,
            avaliacao.Requisitos, avaliacao.RequisitosPendentes, avaliacao.Penalidade, avaliacao.OutrasPenalidades, desabilitadoPor);

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(NpcSheet ficha) => stats.FichaParaRequisitosAsync(ficha);
}
