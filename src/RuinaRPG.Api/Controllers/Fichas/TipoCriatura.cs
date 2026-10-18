using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Condicoes;
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

    public Task<Guid> MestreAsync(CreatureSheet sheet) => Task.FromResult(sheet.GmId);

    public async Task<Guid?> CampanhaAsync(CreatureSheet sheet) =>
        await db.CampaignAttachments
            .Where(a => a.CreatureSheetId == sheet.Id && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == sheet.OwnerId))
            .Select(a => (Guid?)a.CampaignId)
            .FirstOrDefaultAsync();

    public ActionResult Negado(ControllerBase controller) => controller.NotFound();

    public Task<EfeitosDeCondicao> CondicoesAsync(CreatureSheet sheet) => condicoes.ParaCriaturaAsync(sheet);

    public Task<List<CondicaoAtivaNaFichaResponse>> ListarCondicoesAsync(CreatureSheet sheet) => condicoes.ListarDaCriaturaAsync(sheet);

    public async Task<List<Guid>> EncontrosAsync(Guid sheetId) =>
        await db.EncounterParticipants.Where(p => p.SourceCreatureSheetId == sheetId).Select(p => p.EncounterId).Distinct().ToListAsync();

    public int PontosDeIgnicao(CreatureSheet sheet) => sheet.PontosDeIgnicao;

    public void DefinirPontosDeIgnicao(CreatureSheet sheet, int valor) => sheet.PontosDeIgnicao = valor;

    public bool PontosDeIgnicaoAcompanhamONivel => false;

    public Guid? HistoricoId(CreatureSheet sheet) => null;

    public bool AplicaLimitesDeNivel => false;

    public bool SoPericiasDeCriatura => true;

    public bool RecusaAtributosAcimaDoOrcamento => false;
}
