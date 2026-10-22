using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Runes;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Runes;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Runas da ficha (Personagem e NPC). A rota (<c>api/&lt;tipo&gt;-sheets/{sheetId}/runes</c>) fica na subclasse de cada tipo.
/// </summary>
public abstract class RunasDaFichaController<TSheet, TLinha>(RuinaRpgDbContext db, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TLinha : class, IRunaDaFicha
{
    protected RuinaRpgDbContext Db { get; } = db;

    private DbSet<TLinha> Linhas => Db.Set<TLinha>();

    protected abstract (string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId, string? ImageId, string? Tipo, string? Disciplina) Ler(AddRuneRequest request);

    /// <summary>Uma linha nova (os membros <c>required</c>); o resto é preenchido pela base.</summary>
    protected abstract TLinha Nova(string nome, string descricao);

    protected abstract RuneResponse Resposta(TLinha runa, string? imageUrl, string? desabilitadoPor);

    [HttpPost]
    public async Task<ActionResult<RuneResponse>> Add(Guid sheetId, AddRuneRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var pedido = Ler(request);
        var callerId = CurrentUserId();
        var fromScratch = pedido.Nome is not null && pedido.Descricao is not null && pedido.Grau is not null;
        var fromBank = pedido.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // os dois ou nenhum
            return BadRequest("Informe exatamente um: Nome, Descricao e Grau para montar do zero, ou SourceBankEntryId.");

        // Só o jogador (quem chama não é o Mestre) precisa da campanha — tanto para escolher do banco (só as
        // entradas públicas nela) quanto para usar imagens dela e publicar depois a cópia que vai para o banco.
        var gmId = await Tipo.MestreAsync(sheet);
        Guid? campaignId = null;
        if (callerId != gmId)
            campaignId = await Tipo.CampanhaAsync(sheet);

        string nome, descricao;
        int grau;
        TipoDeRuna? tipo;
        DisciplinaDeRuna? disciplina;
        Guid? sourceBankEntryId = null;
        Guid? imageId = null;

        if (fromBank)
        {
            if (!Guid.TryParse(pedido.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            var bankEntry = await Db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == bankEntryId && e.GmId == gmId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            // O GM escolhe qualquer entrada do próprio banco; um jogador só alcança as que o GM anexou
            // à campanha da ficha como públicas (Requisitos - Banco de Runas R0003/R0008).
            if (callerId != gmId
                && (campaignId is null
                    || !await Db.CampaignAttachments.AnyAsync(a => a.CampaignId == campaignId && a.IsPublic && a.RuneBankEntryId == bankEntryId)))
                return BadRequest("Entrada do banco não encontrada.");

            // Ao partir do banco a imagem vem da entrada (Requisitos - Banco de Runas R0005).
            if (!string.IsNullOrWhiteSpace(pedido.ImageId))
                return BadRequest("Ao escolher do banco, a imagem vem da entrada.");

            nome = bankEntry.Nome; descricao = bankEntry.Descricao; grau = bankEntry.Grau; tipo = bankEntry.Tipo;
            disciplina = bankEntry.Disciplina;
            sourceBankEntryId = bankEntryId;
            imageId = bankEntry.ImageId;
        }
        else
        {
            if (!RuneTipo.TryParse(pedido.Tipo, out tipo))
                return BadRequest(RuneTipo.UnknownMessage);

            if (!RuneDisciplina.TryParse(pedido.Disciplina, out disciplina))
                return BadRequest(RuneDisciplina.UnknownMessage);
            if (disciplina is null)
                return BadRequest(RuneDisciplina.RequiredMessage);

            if (!RuneImageAccess.TryParseImageId(pedido.ImageId, out imageId))
                return BadRequest("ImageId inválido.");

            if (imageId is not null && !await RuneImageAccess.CanUseAsync(Db, imageId.Value, callerId, gmId, campaignId))
                return BadRequest("Imagem não encontrada.");

            nome = pedido.Nome!; descricao = pedido.Descricao!; grau = pedido.Grau!.Value;
        }

        var rune = Nova(nome, descricao);
        rune.Id = Guid.NewGuid();
        rune.SheetId = sheetId;
        rune.Grau = grau;
        rune.Tipo = tipo;
        rune.Disciplina = disciplina;
        rune.SourceBankEntryId = sourceBankEntryId;
        rune.ImageId = imageId;
        Linhas.Add(rune);

        // Requisitos - Banco de Runas R0001: uma criação do zero grava também uma cópia independente no
        // banco do GM, e a Runa da ficha guarda o vínculo com ela. Partir de uma entrada do banco (R0003) só
        // copia para a ficha — reusa a entrada, sem duplicá-la; para um jogador ela já é pública na campanha.
        if (!fromBank)
        {
            var bankCopy = new RuneBankEntry { Id = Guid.NewGuid(), GmId = gmId, Nome = nome, Descricao = descricao, Grau = grau, Tipo = tipo, Disciplina = disciplina, ImageId = imageId };
            Db.RuneBankEntries.Add(bankCopy);
            rune.SourceBankEntryId = bankCopy.Id;

            // R0007: quando quem cria é o jogador (não o GM), a cópia vira anexo público da campanha
            // da ficha (Requisitos - Campanha R0012) — se houver uma.
            if (callerId != gmId && campaignId is not null)
            {
                Db.CampaignAttachments.Add(new CampaignAttachment
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId.Value,
                    RuneBankEntryId = bankCopy.Id,
                    IsPublic = true
                });
            }
        }

        await Db.SaveChangesAsync();
        return Created(string.Empty, Resposta(rune, await RuneImageAccess.UrlsAsync(Db, [rune.ImageId]), await Tipo.CondicoesAsync(sheet)));
    }

    [HttpGet]
    public async Task<ActionResult<List<RuneResponse>>> List(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var runes = await Linhas.Where(r => r.SheetId == sheetId).ToListAsync();
        var urls = await RuneImageAccess.UrlsAsync(Db, runes.Select(r => r.ImageId));
        var efeitos = await Tipo.CondicoesAsync(sheet);
        return runes.Select(r => Resposta(r, urls, efeitos)).ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var rune = await Linhas.FirstOrDefaultAsync(r => r.Id == id && r.SheetId == sheetId);
        if (rune is null)
            return NotFound();

        Linhas.Remove(rune);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    private RuneResponse Resposta(TLinha r, Dictionary<Guid, string> imageUrls, EfeitosDeCondicao efeitos) =>
        Resposta(r, RuneImageAccess.UrlOf(imageUrls, r.ImageId), efeitos.DesabilitadoPor(CategoriaDesabilitavel.Runa, r.Id));
}
