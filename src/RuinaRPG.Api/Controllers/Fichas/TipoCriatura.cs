using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Ficha de Criatura: editável pelo Mestre ou pelo jogador que a recebeu; acesso negado é 404.</summary>
public sealed class TipoCriatura(RuinaRpgDbContext db, CondicoesAtivasDaFicha condicoes) : ITipoDeFicha<CreatureSheet>
{
    public async Task<CreatureSheet?> CarregarAsync(Guid sheetId) => await db.CreatureSheets.FindAsync(sheetId);

    public Task<bool> PodeEditarAsync(CreatureSheet sheet, Guid userId) =>
        Task.FromResult(GrantedSheetAuthorization.CanEdit(userId, sheet.OwnerId, sheet.GmId));

    public ActionResult Negado(ControllerBase controller) => controller.NotFound();

    public Task<EfeitosDeCondicao> CondicoesAsync(CreatureSheet sheet) => condicoes.ParaCriaturaAsync(sheet);

    public Guid? HistoricoId(CreatureSheet sheet) => null;

    public bool AplicaLimitesDeNivel => false;

    public bool SoPericiasDeCriatura => true;

    public bool RecusaAtributosAcimaDoOrcamento => false;
}
