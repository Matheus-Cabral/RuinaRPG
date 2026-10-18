using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Maestrias da ficha (criar, listar e apagar), para os três tipos. A rota fica na subclasse de cada tipo.</summary>
public abstract class MaestriasDaFichaController<TSheet, TMaestria, TSkill, TLinhaDeAtributo, TAtributo, TResponse, TAdd>(
    RuinaRpgDbContext db, IPericiaCatalogo pericias, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TMaestria : class, IMaestriaDaFicha<TAtributo>
    where TSkill : class, IPericiaDaFicha<TAtributo>
    where TLinhaDeAtributo : class, IAtributoDaFicha<TAtributo>
    where TAtributo : struct, Enum
{
    protected const string PericiaOuAtributoDesconhecido = "Perícia ou Atributo desconhecido.";

    protected RuinaRpgDbContext Db { get; } = db;
    protected IPericiaCatalogo Pericias { get; } = pericias;

    /// <summary>As Maestrias deste tipo de ficha (CharacterMasteries, NpcMasteries, CreatureMasteries).</summary>
    protected abstract DbSet<TMaestria> Linhas { get; }
    protected abstract DbSet<TSkill> LinhasDePericia { get; }
    protected abstract DbSet<TLinhaDeAtributo> LinhasDeAtributo { get; }

    /// <summary>Uma Maestria nova do tipo (as entidades têm Nome required, então não servem para new()).</summary>
    protected abstract TMaestria Nova(Guid sheetId, string nome, int periciaId, TAtributo atributo, int gastoMaestria);

    protected abstract (string Nome, string Pericia, string? Atributo, int GastoMaestria) Ler(TAdd request);

    protected abstract TResponse Resposta(string id, string nome, string pericia, string atributo, int gastoMaestria, int total);

    [HttpPost]
    public async Task<ActionResult<TResponse>> Add(Guid sheetId, TAdd request)
    {
        var (ficha, erro) = await FichaEditavelAsync(sheetId);
        if (erro is not null)
            return erro;
        var sheet = ficha!;

        var (nome, chave, atributoPedido, gastoMaestria) = Ler(request);
        var pericia = await Pericias.AtivaPorChaveAsync(chave);
        if (pericia is null)
            return BadRequest(PericiaOuAtributoDesconhecido);
        if (!Enum.TryParse<TAtributo>(atributoPedido, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest(PericiaOuAtributoDesconhecido);
        // R0005's "lista fixa mais curta" — only Perícias flagged DisponivelParaCriaturas have a seeded
        // CreatureSkill row; without this check a valid-but-disallowed Perícia (e.g. Alquimia)
        // would pass the lookup above and then 500 in ComputeTotalAsync's SingleAsync.
        if (Tipo.SoPericiasDeCriatura && !pericia.DisponivelParaCriaturas)
            return BadRequest("Perícia fora da lista permitida para Criaturas.");

        var mastery = Nova(sheetId, nome, pericia.Id, atributo, gastoMaestria);
        Linhas.Add(mastery);
        await Db.SaveChangesAsync();

        var porId = await Pericias.PorIdAsync();
        var total = await ComputeTotalAsync(sheet, mastery.Id, pericia, atributo, mastery.GastoMaestria, await Tipo.CondicoesAsync(sheet));
        return Created(string.Empty, ParaResposta(mastery, total, porId));
    }

    [HttpGet]
    public async Task<ActionResult<List<TResponse>>> List(Guid sheetId)
    {
        var (ficha, erro) = await FichaEditavelAsync(sheetId);
        if (erro is not null)
            return erro;
        var sheet = ficha!;

        var porId = await Pericias.PorIdAsync();
        var masteries = (await Linhas.Where(m => m.SheetId == sheetId).ToListAsync())
            .Where(m => porId.TryGetValue(m.PericiaId, out var p) && !p.IsDeleted)
            .ToList();
        var responses = new List<TResponse>();
        var efeitos = await Tipo.CondicoesAsync(sheet);
        foreach (var mastery in masteries)
        {
            var total = await ComputeTotalAsync(sheet, mastery.Id, porId[mastery.PericiaId], mastery.Atributo, mastery.GastoMaestria, efeitos);
            responses.Add(ParaResposta(mastery, total, porId));
        }

        return responses;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var (_, erro) = await FichaEditavelAsync(sheetId);
        if (erro is not null)
            return erro;

        var mastery = await Linhas.FirstOrDefaultAsync(m => m.Id == id && m.SheetId == sheetId);
        if (mastery is null)
            return NotFound();

        Linhas.Remove(mastery);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Condições: o Atributo entra já ajustado pelas linhas de Atributo, e o total passa pelas linhas de Maestria
    /// daquela Perícia. (A fórmula da Maestria nunca somou Artefatos nem penalidades; isso não muda aqui.)
    /// O bônus de Histórico só existe onde a ficha tem Histórico (<see cref="ITipoDeFicha{TSheet}.HistoricoId"/>).
    /// </summary>
    protected async Task<int> ComputeTotalAsync(TSheet sheet, Guid maestriaId, PericiaDefinicao pericia, TAtributo atributo, int gastoMaestria, EfeitosDeCondicao efeitos)
    {
        var sheetId = sheet.Id;
        var periciaId = pericia.Id;
        var skill = await LinhasDePericia.FirstOrDefaultAsync(s => s.SheetId == sheetId && s.PericiaId == periciaId);
        var attribute = await LinhasDeAtributo.SingleAsync(a => a.SheetId == sheetId && a.Atributo.Equals(atributo));
        var historicoId = Tipo.HistoricoId(sheet);
        var historico = historicoId is null ? null : await Db.Historicos.FindAsync(historicoId.Value);
        var bruto = SkillFormulas.Modificador(skill?.Gasto ?? 0, HistoricoBonusCalculator.For(periciaId, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));
        var atributoTotal = efeitos.NoAtributo(atributo.ToString(), AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0));
        return efeitos.NaMaestria(pericia.Chave, maestriaId, gastoMaestria + bruto + atributoTotal);
    }

    protected TResponse ParaResposta(TMaestria m, int total, IReadOnlyDictionary<int, PericiaDefinicao> porId) =>
        Resposta(m.Id.ToString(), m.Nome, porId[m.PericiaId].Chave, m.Atributo.ToString(), m.GastoMaestria, total);
}
