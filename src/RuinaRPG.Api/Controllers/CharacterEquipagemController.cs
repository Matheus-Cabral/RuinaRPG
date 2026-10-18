using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/equipagem")]
public sealed class CharacterEquipagemController(RuinaRpgDbContext db, EquipmentKitGrantService grantService, ITipoDeFicha<CharacterSheet> tipo)
    : EquipagemDaFichaController<CharacterSheet, CharacterWeapon, CharacterArmorSlot, CharacterShield, CharacterInventoryItem, CharacterArtifact>(db, grantService, tipo)
{
    protected override async Task<Guid> MestreDaFichaAsync(CharacterSheet ficha) =>
        await Db.Campaigns.Where(c => c.Id == ficha.CampaignId).Select(c => c.GmId).SingleAsync();

    protected override Task<Guid?> CampanhaDaFichaAsync(CharacterSheet ficha) => Task.FromResult<Guid?>(ficha.CampaignId);
}
