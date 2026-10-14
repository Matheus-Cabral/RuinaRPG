using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/masteries")]
public class CreatureMasteriesController(RuinaRpgDbContext db, IPericiaCatalogo pericias, RuinaRPG.Api.Services.CondicoesAtivasDaFicha condicoes) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreatureMasteryResponse>> Add(Guid sheetId, AddCreatureMasteryRequest request)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var pericia = await pericias.AtivaPorChaveAsync(request.Pericia);
        if (pericia is null)
            return BadRequest("Perícia ou Atributo desconhecido.");
        if (!Enum.TryParse<AtributoCriatura>(request.Atributo, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest("Perícia ou Atributo desconhecido.");
        // R0005's "lista fixa mais curta" — only Perícias flagged DisponivelParaCriaturas have a seeded
        // CreatureSkill row; without this check a valid-but-disallowed Perícia (e.g. Alquimia)
        // would pass the lookup above and then 500 in ComputeTotalAsync's SingleAsync.
        if (!pericia.DisponivelParaCriaturas)
            return BadRequest("Perícia fora da lista permitida para Criaturas.");

        var mastery = new CreatureMastery { Id = Guid.NewGuid(), CreatureSheetId = sheetId, Nome = request.Nome, PericiaId = pericia.Id, Atributo = atributo, GastoMaestria = request.GastoMaestria };
        db.CreatureMasteries.Add(mastery);
        await db.SaveChangesAsync();

        var porId = await pericias.PorIdAsync();
        var total = await ComputeTotalAsync(sheetId, pericia, atributo, mastery.GastoMaestria, await condicoes.ParaCriaturaAsync(sheet));
        return Created(string.Empty, ToResponse(mastery, total, porId));
    }

    [HttpGet]
    public async Task<ActionResult<List<CreatureMasteryResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var porId = await pericias.PorIdAsync();
        var masteries = (await db.CreatureMasteries.Where(m => m.CreatureSheetId == sheetId).ToListAsync())
            .Where(m => porId.TryGetValue(m.PericiaId, out var p) && !p.IsDeleted)
            .ToList();
        var responses = new List<CreatureMasteryResponse>();
        var efeitos = await condicoes.ParaCriaturaAsync(sheet);
        foreach (var mastery in masteries)
        {
            var total = await ComputeTotalAsync(sheetId, porId[mastery.PericiaId], mastery.Atributo, mastery.GastoMaestria, efeitos);
            responses.Add(ToResponse(mastery, total, porId));
        }

        return responses;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var mastery = await db.CreatureMasteries.FirstOrDefaultAsync(m => m.Id == id && m.CreatureSheetId == sheetId);
        if (mastery is null)
            return NotFound();

        db.CreatureMasteries.Remove(mastery);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Condições: o Atributo entra já ajustado pelas linhas de Atributo, e o total passa pelas linhas de Maestria
    /// daquela Perícia. (A fórmula da Maestria nunca somou Artefatos nem penalidades; isso não muda aqui.)
    /// </summary>
    private async Task<int> ComputeTotalAsync(Guid sheetId, PericiaDefinicao pericia, AtributoCriatura atributo, int gastoMaestria, EfeitosDeCondicao efeitos)
    {
        var periciaId = pericia.Id;
        var skill = await db.CreatureSkills.FirstOrDefaultAsync(s => s.CreatureSheetId == sheetId && s.PericiaId == periciaId);
        var attribute = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetId && a.Atributo == atributo);
        var bruto = SkillFormulas.Modificador(skill?.Gasto ?? 0, 0);
        var atributoTotal = efeitos.NoAtributo(atributo.ToString(), AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0));
        return efeitos.NaMaestria(pericia.Chave, gastoMaestria + bruto + atributoTotal);
    }

    private static CreatureMasteryResponse ToResponse(CreatureMastery m, int total, IReadOnlyDictionary<int, PericiaDefinicao> porId) =>
        new(m.Id.ToString(), m.Nome, porId[m.PericiaId].Chave, m.Atributo.ToString(), m.GastoMaestria, total);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
