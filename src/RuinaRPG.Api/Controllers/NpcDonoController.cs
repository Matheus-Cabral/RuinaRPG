using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public sealed class NpcDonoController(RuinaRpgDbContext db, ITipoDeFicha<NpcSheet> tipo)
    : DonoDaFichaController<NpcSheet>(db, tipo)
{
    protected override IQueryable<CampaignAttachment> Anexos(Guid sheetId) => Db.CampaignAttachments.Where(a => a.NpcSheetId == sheetId);
}
