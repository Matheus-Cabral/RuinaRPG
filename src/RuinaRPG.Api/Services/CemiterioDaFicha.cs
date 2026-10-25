using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Único lugar que diz por que uma Ficha de Personagem está no cemitério (Cemitério de fichas, "Estado e motivos").
/// Lista vazia = fora do cemitério. Autorização, listas, encontros e a resposta da ficha usam isto, nunca recalculam.
/// </summary>
public sealed class CemiterioDaFicha(RuinaRpgDbContext db)
{
    public async Task<IReadOnlyList<MotivoDoCemiterio>> MotivosAsync(CharacterSheet sheet)
    {
        var donoEhMembro = await db.CampaignMembers.AnyAsync(m => m.CampaignId == sheet.CampaignId && m.UserId == sheet.OwnerId);
        return Cemiterio.Motivos(sheet.Morto, donoEhMembro);
    }

    public async Task<Dictionary<Guid, IReadOnlyList<MotivoDoCemiterio>>> MotivosAsync(IReadOnlyCollection<CharacterSheet> sheets)
    {
        var campanhas = sheets.Select(s => s.CampaignId).Distinct().ToList();
        var donos = sheets.Select(s => s.OwnerId).Distinct().ToList();
        var vinculos = (await db.CampaignMembers
                .Where(m => campanhas.Contains(m.CampaignId) && donos.Contains(m.UserId))
                .Select(m => new { m.CampaignId, m.UserId })
                .ToListAsync())
            .Select(m => (m.CampaignId, m.UserId))
            .ToHashSet();

        return sheets.ToDictionary(s => s.Id, s => Cemiterio.Motivos(s.Morto, vinculos.Contains((s.CampaignId, s.OwnerId))));
    }
}
