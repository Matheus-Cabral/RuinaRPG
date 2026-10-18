using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public sealed class CharacterArtifactsController(RuinaRpgDbContext db, EquipmentPenaltyService penalidades, CharacterSheetStats stats, ITipoDeFicha<CharacterSheet> tipo)
    : ArtefatosDaFichaController<CharacterSheet, CharacterArtifact, CharacterArtifactResponse, AddCharacterArtifactRequest>(db, penalidades, tipo)
{
    protected override string ArtifactItemIdDe(AddCharacterArtifactRequest request) => request.ArtifactItemId;

    protected override CharacterArtifactResponse Resposta(CharacterArtifact linha, Artefato item, string? imageUrl, AvaliacaoDeEquipamento avaliacao, string? desabilitadoPor) =>
        new(linha.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeAlvo?.ToString() ?? string.Empty, item.Alvo ?? string.Empty, item.Valor ?? 0, imageUrl, item.Descricao,
            avaliacao.Requisitos, avaliacao.RequisitosPendentes, avaliacao.Penalidade, avaliacao.OutrasPenalidades, desabilitadoPor);

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(CharacterSheet ficha) => stats.FichaParaRequisitosAsync(ficha);
}
