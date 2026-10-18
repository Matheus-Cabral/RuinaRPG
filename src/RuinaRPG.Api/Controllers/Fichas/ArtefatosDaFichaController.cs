using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Artefatos da ficha, para os três tipos. A rota (<c>api/&lt;tipo&gt;-sheets/{sheetId}</c>) fica na subclasse de cada tipo.</summary>
public abstract class ArtefatosDaFichaController<TSheet, TLinha, TResponse, TAdd>(RuinaRpgDbContext db, EquipmentPenaltyService penalidades, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TLinha : class, IArtefatoDaFicha, new()
{
    protected RuinaRpgDbContext Db { get; } = db;
    protected EquipmentPenaltyService Penalidades { get; } = penalidades;

    private DbSet<TLinha> Linhas => Db.Set<TLinha>();

    protected abstract string ArtifactItemIdDe(TAdd request);

    protected abstract TResponse Resposta(TLinha linha, Artefato item, string? imageUrl, AvaliacaoDeEquipamento avaliacao, string? desabilitadoPor);

    /// <summary>O retrato da ficha usado para conferir Requisitos (o Stats do tipo).</summary>
    protected abstract Task<FichaParaRequisitos> FichaParaRequisitosAsync(TSheet ficha);

    [HttpPost("artifacts")]
    public async Task<ActionResult<TResponse>> AddArtifact(Guid sheetId, TAdd request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        if (!Guid.TryParse(ArtifactItemIdDe(request), out var artifactItemId))
            return BadRequest("ArtifactItemId inválido.");

        var newItem = await Db.Set<Artefato>().FirstOrDefaultAsync(a => a.Id == artifactItemId);
        if (newItem is null)
            return BadRequest("Item de artefato não encontrado.");

        // Modelo de Dados: "limite de 3 por TipoDeAlvo validado na aplicação, não no schema".
        var existingOfSameType = await Linhas
            .Where(a => a.SheetId == sheetId)
            .Join(Db.Set<Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i.TipoDeAlvo)
            .CountAsync(t => t == newItem.TipoDeAlvo);
        if (existingOfSameType >= 3)
            return BadRequest($"Limite de 3 Artefatos do tipo {newItem.TipoDeAlvo} já atingido.");

        var artifact = new TLinha { Id = Guid.NewGuid(), SheetId = sheetId, ArtifactItemId = artifactItemId };
        Linhas.Add(artifact);
        await Db.SaveChangesAsync();

        return Created(string.Empty, await RespostaAsync(ficha, artifact));
    }

    [HttpGet("artifacts")]
    public async Task<ActionResult<List<TResponse>>> ListArtifacts(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var artifacts = await Linhas.Where(a => a.SheetId == sheetId).ToListAsync();
        var responses = new List<TResponse>();
        foreach (var artifact in artifacts)
            responses.Add(await RespostaAsync(ficha, artifact));
        return responses;
    }

    [HttpDelete("artifacts/{id}")]
    public async Task<IActionResult> DeleteArtifact(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var artifact = await Linhas.FirstOrDefaultAsync(a => a.Id == id && a.SheetId == sheetId);
        if (artifact is null)
            return NotFound();

        Linhas.Remove(artifact);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<TResponse> RespostaAsync(TSheet ficha, TLinha artifact)
    {
        var item = await Db.Set<Artefato>().SingleAsync(a => a.Id == artifact.ArtifactItemId);
        var imageUrl = await UrlDaImagemAsync(Db, item.ImageId);
        var avaliacao = await Penalidades.AvaliarAsync(item, () => _fichaSemPenalidades ??= FichaParaRequisitosAsync(ficha));
        // Condições ativas da ficha que desabilitam esta linha (null se nenhuma). O Id é o da linha da ficha, o mesmo de itens-desabilitaveis.
        var desabilitadoPor = (await Tipo.CondicoesAsync(ficha)).DesabilitadoPor(CategoriaDesabilitavel.Artefato, artifact.Id);
        return Resposta(artifact, item, imageUrl, avaliacao, desabilitadoPor);
    }

    // O retrato da ficha SEM penalidades é calculado uma vez por requisição, e só se algum Artefato tiver Requisitos.
    private Task<FichaParaRequisitos>? _fichaSemPenalidades;
}
