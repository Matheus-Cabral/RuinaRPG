using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;

namespace RuinaRPG.Api.Services;

public enum MotivoDaVez { Inicio, Proxima, Anterior, Escolhida, Remocao }

/// <summary>O que uma mudança de vez fez; os ganchos das Condições leem daqui.</summary>
public sealed record MudancaDeVez(int RoundAnterior, int RoundNovo, Guid? ParticipanteAnteriorId, EncounterParticipant? Participante, MotivoDaVez Motivo)
{
    public bool EntrouEmNovoTurno => RoundNovo > RoundAnterior; // Inicio (0 → 1) included
    public bool ParticipanteRecebeuAVez => Participante is not null && Motivo != MotivoDaVez.Anterior;
}

public class EncounterTurnos(EncounterLog log)
{
    /// <summary>
    /// Único ponto em que a vez muda. Ajusta CurrentRound/CurrentParticipantId, devolve as Ações a 3
    /// quando o motivo dá a vez (todos menos Anterior), e registra NovoTurno/VezPassada/VezVoltou/
    /// VezEscolhida como hoje. Quem chama salva. Devolve o que aconteceu, para os ganchos das Condições.
    /// Com Inicio, o status já deve ser EmAndamento: registra Iniciado logo depois de a vez apontar para o primeiro.
    /// </summary>
    public async Task<MudancaDeVez> DarAVezAsync(Encounter encounter, EncounterParticipant? participante, int round, MotivoDaVez motivo)
    {
        var mudanca = new MudancaDeVez(encounter.CurrentRound, round, encounter.CurrentParticipantId, participante, motivo);

        encounter.CurrentRound = round;
        encounter.CurrentParticipantId = participante?.Id;
        if (mudanca.ParticipanteRecebeuAVez)
            participante!.AcoesRestantes = 3;

        if (motivo == MotivoDaVez.Inicio)
            await log.RegistrarAsync(encounter, EncounterLogTipo.Iniciado);
        else if (mudanca.EntrouEmNovoTurno)
            await log.RegistrarAsync(encounter, EncounterLogTipo.NovoTurno);

        if (participante is not null)
        {
            var tipo = motivo switch
            {
                MotivoDaVez.Anterior => EncounterLogTipo.VezVoltou,
                MotivoDaVez.Escolhida => EncounterLogTipo.VezEscolhida,
                _ => EncounterLogTipo.VezPassada,
            };
            await log.RegistrarAsync(encounter, tipo, participante.Nome);
        }

        return mudanca;
    }
}
