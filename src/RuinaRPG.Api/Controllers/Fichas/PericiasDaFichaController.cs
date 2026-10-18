using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Perícias da ficha (listar e editar uma), para os três tipos. A rota fica na subclasse de cada tipo.</summary>
public abstract class PericiasDaFichaController<TSheet, TSkill, TAtributo, TResponse, TUpdate>(
    RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TSkill : class, IPericiaDaFicha<TAtributo>, new()
    where TAtributo : struct, Enum
{
    protected RuinaRpgDbContext Db { get; } = db;
    protected IPericiaCatalogo Pericias { get; } = pericias;
    protected ITabelaDeNiveis TabelaDeNiveis { get; } = tabelaDeNiveis;

    /// <summary>As linhas de Perícia deste tipo de ficha (CharacterSkills, NpcSkills, CreatureSkills).</summary>
    protected DbSet<TSkill> Linhas => Db.Set<TSkill>();

    /// <summary>A lista de Perícias da ficha já calculada, no record de resposta do tipo.</summary>
    protected abstract Task<List<TResponse>> ListarAsync(TSheet ficha);

    protected abstract (int Gasto, string? AtributoEscolhido) Ler(TUpdate request);

    [HttpGet]
    public async Task<ActionResult<List<TResponse>>> List(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        return await ListarAsync(ficha);
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, string pericia, TUpdate request)
    {
        // Chave desconhecida ou Perícia removida: resolvida antes de tudo, como a antiga falha de binding da rota.
        var def = await Pericias.AtivaPorChaveAsync(pericia);
        if (def is null)
            return NotFound();

        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var (gasto, atributoEscolhido) = Ler(request);
        if (Tipo.SoPericiasDeCriatura && !def.DisponivelParaCriaturas)
            // R0005's "lista fixa mais curta" — só Perícias com DisponivelParaCriaturas aparecem na Criatura.
            return BadRequest("Perícia fora da lista permitida para Criaturas.");
        if (Tipo.AplicaLimitesDeNivel)
        {
            var tabela = await TabelaDeNiveis.ObterAsync();
            var gastoAtual = await Linhas.Where(s => s.SheetId == sheetId && s.PericiaId == def.Id).Select(s => s.Gasto).SingleOrDefaultAsync();
            if (LimitesDeNivel.Gasto(def.Nome, gastoAtual, gasto, tabela.Limite(ChavesDeNivel.MaxPericia, ficha.Nivel), ficha.Nivel) is { } erroDeLimite)
                return BadRequest(erroDeLimite);
        }

        var skill = await Linhas.SingleOrDefaultAsync(s => s.SheetId == sheetId && s.PericiaId == def.Id);
        if (skill is null)
        {
            skill = new TSkill { Id = Guid.NewGuid(), SheetId = sheetId, PericiaId = def.Id };
            Linhas.Add(skill);
        }
        skill.Gasto = gasto;
        skill.AtributoEscolhido = Enum.TryParse<TAtributo>(atributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
        await Db.SaveChangesAsync();

        return NoContent();
    }
}
