using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Regras de imagem da aba História, compartilhadas pelas fichas de Personagem e de NPC — a galeria
/// (…/historia/imagens) e as imagens dentro do texto (…/historia).
/// <para>
/// Quem salva só pode pôr na ficha uma imagem que pode usar: upload próprio ou — só para um jogador — uma
/// imagem que o GM anexou como pública à campanha (a mesma regra de <see cref="RuneImageAccess.CanUseAsync"/>).
/// Uma imagem que JÁ está na ficha continua valendo para qualquer um que possa editá-la: GM e jogador editam
/// a mesma História, e um não pode apagar as imagens do outro só por salvar.
/// </para>
/// Exibir não depende de nada disso: as imagens são servidas pelo nginx em /images/{arquivo}, sem
/// autenticação e com nome impossível de adivinhar, então GM e jogador veem as mesmas URLs.
/// </summary>
internal static class HistoriaImageAccess
{
    public const int MaxImagens = 50;
    public const string MaxImagensMessage = "A galeria da História pode ter no máximo 50 imagens.";
    public const string InvalidIdMessage = "Um dos identificadores de imagem informados é inválido.";
    public const string NotFoundMessage = "Imagem não encontrada.";

    /// <summary>Ids na ordem recebida, sem repetição; false se algum não é um Guid. Lista nula = galeria vazia.</summary>
    public static bool TryParseIds(List<string>? raw, out List<Guid> ids)
    {
        ids = [];
        foreach (var value in raw ?? [])
        {
            if (!Guid.TryParse(value, out var id))
                return false;
            if (!ids.Contains(id))
                ids.Add(id);
        }
        return true;
    }

    /// <summary>Entre <paramref name="imageIds"/>, as imagens que existem e que <paramref name="callerId"/> pode usar.</summary>
    public static async Task<HashSet<Guid>> UsableAsync(RuinaRpgDbContext db, IReadOnlyCollection<Guid> imageIds, Guid callerId, Guid gmId, Guid? campaignId)
    {
        if (imageIds.Count == 0)
            return [];

        var usable = (await db.Images.Where(i => imageIds.Contains(i.Id) && i.UploadedByUserId == callerId).Select(i => i.Id).ToListAsync()).ToHashSet();
        if (callerId != gmId && campaignId is not null && usable.Count < imageIds.Count)
        {
            usable.UnionWith(await db.CampaignAttachments
                .Where(a => a.CampaignId == campaignId && a.IsPublic && a.ImageId != null && imageIds.Contains(a.ImageId.Value))
                .Select(a => a.ImageId!.Value)
                .ToListAsync());
        }
        return usable;
    }

    /// <summary>
    /// Os arquivos de imagem ("{guid}.{ext}") que podem ficar dentro do texto neste salvamento: os que a
    /// História gravada já traz em &lt;img&gt; e os que quem salva pode usar. O resto o sanitizador remove.
    /// </summary>
    public static async Task<IReadOnlySet<string>> AllowedInlineFilesAsync(RuinaRpgDbContext db, string? incomingHtml, string? storedHtml, Guid callerId, Guid gmId, Guid? campaignId)
    {
        var requested = HistoriaSanitizer.ImageFiles(incomingHtml);
        if (requested.Count == 0)
            return requested;

        var alreadyThere = HistoriaSanitizer.ImageFiles(storedHtml);
        var allowed = requested.Where(alreadyThere.Contains).ToHashSet(StringComparer.Ordinal);
        var toCheck = requested.Where(f => !alreadyThere.Contains(f)).ToList();
        if (toCheck.Count == 0)
            return allowed;

        var images = await db.Images.Where(i => toCheck.Contains(i.Path)).Select(i => new { i.Id, i.Path }).ToListAsync();
        var usable = await UsableAsync(db, images.Select(i => i.Id).ToList(), callerId, gmId, campaignId);
        allowed.UnionWith(images.Where(i => usable.Contains(i.Id)).Select(i => i.Path));
        return allowed;
    }

    /// <summary>A galeria na ordem de <paramref name="orderedIds"/>, com a URL de cada imagem.</summary>
    public static async Task<List<HistoriaImagemResponse>> ToResponseAsync(RuinaRpgDbContext db, IReadOnlyList<Guid> orderedIds)
    {
        var urls = await RuneImageAccess.UrlsAsync(db, orderedIds.Select(id => (Guid?)id));
        return orderedIds.Where(urls.ContainsKey).Select(id => new HistoriaImagemResponse(id.ToString(), urls[id])).ToList();
    }
}
