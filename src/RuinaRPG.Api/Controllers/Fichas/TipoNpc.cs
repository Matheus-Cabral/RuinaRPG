using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Ficha de NPC: editável pelo Mestre ou pelo jogador que a recebeu; acesso negado é 404.</summary>
public class TipoNpc(RuinaRpgDbContext db, CondicoesAtivasDaFicha condicoes) : ITipoDeFicha<NpcSheet>
{
    public async Task<NpcSheet?> CarregarAsync(Guid sheetId) => await db.NpcSheets.FindAsync(sheetId);

    public Task<bool> PodeEditarAsync(NpcSheet sheet, Guid userId) =>
        Task.FromResult(GrantedSheetAuthorization.CanEdit(userId, sheet.OwnerId, sheet.GmId));

    public ActionResult Negado(ControllerBase controller) => controller.NotFound();

    public Task<EfeitosDeCondicao> CondicoesAsync(NpcSheet sheet) => condicoes.ParaNpcAsync(sheet);

    public Guid? HistoricoId(NpcSheet sheet) => sheet.HistoricoId;

    public bool AplicaLimitesDeNivel => true;

    public bool SoPericiasDeCriatura => false;

    public bool RecusaAtributosAcimaDoOrcamento => false;
}
