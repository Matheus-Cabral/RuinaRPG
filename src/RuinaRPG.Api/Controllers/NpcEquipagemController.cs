using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
