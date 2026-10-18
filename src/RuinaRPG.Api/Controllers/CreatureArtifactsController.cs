using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}")]
public sealed class CreatureArtifactsController(RuinaRpgDbContext db, EquipmentPenaltyService penalidades, CreatureSheetStats stats, ITipoDeFicha<CreatureSheet> tipo)
    : ArtefatosDaFichaController<CreatureSheet, CreatureArtifact, CreatureArtifactResponse, AddCreatureArtifactRequest>(db, penalidades, tipo)
{
    protected override string ArtifactItemIdDe(AddCreatureArtifactRequest request) => request.ArtifactItemId;

    protected override CreatureArtifactResponse Resposta(CreatureArtifact linha, Artefato item, string? imageUrl, AvaliacaoDeEquipamento avaliacao, string? desabilitadoPor) =>
        new(linha.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeAlvo?.ToString() ?? string.Empty, item.Alvo ?? string.Empty, item.Valor ?? 0, imageUrl, item.Descricao,
            avaliacao.Requisitos, avaliacao.RequisitosPendentes, avaliacao.Penalidade, avaliacao.OutrasPenalidades, desabilitadoPor);

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(CreatureSheet ficha) => stats.FichaParaRequisitosAsync(ficha);
}
