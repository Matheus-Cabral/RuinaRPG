using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Linhas de Afinidade da ficha (Personagem e NPC). A rota (<c>api/&lt;tipo&gt;-sheets/{sheetId}/affinities</c>)
/// fica na subclasse de cada tipo.
/// </summary>
public abstract class AfinidadesDaFichaController<TSheet, TLinha>(RuinaRpgDbContext db, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TLinha : class, IAfinidadeDaFicha, new()
{
    protected RuinaRpgDbContext Db { get; } = db;

    private DbSet<TLinha> Linhas => Db.Set<TLinha>();

    protected abstract PedidoDeAfinidade LerNova(AddAffinityRequest request);

    protected abstract PedidoDeAfinidade LerAlteracao(UpdateAffinityRequest request);

    protected abstract AffinityResponse Resposta(TLinha linha, int? elementoValorComCondicoes);

    /// <summary>
    /// A recusa por Vocação Arcana (Ficha de Personagem R0007), ou null. Por padrão nenhuma: no NPC o máximo só
    /// informa (Ficha de NPCs R0010).
    /// </summary>
    protected virtual Task<string?> ExcedeVocacaoArcanaAsync(TSheet ficha, LinhaDeAfinidade novaLinha, Guid? substituindoId) =>
        Task.FromResult<string?>(null);

    /// <summary>Os campos comuns aos pedidos de criar e de alterar uma linha.</summary>
    protected sealed record PedidoDeAfinidade(string? Elemento, int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor, int? SubElementoValor, int? Experiencia);

    [HttpPost]
    public async Task<ActionResult<AffinityResponse>> Add(Guid sheetId, AddAffinityRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var pedido = LerNova(request);
        if (!TryParseEssencias(pedido.Elemento, pedido.SegundaEssencia, out var elemento, out var segunda, out var error))
            return BadRequest(error);

        var subElemento = Derivar(elemento, segunda);
        if (await HasDuplicateSubElementoAsync(sheetId, subElemento, excludingId: null))
            return BadRequest("Já existe uma linha de Afinidade com esse Sub-Elemento.");

        var novaLinha = new LinhaDeAfinidade(elemento, pedido.ElementoValor, subElemento, pedido.SubElementoValor, segunda, pedido.SegundaEssenciaValor);
        if (await ExcedeVocacaoArcanaAsync(sheet, novaLinha, substituindoId: null) is { } excesso)
            return BadRequest(excesso);

        var affinity = new TLinha
        {
            Id = Guid.NewGuid(), SheetId = sheetId, Elemento = elemento, ElementoValor = pedido.ElementoValor,
            SegundaEssencia = segunda, SegundaEssenciaValor = pedido.SegundaEssenciaValor,
            SubElemento = subElemento, SubElementoValor = pedido.SubElementoValor, Experiencia = pedido.Experiencia
        };
        Linhas.Add(affinity);
        await Db.SaveChangesAsync();

        return Created(string.Empty, Resposta(affinity, await Tipo.CondicoesAsync(sheet)));
    }

    [HttpGet]
    public async Task<ActionResult<List<AffinityResponse>>> List(Guid sheetId)
    {
        if ((await FichaParaLeituraAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var affinities = await Linhas.Where(a => a.SheetId == sheetId).ToListAsync();
        var efeitos = await Tipo.CondicoesAsync(sheet);
        return affinities.Select(a => Resposta(a, efeitos)).ToList();
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AffinityResponse>> Update(Guid sheetId, Guid id, UpdateAffinityRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var affinity = await Linhas.FirstOrDefaultAsync(a => a.Id == id && a.SheetId == sheetId);
        if (affinity is null)
            return NotFound();

        var pedido = LerAlteracao(request);
        if (!TryParseEssencias(pedido.Elemento, pedido.SegundaEssencia, out var elemento, out var segunda, out var error))
            return BadRequest(error);

        var essenciaMudou = elemento != affinity.Elemento || segunda != affinity.SegundaEssencia;
        var subElemento = essenciaMudou ? Derivar(elemento, segunda) : affinity.SubElemento;
        if (subElemento != affinity.SubElemento && await HasDuplicateSubElementoAsync(sheetId, subElemento, id))
            return BadRequest("Já existe uma linha de Afinidade com esse Sub-Elemento.");

        var novaLinha = new LinhaDeAfinidade(elemento, pedido.ElementoValor, subElemento, pedido.SubElementoValor, segunda, pedido.SegundaEssenciaValor);
        if (await ExcedeVocacaoArcanaAsync(sheet, novaLinha, substituindoId: id) is { } excesso)
            return BadRequest(excesso);

        affinity.Elemento = elemento;
        affinity.ElementoValor = pedido.ElementoValor;
        affinity.SegundaEssencia = segunda;
        affinity.SegundaEssenciaValor = pedido.SegundaEssenciaValor;
        affinity.SubElemento = subElemento;
        affinity.SubElementoValor = pedido.SubElementoValor;
        affinity.Experiencia = pedido.Experiencia;
        await Db.SaveChangesAsync();

        return Ok(Resposta(affinity, await Tipo.CondicoesAsync(sheet)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var affinity = await Linhas.FirstOrDefaultAsync(a => a.Id == id && a.SheetId == sheetId);
        if (affinity is null)
            return NotFound();

        Linhas.Remove(affinity);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    // Essência 1 e 2 são opcionais (linha em branco continua valendo). O Sub-Elemento nunca vem do
    // cliente: é a interseção na Matriz Elemental (Requisitos - Ficha de Personagem 2.c).
    private static bool TryParseEssencias(string? elementoRaw, string? segundaRaw,
        out Elemento? elemento, out EssenciaBasica? segunda, out string? error)
    {
        elemento = null;
        segunda = null;
        error = null;

        if (!string.IsNullOrEmpty(elementoRaw))
        {
            if (!Enum.TryParse<Elemento>(elementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Elemento desconhecido.";
                return false;
            }
            elemento = parsed;
        }

        if (!string.IsNullOrEmpty(segundaRaw))
        {
            if (!Enum.TryParse<EssenciaBasica>(segundaRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Essência Básica desconhecida.";
                return false;
            }
            segunda = parsed;
        }

        if (segunda is not null && elemento is null)
        {
            error = "Escolha a Essência Básica 1 antes da 2.";
            return false;
        }

        if (elemento is { } e1 && segunda is { } e2 && MatrizElemental.Intersecao(e1, e2) is null)
        {
            error = "Essas duas Essências não se cruzam na Matriz Elemental.";
            return false;
        }

        return true;
    }

    private static SubElemento? Derivar(Elemento? elemento, EssenciaBasica? segunda) =>
        elemento is { } e1 && segunda is { } e2 ? MatrizElemental.Intersecao(e1, e2) : null;

    private async Task<bool> HasDuplicateSubElementoAsync(Guid sheetId, SubElemento? subElemento, Guid? excludingId) =>
        subElemento is not null && await Linhas.AnyAsync(a =>
            a.SheetId == sheetId && a.SubElemento == subElemento && (excludingId == null || a.Id != excludingId));

    // O valor guardado segue intacto em ElementoValor (é o que a tela edita e o contador de Vocação Arcana soma);
    // o que as Condições mudam vai à parte, só para exibir.
    private AffinityResponse Resposta(TLinha a, EfeitosDeCondicao efeitos) => Resposta(a, efeitos.NaAfinidade(a.Elemento is { } e ? ConversaoDeLinhas.IdDoElemento(e) : null, a.ElementoValor));
}
