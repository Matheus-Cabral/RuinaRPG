using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Endpoints da ficha que só Personagem e NPC têm (a Criatura não tem Linhagem, Arca nem aba História): Habilidade
/// Racial, Variantes liberadas e a aba História com a sua galeria. As leituras negam com 404 nos dois tipos.
/// </summary>
public abstract class FichaComLinhagemController<TSheet, TStats, TImagem>(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<TSheet> tipo, TStats stats, EquipmentPenaltyService penalidades, IHubContext<EncounterHub> hub, ILogger logger)
    : FichasController<TSheet, TStats>(db, tabelaDeNiveis, tipo, stats, penalidades, hub, logger)
    where TSheet : class, IFichaComLinhagem
    where TStats : IFichaStats<TSheet>
    where TImagem : class, IImagemDaHistoria, new()
{
    private DbSet<TImagem> Imagens => Db.Set<TImagem>();

    [HttpGet("{id}/racial-ability")]
    public async Task<ActionResult<RacialAbilityResponse>> RacialAbility(Guid id)
    {
        if ((await FichaParaLeituraAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        var gmId = await Tipo.MestreAsync(sheet);
        var arcaDado = await Db.DadoDeArcaDoGmAsync(gmId);
        if (sheet.Variante is null)
            return new RacialAbilityResponse(null, null, null, null, null, new(), arcaDado);

        var over = await Db.RacialAbilityOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == sheet.Variante.Value);
        var (nome, descricao) = over is not null
            ? (over.Nome, over.Descricao)
            : (RacialAbilityLookup.For(sheet.Variante.Value, arcaDado).Nome, RacialAbilityLookup.For(sheet.Variante.Value, arcaDado).Descricao);

        string? arcaNome = null;
        string? arcaDescricao = null;
        var arcaEvolucoes = new List<ArcaEvolucaoResponse>();
        var arcaForaDaTabela = sheet.Linhagem == Linhagem.Humano && sheet.ArcaRolada > arcaDado;
        if (sheet.Linhagem == Linhagem.Humano && sheet.ArcaRolada is not null && !arcaForaDaTabela)
        {
            var arca = await Db.ArcaEntries.Include(a => a.Evolucoes)
                .FirstOrDefaultAsync(a => a.GmId == gmId && a.Roll == sheet.ArcaRolada.Value);
            // Uma Arca criada só para guardar evoluções tem Nome vazio — a ficha a trata como não cadastrada.
            arcaNome = string.IsNullOrEmpty(arca?.Nome) ? null : arca.Nome;
            arcaDescricao = string.IsNullOrEmpty(arca?.Descricao) ? null : arca.Descricao;
            if (arca is not null)
                arcaEvolucoes = ArcaEvolucaoRules.Desbloqueadas(arca.Evolucoes, e => e.Nivel, e => e.CriadaEm, sheet.Nivel)
                    .Select(e => new ArcaEvolucaoResponse(e.Id, e.Nivel, e.Descricao)).ToList();
        }

        return new RacialAbilityResponse(nome, descricao, sheet.ArcaRolada, arcaNome, arcaDescricao, arcaEvolucoes, arcaDado, arcaForaDaTabela);
    }

    [HttpGet("{id}/variantes-liberadas")]
    public async Task<ActionResult<List<VarianteLiberadaResponse>>> VariantesLiberadas(Guid id)
    {
        if ((await FichaParaLeituraAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        return await VarianteLiberadaResolver.ListAsync(Db, await Tipo.MestreAsync(sheet));
    }

    /// <summary>
    /// Aba História — fora do Update geral de propósito, para o salvamento automático (disparado por qualquer outro
    /// campo) nunca sobrescrever a história com uma cópia desatualizada, e vice-versa. Mesma autorização do Update.
    /// </summary>
    [HttpPut("{id}/historia")]
    public async Task<IActionResult> UpdateHistoria(Guid id, UpdateHistoriaRequest request)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        // O limite vale para o que é gravado (sanitizar pode aumentar valores de CSS); o limite bruto só limita o trabalho.
        if (request.Historia is { Length: > HistoriaSanitizer.MaxRawLength })
            return BadRequest(HistoriaSanitizer.MaxLengthMessage);

        // Imagens dentro do texto: só as do app que quem salva pode usar (ou que a História já trazia) —
        // qualquer outro <img> é removido, sem erro (HistoriaSanitizer / HistoriaImageAccess).
        var gmId = await Tipo.MestreAsync(sheet);
        var imagens = await HistoriaImageAccess.AllowedInlineFilesAsync(Db, request.Historia, sheet.Historia, CurrentUserId(), gmId, await CampanhaParaImagensAsync(sheet, gmId));
        var clean = HistoriaSanitizer.Sanitize(request.Historia, imagens);
        if (clean is { Length: > HistoriaSanitizer.MaxLength })
            return BadRequest(HistoriaSanitizer.MaxLengthMessage);

        sheet.Historia = clean;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Aba História — a galeria de imagens anexadas, na ordem em que aparecem. Quem pode abrir a ficha pode ver.</summary>
    [HttpGet("{id}/historia/imagens")]
    public async Task<ActionResult<List<HistoriaImagemResponse>>> HistoriaImagens(Guid id)
    {
        if ((await FichaParaLeituraAsync(id)).Negado(out var erro, out _))
            return erro;

        var ids = await Imagens.Where(i => i.SheetId == id).OrderBy(i => i.Ordem).Select(i => i.ImageId).ToListAsync();
        return await HistoriaImageAccess.ToResponseAsync(Db, ids);
    }

    /// <summary>
    /// Aba História — substitui a galeria pela lista recebida (anexar, remover e reordenar são o mesmo PUT).
    /// Mesma autorização de UpdateHistoria; uma imagem nova na galeria precisa ser de quem salva
    /// (HistoriaImageAccess), senão 400 e nada muda.
    /// </summary>
    [HttpPut("{id}/historia/imagens")]
    public async Task<ActionResult<List<HistoriaImagemResponse>>> UpdateHistoriaImagens(Guid id, UpdateHistoriaImagensRequest request)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        if (!HistoriaImageAccess.TryParseIds(request.ImageIds, out var ids))
            return BadRequest(HistoriaImageAccess.InvalidIdMessage);
        if (ids.Count > HistoriaImageAccess.MaxImagens)
            return BadRequest(HistoriaImageAccess.MaxImagensMessage);

        var existing = await Imagens.Where(i => i.SheetId == id).ToListAsync();
        var novas = ids.Where(imageId => existing.All(e => e.ImageId != imageId)).ToList();
        var gmId = await Tipo.MestreAsync(sheet);
        if ((await HistoriaImageAccess.UsableAsync(Db, novas, CurrentUserId(), gmId, await CampanhaParaImagensAsync(sheet, gmId))).Count != novas.Count)
            return BadRequest(HistoriaImageAccess.NotFoundMessage);

        Imagens.RemoveRange(existing.Where(e => !ids.Contains(e.ImageId)));
        for (var ordem = 0; ordem < ids.Count; ordem++)
        {
            var imageId = ids[ordem];
            if (existing.FirstOrDefault(e => e.ImageId == imageId) is { } row)
                row.Ordem = ordem;
            else
                Imagens.Add(new TImagem { SheetId = id, ImageId = imageId, Ordem = ordem });
        }
        await Db.SaveChangesAsync();

        return await HistoriaImageAccess.ToResponseAsync(Db, ids);
    }

    /// <summary>
    /// A campanha cujas imagens públicas quem salva também pode usar — só interessa quando quem chama não é o Mestre
    /// (HistoriaImageAccess ignora a campanha para ele); null para o Mestre.
    /// </summary>
    private async Task<Guid?> CampanhaParaImagensAsync(TSheet sheet, Guid gmId) =>
        CurrentUserId() == gmId ? null : await Tipo.CampanhaAsync(sheet);
}
