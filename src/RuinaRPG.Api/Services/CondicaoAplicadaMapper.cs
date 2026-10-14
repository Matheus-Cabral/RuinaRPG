using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;

namespace RuinaRPG.Api.Services;

public static class CondicaoAplicadaMapper
{
    /// <summary>
    /// Ativa só com o encontro em andamento e o prazo não vencido. Na preparação o Turno atual é 0, mas a duração
    /// conta a partir do Turno 1, então os Turnos restantes usam max(turno, 1).
    /// </summary>
    public static CondicaoAplicadaResponse ToResponse(EncounterParticipantCondicao condicao, Encounter encounter)
    {
        var turno = Math.Max(encounter.CurrentRound, 1);
        var ativa = encounter.Status == EncounterStatus.EmAndamento
            && !DuracaoDeCondicao.Expirou(condicao.AplicadaNoTurno, condicao.DuracaoTurnos, turno);
        return new CondicaoAplicadaResponse(condicao.Id.ToString(), condicao.Nome, condicao.Descricao, condicao.Icone,
            DuracaoDeCondicao.TurnosRestantes(condicao.AplicadaNoTurno, condicao.DuracaoTurnos, turno), ativa);
    }
}
