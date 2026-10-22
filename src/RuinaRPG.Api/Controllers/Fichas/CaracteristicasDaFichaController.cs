using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Uma Característica do catálogo, venha ela de Trait ou de CreatureExclusiveTrait: tipos C# sem relação (duas tabelas),
/// com a mesma forma que interessa às respostas.
/// </summary>
public sealed record CaracteristicaDoCatalogo(Guid Id, string Nome, string Descricao, int Custo, Polaridade Polaridade, bool RequerEspecificacao)
{
    public static CaracteristicaDoCatalogo De(Trait t) => new(t.Id, t.Nome, t.Descricao, t.Custo, t.Polaridade, t.RequerEspecificacao);

    public static CaracteristicaDoCatalogo De(CreatureExclusiveTrait t) => new(t.Id, t.Nome, t.Descricao, t.Custo, t.Polaridade, t.RequerEspecificacao);
}

/// <summary>
/// Características da ficha (adicionar, listar com o orçamento, apagar), para os três tipos. A rota fica na subclasse
/// de cada tipo. Personagem e NPC usam o catálogo comum e têm traços raciais
/// (<see cref="CaracteristicasComRaciaisDaFichaController{TSheet, TLinha}"/>); a Criatura só
/// adiciona do catálogo exclusivo de criatura.
/// </summary>
public abstract class CaracteristicasDaFichaController<TSheet, TLinha, TResponse, TLista>(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TLinha : class, ILinhaDaFicha
{
    protected RuinaRpgDbContext Db { get; } = db;

    protected DbSet<TLinha> Linhas => Db.Set<TLinha>();

    protected abstract (string TraitId, string? Especificacao) Ler(AddTraitRequest request);

    /// <summary>A Característica do catálogo que este tipo pode adicionar, ou null.</summary>
    protected abstract Task<CaracteristicaDoCatalogo?> CaracteristicaParaAdicionarAsync(Guid traitId);

    /// <summary>O 400 quando <see cref="CaracteristicaParaAdicionarAsync"/> não achou a Característica.</summary>
    protected virtual Task<ActionResult> CaracteristicaNaoEncontradaAsync(Guid traitId) =>
        Task.FromResult<ActionResult>(BadRequest("Trait não encontrado."));

    /// <summary>A soma do Custo das Características da ficha com essa Polaridade, a que conta para o orçamento.</summary>
    protected abstract Task<int> SomaDoCustoAsync(Guid sheetId, Polaridade polaridade);

    protected abstract TLinha Nova(Guid sheetId, CaracteristicaDoCatalogo caracteristica, string? especificacao);

    /// <summary>As Características da ficha com a entrada do catálogo de cada uma (linhas sem entrada ficam de fora).</summary>
    protected abstract Task<List<(TLinha Linha, CaracteristicaDoCatalogo Caracteristica)>> LinhasComCatalogoAsync(Guid sheetId);

    /// <summary>O Custo da linha na ficha (o traço racial do Personagem e do NPC custa 0).</summary>
    protected virtual int CustoNaFicha(TLinha linha, CaracteristicaDoCatalogo caracteristica) => caracteristica.Custo;

    protected abstract TResponse Resposta(TLinha linha, CaracteristicaDoCatalogo caracteristica, int custo);

    protected abstract TLista Lista(List<TResponse> positivas, int totalPositivas, List<TResponse> negativas, int totalNegativas, int pontosDisponiveis, int pontosDisponiveisNegativas);

    [HttpPost("traits")]
    public async Task<ActionResult<TResponse>> AddTrait(Guid sheetId, AddTraitRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var (traitIdPedido, especificacao) = Ler(request);
        if (!Guid.TryParse(traitIdPedido, out var traitId))
            return BadRequest("TraitId inválido.");

        var trait = await CaracteristicaParaAdicionarAsync(traitId);
        if (trait is null)
            return await CaracteristicaNaoEncontradaAsync(traitId);

        if (trait.RequerEspecificacao && string.IsNullOrWhiteSpace(especificacao))
            return BadRequest("Esta característica exige uma especificação.");

        // Características 5.d: cada lista tem seu orçamento, aplicado à parte — o total das Positivas não passa do
        // orçamento positivo, e o das Negativas (em módulo; o Custo é guardado negativo) não passa do negativo, o dobro
        // do positivo. Espelha a regra dos Atributos (2.a).
        var tabela = await tabelaDeNiveis.ObterAsync();
        var pontosDisponiveis = TraitPointBudgetCalculator.Para(trait.Polaridade, ficha.Nivel, tabela);
        var existingTotal = await SomaDoCustoAsync(sheetId, trait.Polaridade);
        if (Math.Abs(existingTotal) + Math.Abs(trait.Custo) > pontosDisponiveis)
            return BadRequest($"Gasto excede os {pontosDisponiveis} pontos de Característica {trait.Polaridade} disponíveis.");

        var linha = Nova(sheetId, trait, trait.RequerEspecificacao ? especificacao : null);
        Linhas.Add(linha);
        await Db.SaveChangesAsync();

        return Created(string.Empty, Resposta(linha, trait, CustoNaFicha(linha, trait)));
    }

    [HttpGet("traits")]
    public async Task<ActionResult<TLista>> ListTraits(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var rows = (await LinhasComCatalogoAsync(sheetId))
            .Select(x =>
            {
                var custo = CustoNaFicha(x.Linha, x.Caracteristica);
                return (x.Caracteristica.Polaridade, Custo: custo, Resposta: Resposta(x.Linha, x.Caracteristica, custo));
            })
            .ToList();

        var positivas = rows.Where(r => r.Polaridade == Polaridade.Positiva).ToList();
        var negativas = rows.Where(r => r.Polaridade == Polaridade.Negativa).ToList();
        var tabela = await tabelaDeNiveis.ObterAsync();
        var pontosDisponiveis = TraitPointBudgetCalculator.Compute(ficha.Nivel, tabela);
        return Lista(positivas.Select(r => r.Resposta).ToList(), positivas.Sum(r => r.Custo), negativas.Select(r => r.Resposta).ToList(), negativas.Sum(r => r.Custo),
            pontosDisponiveis, TraitPointBudgetCalculator.ComputeNegativas(ficha.Nivel, tabela));
    }

    [HttpDelete("traits/{id}")]
    public async Task<IActionResult> DeleteTrait(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var linha = await Linhas.FirstOrDefaultAsync(t => t.Id == id && t.SheetId == sheetId);
        if (linha is null)
            return NotFound();

        Linhas.Remove(linha);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Características do Personagem e do NPC: só o catálogo comum, e os traços raciais custam 0 e ficam fora do orçamento.</summary>
public abstract class CaracteristicasComRaciaisDaFichaController<TSheet, TLinha>(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<TSheet> tipo)
    : CaracteristicasDaFichaController<TSheet, TLinha, TraitDaFichaResponse, TraitsListResponse>(db, tabelaDeNiveis, tipo)
    where TSheet : class, IFicha
    where TLinha : class, ICaracteristicaDaFicha, new()
{
    protected override async Task<CaracteristicaDoCatalogo?> CaracteristicaParaAdicionarAsync(Guid traitId) =>
        await Db.Traits.FirstOrDefaultAsync(t => t.Id == traitId && !t.IsDeleted) is { } trait ? CaracteristicaDoCatalogo.De(trait) : null;

    // Os traços raciais (IsRacial) ficam de fora: custam 0 e nunca contam para este orçamento, seja qual for o Custo
    // do Trait (Requisitos - Ficha de Personagem 5.d; Ficha de NPCs igual).
    protected override Task<int> SomaDoCustoAsync(Guid sheetId, Polaridade polaridade) =>
        Linhas
            .Where(t => t.SheetId == sheetId && t.Polaridade == polaridade && !t.IsRacial)
            .Join(Db.Traits, ct => ct.TraitId, t => t.Id, (ct, t) => t.Custo)
            .SumAsync();

    protected override TLinha Nova(Guid sheetId, CaracteristicaDoCatalogo caracteristica, string? especificacao) =>
        new() { Id = Guid.NewGuid(), SheetId = sheetId, TraitId = caracteristica.Id, Polaridade = caracteristica.Polaridade, Especificacao = especificacao };

    protected override async Task<List<(TLinha Linha, CaracteristicaDoCatalogo Caracteristica)>> LinhasComCatalogoAsync(Guid sheetId) =>
        (await Linhas
            .Where(t => t.SheetId == sheetId)
            .Join(Db.Traits, ct => ct.TraitId, t => t.Id, (ct, t) => new { ct, t })
            .ToListAsync())
            .Select(x => (x.ct, CaracteristicaDoCatalogo.De(x.t)))
            .ToList();

    protected override int CustoNaFicha(TLinha linha, CaracteristicaDoCatalogo caracteristica) => linha.IsRacial ? 0 : caracteristica.Custo;
}
