using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/attributes")]
public sealed class CharacterAttributesController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CharacterSheet> tipo, RuinaRPG.Api.Services.CharacterSheetStats stats)
    : AtributosDaFichaController<CharacterSheet, CharacterAttribute, Atributo>(db, tabelaDeNiveis, tipo)
{
    protected override (int Gasto, int Bonus, bool TemMaestria) Ler(UpdateAttributeRequest request) => (request.Gasto, request.Bonus, request.TemMaestria);

    // Artefatos (Posses 5.b: estar na ficha é estar equipado), penalidades de equipamento ativas e Condições: CharacterSheetStats.
    protected override async Task<List<AttributeResponse>> ListarAsync(CharacterSheet ficha) =>
        (await stats.AtributosAsync(ficha, await Tipo.CondicoesAsync(ficha)))
            .Select(a => new AttributeResponse(a.Atributo, a.Gasto, a.Bonus, a.TemMaestria, a.Total))
            .ToList();
}
