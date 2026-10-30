using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Condicoes;
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

    public Task<List<CondicaoAtivaNaFichaResponse>> ListarCondicoesAsync(CharacterSheet sheet) => condicoes.ListarDoPersonagemAsync(sheet.Id);

    public async Task<List<Guid>> EncontrosAsync(Guid sheetId) =>
        await db.EncounterParticipants.Where(p => p.SourceCharacterSheetId == sheetId).Select(p => p.EncounterId).Distinct().ToListAsync();

    public int PontosDeIgnicao(CharacterSheet sheet) => sheet.PontosDeIgnicaoAtual;

    public void DefinirPontosDeIgnicao(CharacterSheet sheet, int valor) => sheet.PontosDeIgnicaoAtual = valor;

    public bool PontosDeIgnicaoAcompanhamONivel => true;

    public Guid? HistoricoId(CharacterSheet sheet) => sheet.HistoricoId;

    public bool AplicaLimitesDeNivel => true;

    public bool SoPericiasDeCriatura => false;

    public bool RecusaAtributosAcimaDoOrcamento => true;

    public bool AfinidadesRecusamForaDasRegras(CharacterSheet sheet, Guid userId) => true;
}
