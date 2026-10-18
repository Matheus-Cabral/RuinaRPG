using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/equipagem")]
public sealed class NpcEquipagemController(RuinaRpgDbContext db, EquipmentKitGrantService grantService, ITipoDeFicha<NpcSheet> tipo)
    : EquipagemDaFichaController<NpcSheet, NpcWeapon, NpcArmorSlot, NpcShield, NpcInventoryItem, NpcArtifact>(db, grantService, tipo)
{
    protected override Task<Guid> MestreDaFichaAsync(NpcSheet ficha) => Task.FromResult(ficha.GmId);

    // A ficha de NPC não tem coluna CampaignId: um NPC só tem campanha onde está anexado e seu OwnerId atual
    // é membro — a mesma regra de NpcSheetsController.ToResponseAsync. Um NPC do próprio Mestre, ainda não
    // concedido, dá null.
    protected override async Task<Guid?> CampanhaDaFichaAsync(NpcSheet ficha) =>
        await Db.CampaignAttachments
            .Where(a => a.NpcSheetId == ficha.Id && Db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == ficha.OwnerId))
            .Select(a => (Guid?)a.CampaignId)
            .FirstOrDefaultAsync();
}
