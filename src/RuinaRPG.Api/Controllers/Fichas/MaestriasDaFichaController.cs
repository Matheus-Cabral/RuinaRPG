using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Maestrias da ficha (criar, listar, editar e apagar), para os três tipos. A rota fica na subclasse de cada tipo.</summary>
public abstract class MaestriasDaFichaController<TSheet, TMaestria, TSkill, TLinhaDeAtributo, TAtributo>(
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
    protected DbSet<TMaestria> Linhas => Db.Set<TMaestria>();
    protected DbSet<TSkill> LinhasDePericia => Db.Set<TSkill>();
    protected DbSet<TLinhaDeAtributo> LinhasDeAtributo => Db.Set<TLinhaDeAtributo>();

    /// <summary>Uma Maestria nova do tipo (as entidades têm Nome required, então não servem para new()).</summary>
    protected abstract TMaestria Nova(Guid sheetId, string nome, int periciaId, TAtributo atributo, int gastoMaestria);

    protected abstract (string Nome, string Pericia, string? Atributo, int GastoMaestria) Ler(AddMasteryRequest request);

    protected abstract MasteryResponse Resposta(string id, string nome, string pericia, string atributo, int gastoMaestria, int total);

    [HttpPost]
    public async Task<ActionResult<MasteryResponse>> Add(Guid sheetId, AddMasteryRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var (nome, chave, atributoPedido, gastoMaestria) = Ler(request);
        var pericia = await Pericias.AtivaPorChaveAsync(chave);
        if (pericia is null)
            return BadRequest(PericiaOuAtributoDesconhecido);
        if (!Enum.TryParse<TAtributo>(atributoPedido, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest(PericiaOuAtributoDesconhecido);
        // A "lista fixa mais curta" da R0005: só Perícias marcadas DisponivelParaCriaturas têm linha de
        // CreatureSkill semeada; sem esta checagem, uma Perícia válida mas não permitida (ex.: Alquimia)
        // passaria pela busca acima e daria 500 no SingleAsync de ComputeTotalAsync.
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
    public async Task<ActionResult<List<MasteryResponse>>> List(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var porId = await Pericias.PorIdAsync();
        var masteries = (await Linhas.Where(m => m.SheetId == sheetId).ToListAsync())
            .Where(m => porId.TryGetValue(m.PericiaId, out var p) && !p.IsDeleted)
            .ToList();
        var responses = new List<MasteryResponse>();
        var efeitos = await Tipo.CondicoesAsync(sheet);
        foreach (var mastery in masteries)
        {
            var total = await ComputeTotalAsync(sheet, mastery.Id, porId[mastery.PericiaId], mastery.Atributo, mastery.GastoMaestria, efeitos);
            responses.Add(ParaResposta(mastery, total, porId));
        }

        return responses;
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MasteryResponse>> Update(Guid sheetId, Guid id, UpdateMasteryRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var pericia = await Pericias.AtivaPorChaveAsync(request.Pericia);
        if (pericia is null)
            return BadRequest(PericiaOuAtributoDesconhecido);
        if (!Enum.TryParse<TAtributo>(request.Atributo, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest(PericiaOuAtributoDesconhecido);
        if (Tipo.SoPericiasDeCriatura && !pericia.DisponivelParaCriaturas)
            return BadRequest("Perícia fora da lista permitida para Criaturas.");

        var mastery = await Linhas.FirstOrDefaultAsync(m => m.Id == id && m.SheetId == sheetId);
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
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

    protected MasteryResponse ParaResposta(TMaestria m, int total, IReadOnlyDictionary<int, PericiaDefinicao> porId) =>
        Resposta(m.Id.ToString(), m.Nome, porId[m.PericiaId].Chave, m.Atributo.ToString(), m.GastoMaestria, total);
}
