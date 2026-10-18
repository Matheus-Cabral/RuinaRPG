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
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/attributes")]
public sealed class CharacterAttributesController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CharacterSheet> tipo, RuinaRPG.Api.Services.CharacterSheetStats stats)
    : AtributosDaFichaController<CharacterSheet, CharacterAttribute, Atributo, CharacterAttributeResponse, UpdateCharacterAttributeRequest>(db, tabelaDeNiveis, tipo)
{
    protected override (int Gasto, int Bonus, bool TemMaestria) Ler(UpdateCharacterAttributeRequest request) => (request.Gasto, request.Bonus, request.TemMaestria);

    // Cálculo próprio do Personagem, mantido como estava até o núcleo de stats compartilhado (spec D.4).
    protected override async Task<List<CharacterAttributeResponse>> ListarAsync(CharacterSheet sheet)
    {
        // Display order is independent of the enum's underlying (persisted) int value —
        // see AttributeDisplayOrder's doc comment — so this sorts in memory, not in SQL.
        var attributes = (await Db.CharacterAttributes.Where(a => a.SheetId == sheet.Id).ToListAsync())
            .OrderBy(a => AttributeDisplayOrder.Rank(a.Atributo))
            .ToList();

        // Artefatos (Posses 5.b: estar na ficha é estar equipado) + penalidades de equipamento ativas.
        var efeitos = await Tipo.CondicoesAsync(sheet);
        var artefatos = await stats.ModificadoresAsync(sheet, efeitos);

        return attributes
            .Select(a => new CharacterAttributeResponse(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                efeitos.NoAtributo(a.Atributo.ToString(), AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                    artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())))))
            .ToList();
    }
}
