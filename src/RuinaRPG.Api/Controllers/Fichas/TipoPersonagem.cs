using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Ficha de Personagem: editável pelo dono ou pelo Mestre da campanha; acesso negado é 403.</summary>
public sealed class TipoPersonagem(RuinaRpgDbContext db, CondicoesAtivasDaFicha condicoes) : ITipoDeFicha<CharacterSheet>
{
    public async Task<CharacterSheet?> CarregarAsync(Guid sheetId) => await db.CharacterSheets.FindAsync(sheetId);

    public async Task<bool> PodeEditarAsync(CharacterSheet sheet, Guid userId) =>
        CharacterSheetAuthorization.CanEdit(userId, sheet.OwnerId, await MestreAsync(sheet));

    public async Task<Guid> MestreAsync(CharacterSheet sheet) =>
        await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();

    public Task<Guid?> CampanhaAsync(CharacterSheet sheet) => Task.FromResult<Guid?>(sheet.CampaignId);

    public ActionResult Negado(ControllerBase controller) => controller.Forbid();

    public Task<EfeitosDeCondicao> CondicoesAsync(CharacterSheet sheet) => condicoes.ParaPersonagemAsync(sheet.Id);

    public Guid? HistoricoId(CharacterSheet sheet) => sheet.HistoricoId;

    public bool AplicaLimitesDeNivel => true;

    public bool SoPericiasDeCriatura => false;

    public bool RecusaAtributosAcimaDoOrcamento => true;
}
