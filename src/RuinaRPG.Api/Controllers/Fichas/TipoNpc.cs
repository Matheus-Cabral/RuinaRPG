using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Ficha de NPC: editável pelo Mestre ou pelo jogador que a recebeu; acesso negado é 404.</summary>
public sealed class TipoNpc(RuinaRpgDbContext db, CondicoesAtivasDaFicha condicoes) : ITipoDeFicha<NpcSheet>
{
    public async Task<NpcSheet?> CarregarAsync(Guid sheetId) => await db.NpcSheets.FindAsync(sheetId);

    public Task<bool> PodeEditarAsync(NpcSheet sheet, Guid userId) =>
        Task.FromResult(GrantedSheetAuthorization.CanEdit(userId, sheet.OwnerId, sheet.GmId));

    public Task<Guid> MestreAsync(NpcSheet sheet) => Task.FromResult(sheet.GmId);

    public async Task<Guid?> CampanhaAsync(NpcSheet sheet) =>
        await db.CampaignAttachments
            .Where(a => a.NpcSheetId == sheet.Id && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == sheet.OwnerId))
            .Select(a => (Guid?)a.CampaignId)
            .FirstOrDefaultAsync();

    public ActionResult Negado(ControllerBase controller) => controller.NotFound();

    public Task<EfeitosDeCondicao> CondicoesAsync(NpcSheet sheet) => condicoes.ParaNpcAsync(sheet);

    public Guid? HistoricoId(NpcSheet sheet) => sheet.HistoricoId;

    public bool AplicaLimitesDeNivel => true;

    public bool SoPericiasDeCriatura => false;

    public bool RecusaAtributosAcimaDoOrcamento => false;
}
