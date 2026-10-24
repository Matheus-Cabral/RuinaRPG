using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Smoke;

public record SmokeRequest(Guid UserId, string Path);

public static class SmokePlan
{
    /// <summary>Monta a lista de GETs a partir de leituras apenas: nada é escrito.</summary>
    public static async Task<IReadOnlyList<SmokeRequest>> BuildAsync(RuinaRpgDbContext db)
    {
        var plan = new List<SmokeRequest>();

        var campaigns = await db.Campaigns.AsNoTracking().Select(c => new { c.Id, c.GmId }).ToListAsync();
        var gmByCampaign = campaigns.ToDictionary(c => c.Id, c => c.GmId);

        foreach (var c in campaigns)
        {
            plan.Add(new(c.GmId, "/api/campaigns"));
            foreach (var suffix in new[] { "members", "character-sheets", "encounters", "diary", "available-items" })
                plan.Add(new(c.GmId, $"/api/campaigns/{c.Id}/{suffix}"));
        }

        var sheets = await db.CharacterSheets.AsNoTracking().Select(s => new { s.Id, s.CampaignId }).ToListAsync();
        foreach (var s in sheets.Where(s => gmByCampaign.ContainsKey(s.CampaignId)))
            plan.Add(new(gmByCampaign[s.CampaignId], $"/api/character-sheets/{s.Id}"));

        foreach (var n in await db.NpcSheets.AsNoTracking().Select(n => new { n.Id, n.GmId }).ToListAsync())
            plan.Add(new(n.GmId, $"/api/npc-sheets/{n.Id}"));
        foreach (var n in await db.CreatureSheets.AsNoTracking().Select(n => new { n.Id, n.GmId }).ToListAsync())
            plan.Add(new(n.GmId, $"/api/creature-sheets/{n.Id}"));

        foreach (var e in await db.Encounters.AsNoTracking().Select(e => new { e.Id, e.CampaignId }).ToListAsync())
            if (gmByCampaign.TryGetValue(e.CampaignId, out var gmId))
                plan.Add(new(gmId, $"/api/encounters/{e.Id}/log"));

        var gms = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.GM).Select(u => u.Id).ToListAsync();
        foreach (var g in gms)
            foreach (var path in new[] { "items", "spell-ability-bank", "traits", "rulebook", "npc-sheets", "creature-sheets", "auth/me" })
                plan.Add(new(g, $"/api/{path}"));

        return plan;
    }
}
