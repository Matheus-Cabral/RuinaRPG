using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public sealed class CharacterTraitsController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CharacterSheet> tipo)
    : CaracteristicasComRaciaisDaFichaController<CharacterSheet, CharacterTrait, CharacterTraitResponse, CharacterTraitsListResponse, AddCharacterTraitRequest>(db, tabelaDeNiveis, tipo)
{
    protected override (string TraitId, string? Especificacao) Ler(AddCharacterTraitRequest request) => (request.TraitId, request.Especificacao);

    protected override CharacterTraitResponse Resposta(CharacterTrait linha, CaracteristicaDoCatalogo caracteristica, int custo) =>
        new(linha.Id.ToString(), caracteristica.Id.ToString(), caracteristica.Nome, caracteristica.Descricao, custo, caracteristica.Polaridade.ToString(), linha.Especificacao,
            caracteristica.RequerEspecificacao, linha.IsRacial);

    protected override CharacterTraitsListResponse Lista(List<CharacterTraitResponse> positivas, int totalPositivas, List<CharacterTraitResponse> negativas, int totalNegativas, int pontosDisponiveis, int pontosDisponiveisNegativas) =>
        new(positivas, totalPositivas, negativas, totalNegativas, pontosDisponiveis, pontosDisponiveisNegativas);
}
