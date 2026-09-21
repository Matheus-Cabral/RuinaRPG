using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Regras de imagem compartilhadas pelos controllers de Runa (Banco de Runas, Runas de Personagem e de NPC).
/// Uma Runa tem no máximo uma imagem, opcional (Requisitos - Banco de Runas R0005).
/// </summary>
internal static class RuneImageAccess
{
    /// <summary>
    /// null, "" ou só espaços significam "sem imagem" (o cliente manda "" na opção "— nenhuma —"); um Guid
    /// malformado devolve false, para o controller responder 400 em vez de estourar.
    /// </summary>
    public static bool TryParseImageId(string? raw, out Guid? imageId)
    {
        imageId = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (!Guid.TryParse(raw, out var parsed))
            return false;

        imageId = parsed;
        return true;
    }

    /// <summary>
    /// A imagem existe e quem chama pode usá-la: upload próprio, ou — só para um jogador (chamador que não é
    /// o GM) — uma imagem que o GM anexou como pública à campanha. Um Guid alheio/inexistente precisa virar
    /// um 400 controlado, não uma violação de FK (500).
    /// </summary>
    public static async Task<bool> CanUseAsync(RuinaRpgDbContext db, Guid imageId, Guid callerId, Guid gmId, Guid? campaignId)
    {
        var uploadedBy = await db.Images.Where(i => i.Id == imageId).Select(i => (Guid?)i.UploadedByUserId).FirstOrDefaultAsync();
        if (uploadedBy is null)
            return false;

        if (uploadedBy == callerId)
            return true;

        return callerId != gmId
            && campaignId is not null
            && await db.CampaignAttachments.AnyAsync(a => a.CampaignId == campaignId && a.IsPublic && a.ImageId == imageId);
    }

    /// <summary>Resolve "/images/{Path}" de vários ids numa única consulta (evita N+1 nas listagens).</summary>
    public static async Task<Dictionary<Guid, string>> UrlsAsync(RuinaRpgDbContext db, IEnumerable<Guid?> imageIds)
    {
        var ids = imageIds.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await db.Images
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => $"/images/{i.Path}");
    }

    public static string? UrlOf(Dictionary<Guid, string> urls, Guid? imageId) =>
        imageId is not null && urls.TryGetValue(imageId.Value, out var url) ? url : null;
}
