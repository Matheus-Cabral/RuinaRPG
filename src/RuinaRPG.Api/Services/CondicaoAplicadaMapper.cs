using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;

namespace RuinaRPG.Api.Services;

public static class CondicaoAplicadaMapper
{
    /// <summary>Na preparação o Turno atual é 0, mas a duração conta a partir do Turno 1.</summary>
    public static int TurnoDeContagem(int currentRound) => Math.Max(currentRound, 1);

    /// <summary>
    /// Ativa só com o encontro em andamento e o prazo não vencido. É a única definição de "ativa": a resposta do
    /// encontro e os números das fichas (CondicoesAtivasDaFicha) usam esta.
    /// </summary>
    public static bool Ativa(EncounterParticipantCondicao condicao, EncounterStatus status, int currentRound) =>
        status == EncounterStatus.EmAndamento
        && !DuracaoDeCondicao.Expirou(condicao.AplicadaNoTurno, condicao.DuracaoTurnos, TurnoDeContagem(currentRound));

    public static CondicaoAplicadaResponse ToResponse(EncounterParticipantCondicao condicao, Encounter encounter) =>
        new(condicao.Id.ToString(), condicao.Nome, condicao.Descricao, condicao.Icone,
            DuracaoDeCondicao.TurnosRestantes(condicao.AplicadaNoTurno, condicao.DuracaoTurnos, TurnoDeContagem(encounter.CurrentRound)),
            Ativa(condicao, encounter.Status, encounter.CurrentRound));
}
