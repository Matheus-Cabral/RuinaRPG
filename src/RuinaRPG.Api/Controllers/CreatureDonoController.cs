using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}")]
public sealed class CreatureDonoController(RuinaRpgDbContext db, ITipoDeFicha<CreatureSheet> tipo)
    : DonoDaFichaController<CreatureSheet>(db, tipo)
{
    protected override IQueryable<CampaignAttachment> Anexos(Guid sheetId) => Db.CampaignAttachments.Where(a => a.CreatureSheetId == sheetId);
}
