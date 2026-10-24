using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/masteries")]
public sealed class CharacterMasteriesController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITipoDeFicha<CharacterSheet> tipo)
    : MaestriasDaFichaController<CharacterSheet, CharacterMastery, CharacterSkill, CharacterAttribute, Atributo>(db, pericias, tipo)
{
    protected override CharacterMastery Nova(Guid sheetId, string nome, int periciaId, Atributo atributo, int gastoMaestria) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, Nome = nome, PericiaId = periciaId, Atributo = atributo, GastoMaestria = gastoMaestria };

    protected override (string Nome, string Pericia, string? Atributo, int GastoMaestria) Ler(AddMasteryRequest request) =>
        (request.Nome, request.Pericia, request.Atributo, request.GastoMaestria);

    protected override MasteryResponse Resposta(string id, string nome, string pericia, string atributo, int gastoMaestria, int total) =>
        new(id, nome, pericia, atributo, gastoMaestria, total);

    // Só o Personagem edita uma Maestria existente; NPC e Criatura só criam e apagam.
    [HttpPut("{id}")]
    public async Task<ActionResult<MasteryResponse>> Update(Guid sheetId, Guid id, UpdateCharacterMasteryRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var pericia = await Pericias.AtivaPorChaveAsync(request.Pericia);
        if (pericia is null)
            return BadRequest(PericiaOuAtributoDesconhecido);
        if (!Enum.TryParse<Atributo>(request.Atributo, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest(PericiaOuAtributoDesconhecido);

        var mastery = await Db.CharacterMasteries.FirstOrDefaultAsync(m => m.Id == id && m.SheetId == sheetId);
        if (mastery is null)
            return NotFound();

        mastery.Nome = request.Nome;
        mastery.PericiaId = pericia.Id;
        mastery.Atributo = atributo;
        mastery.GastoMaestria = request.GastoMaestria;
        await Db.SaveChangesAsync();

        var porId = await Pericias.PorIdAsync();
        var total = await ComputeTotalAsync(sheet, mastery.Id, pericia, atributo, mastery.GastoMaestria, await Tipo.CondicoesAsync(sheet));
        return Ok(ParaResposta(mastery, total, porId));
    }
}
