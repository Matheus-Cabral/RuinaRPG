using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Atributos da ficha (listar, orçamento e editar um), para os três tipos. A rota fica na subclasse de cada tipo.</summary>
public abstract class AtributosDaFichaController<TSheet, TLinha, TAtributo, TResponse, TUpdate>(
    RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TLinha : class, IAtributoDaFicha<TAtributo>
    where TAtributo : struct, Enum
{
    protected RuinaRpgDbContext Db { get; } = db;
    protected ITabelaDeNiveis TabelaDeNiveis { get; } = tabelaDeNiveis;

    /// <summary>As linhas de Atributo deste tipo de ficha (CharacterAttributes, NpcAttributes, CreatureAttributes).</summary>
    protected abstract DbSet<TLinha> Linhas { get; }

    /// <summary>A lista de Atributos da ficha já calculada, no record de resposta do tipo.</summary>
    protected abstract Task<List<TResponse>> ListarAsync(TSheet ficha);

    protected abstract (int Gasto, int Bonus, bool TemMaestria) Ler(TUpdate request);

    /// <summary>Pontos de Atributo que o Nível da ficha já deu (Tabela de Níveis). A Criatura soma o Rank (Ficha de Criaturas R0005 2.a).</summary>
    protected virtual int PontosDisponiveis(TSheet ficha, ProgressaoDeNivel tabela) => AttributePointBudgetCalculator.Compute(ficha.Nivel, tabela);

    [HttpGet]
    public async Task<ActionResult<List<TResponse>>> List(Guid sheetId)
    {
        var (ficha, erro) = await FichaEditavelAsync(sheetId);
        if (erro is not null)
            return erro;

        return await ListarAsync(ficha!);
    }

    /// <summary>
    /// Pontos de Atributo que o Nível da ficha já deu contra o Gasto somado. No NPC e na Criatura só informa —
    /// o GM pode passar do total (ver Requisitos - Ficha de NPCs R0007); no Personagem o Update recusa (2.a).
    /// </summary>
    [HttpGet("budget")]
    public async Task<ActionResult<AttributePointBudgetResponse>> Budget(Guid sheetId)
    {
        var (ficha, erro) = await FichaEditavelAsync(sheetId);
        if (erro is not null)
            return erro;

        var gastoTotal = await Linhas.Where(a => a.SheetId == sheetId).SumAsync(a => a.Gasto);
        var tabela = await TabelaDeNiveis.ObterAsync();
        return new AttributePointBudgetResponse(gastoTotal, PontosDisponiveis(ficha!, tabela));
    }

    [HttpPut("{atributo}")]
    public async Task<IActionResult> Update(Guid sheetId, TAtributo atributo, TUpdate request)
    {
        var (ficha, erro) = await FichaEditavelAsync(sheetId);
        if (erro is not null)
            return erro;
        var sheet = ficha!;

        var (gasto, bonus, temMaestria) = Ler(request);
        var attribute = await Linhas.SingleAsync(a => a.SheetId == sheetId && a.Atributo.Equals(atributo));

        // "A soma de Gasto de todos os 8 atributos ... não pode ultrapassar o total de pontos que
        // o personagem já recebeu (criação + níveis)" (2.a). Rejected rather than clamped. O
        // Auditor pode reduzir o orçamento da Tabela de Níveis, deixando uma ficha acima dele; por
        // isso a checagem vale só ao SUBIR o Gasto — baixar ou manter (e editar Bônus/Maestria)
        // é sempre permitido.
        if (Tipo.RecusaAtributosAcimaDoOrcamento && gasto > attribute.Gasto)
        {
            var tabela = await TabelaDeNiveis.ObterAsync();
            var gastoDosOutros = await Linhas.Where(a => a.SheetId == sheetId && !a.Atributo.Equals(atributo)).SumAsync(a => a.Gasto);
            var pontosDisponiveis = PontosDisponiveis(sheet, tabela);
            if (gastoDosOutros + gasto > pontosDisponiveis)
                return BadRequest($"Gasto excede os {pontosDisponiveis} pontos de Atributo disponíveis.");
        }
        if (Tipo.AplicaLimitesDeNivel)
        {
            var tabela = await TabelaDeNiveis.ObterAsync();
            if (LimitesDeNivel.Gasto(atributo.ToString(), attribute.Gasto, gasto, tabela.Limite(ChavesDeNivel.MaxAtributo, sheet.Nivel), sheet.Nivel) is { } erroDeLimite)
                return BadRequest(erroDeLimite);
        }

        attribute.Gasto = gasto;
        attribute.Bonus = bonus;
        attribute.TemMaestria = temMaestria;
        await Db.SaveChangesAsync();

        return NoContent();
    }
}
