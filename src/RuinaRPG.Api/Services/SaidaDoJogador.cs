using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Services;

/// <summary>
/// A saída de um jogador de campanhas (Campanha R0018; Convite de Jogador R0008 e R0009 aplicam-na a todas as campanhas
/// do GM). Único lugar com estes passos; remover da campanha, desativar e excluir a conta só o chamam. Quem chama salva,
/// numa transação, e depois chama <see cref="NotificarAsync"/>.
/// </summary>
public class SaidaDoJogador(RuinaRpgDbContext db, RemocaoDeParticipante remocao, CondicoesDoEncontro condicoesDoEncontro,
    IHubContext<NotificationHub> notificacoes, IHubContext<EncounterHub> encontros, ILogger<SaidaDoJogador> logger)
{
    private readonly HashSet<Guid> _encontrosAAvisar = [];
    private readonly HashSet<Guid> _jogadoresAAvisar = [];

    /// <summary>
    /// Para cada campanha de <paramref name="campanhaIds"/>: os participantes vindos de uma Ficha de Personagem dele ou de
    /// uma ficha concedida a ele e anexada à campanha saem dos encontros em preparação ou em andamento (Gerenciador de
    /// Encontros R0008; finalizados são histórico); as concessões voltam ao GM, menos as anexadas também a uma campanha
    /// em que ele continua membro; o vínculo de membro é apagado (as Fichas de Personagem entram no cemitério por cálculo).
    /// </summary>
    public async Task SairAsync(Guid jogadorId, IReadOnlyCollection<Guid> campanhaIds)
    {
        var ficaEm = await db.CampaignMembers
            .Where(m => m.UserId == jogadorId && !campanhaIds.Contains(m.CampaignId))
            .Select(m => m.CampaignId).ToListAsync();
        var anexos = await db.CampaignAttachments
            .Where(a => (a.NpcSheetId != null || a.CreatureSheetId != null) && (campanhaIds.Contains(a.CampaignId) || ficaEm.Contains(a.CampaignId)))
            .Select(a => new { a.CampaignId, FichaId = (a.NpcSheetId ?? a.CreatureSheetId)!.Value })
            .ToListAsync();
        var anexadas = anexos.Select(a => a.FichaId).Distinct().ToList();
        IFichaConcedivel[] concessoes =
        [
            .. await db.NpcSheets.Where(s => s.OwnerId == jogadorId && anexadas.Contains(s.Id)).ToListAsync(),
            .. await db.CreatureSheets.Where(s => s.OwnerId == jogadorId && anexadas.Contains(s.Id)).ToListAsync(),
        ];
        var concedidas = concessoes.Select(c => c.Id).ToHashSet();
        var concedidasNaSaida = anexos.Where(a => campanhaIds.Contains(a.CampaignId) && concedidas.Contains(a.FichaId))
            .Select(a => (a.CampaignId, a.FichaId)).ToHashSet();

        await SairDosEncontrosAsync(jogadorId, campanhaIds, concedidasNaSaida);

        var ficam = anexos.Where(a => ficaEm.Contains(a.CampaignId)).Select(a => a.FichaId).ToHashSet();
        var saindo = concedidasNaSaida.Select(a => a.FichaId).ToHashSet();
        foreach (var concessao in concessoes.Where(c => saindo.Contains(c.Id) && !ficam.Contains(c.Id)))
            concessao.OwnerId = null;

        db.CampaignMembers.RemoveRange(await db.CampaignMembers.Where(m => m.UserId == jogadorId && campanhaIds.Contains(m.CampaignId)).ToListAsync());
        _jogadoresAAvisar.Add(jogadorId);
    }

    private async Task SairDosEncontrosAsync(Guid jogadorId, IReadOnlyCollection<Guid> campanhaIds, HashSet<(Guid CampaignId, Guid FichaId)> concedidasNaSaida)
    {
        var personagens = await db.CharacterSheets
            .Where(s => s.OwnerId == jogadorId && campanhaIds.Contains(s.CampaignId))
            .Select(s => s.Id).ToListAsync();
        var concedidas = concedidasNaSaida.Select(a => a.FichaId).Distinct().ToList();
        var ativos = await db.Encounters
            .Where(e => campanhaIds.Contains(e.CampaignId) && e.Status != EncounterStatus.Finalizado)
            .ToListAsync();
        foreach (var encontro in ativos)
        {
            var dele = (await db.EncounterParticipants
                    .Where(p => p.EncounterId == encontro.Id && (
                        (p.SourceCharacterSheetId != null && personagens.Contains(p.SourceCharacterSheetId.Value)) ||
                        (p.SourceNpcSheetId != null && concedidas.Contains(p.SourceNpcSheetId.Value)) ||
                        (p.SourceCreatureSheetId != null && concedidas.Contains(p.SourceCreatureSheetId.Value))))
                    .ToListAsync())
                .Where(p => p.SourceCharacterSheetId is not null || concedidasNaSaida.Contains((encontro.CampaignId, (p.SourceNpcSheetId ?? p.SourceCreatureSheetId)!.Value)))
                // Quem tem a vez sai por último: ela passa direto a quem fica.
                .OrderBy(p => p.Id == encontro.CurrentParticipantId)
                .ToList();
            foreach (var participante in dele)
                await remocao.RemoverAsync(encontro, participante.Id);
            if (dele.Count > 0)
                _encontrosAAvisar.Add(encontro.Id);
        }
    }

    /// <summary>
    /// Depois do salvamento que deu certo: os avisos das Condições, ParticipantsChanged aos encontros que perderam
    /// participantes e CampanhasDoJogadorAlteradas ao jogador. Uma falha dos hubs vai para o log e não derruba a ação.
    /// </summary>
    public async Task NotificarAsync()
    {
        await condicoesDoEncontro.NotificarAsync();
        foreach (var encounterId in _encontrosAAvisar)
        {
            try
            {
                await encontros.Clients.Group($"encounter-{encounterId}").SendAsync("ParticipantsChanged");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify encounter {EncounterId} of a ParticipantsChanged update.", encounterId);
            }
        }
        foreach (var jogadorId in _jogadoresAAvisar)
        {
            try
            {
                await notificacoes.Clients.User(jogadorId.ToString()).SendAsync(NotificationEvents.CampanhasDoJogadorAlteradas);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to push CampanhasDoJogadorAlteradas to player {PlayerId}", jogadorId);
            }
        }
    }
}
