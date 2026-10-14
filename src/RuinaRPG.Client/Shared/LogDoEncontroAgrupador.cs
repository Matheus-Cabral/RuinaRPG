using RuinaRPG.Contracts.Encounters;

namespace RuinaRPG.Client.Shared;

/// <summary>Lógica pura do log do encontro: agrupa por Turno e por vez e monta o texto de cada linha.</summary>
public static class LogDoEncontroAgrupador
{
    /// <summary>Grupos por Turno (ordem crescente) e, dentro dele, por vez, na ordem em que aconteceram.</summary>
    public static List<TurnoDoLog> PorTurno(IEnumerable<EncounterLogEntryResponse> linhas)
    {
        var turnos = new List<TurnoDoLog>();
        foreach (var porTurno in linhas.GroupBy(l => l.Turno).OrderBy(g => g.Key))
        {
            var vezes = new List<VezDoLog>();
            foreach (var linha in porTurno)
            {
                if (vezes.Count == 0 || MudouAVez(vezes[^1].Linhas[^1], linha))
                    vezes.Add(new VezDoLog(linha.VezNome, new List<EncounterLogEntryResponse>()));
                vezes[^1].Linhas.Add(linha);
            }
            turnos.Add(new TurnoDoLog(porTurno.Key, vezes));
        }
        return turnos;
    }

    // Participants can share a name, so the id decides; the name is only the fallback when neither row has one.
    private static bool MudouAVez(EncounterLogEntryResponse anterior, EncounterLogEntryResponse atual) =>
        anterior.VezParticipantId is null && atual.VezParticipantId is null
            ? anterior.VezNome != atual.VezNome
            : anterior.VezParticipantId != atual.VezParticipantId;

    public static string Texto(EncounterLogEntryResponse linha) => linha.Tipo switch
    {
        "Iniciado" => "Encontro iniciado",
        "Finalizado" => "Encontro finalizado",
        "Reaberto" => "Encontro reaberto",
        "NovoTurno" => $"Início do Turno {linha.Turno}",
        "VezPassada" => $"Vez de {linha.ParticipanteNome}",
        "VezVoltou" => $"A vez voltou para {linha.ParticipanteNome}",
        "VezEscolhida" => $"A vez foi passada para {linha.ParticipanteNome}",
        "ParticipanteAdicionado" => $"{linha.ParticipanteNome} entrou no encontro",
        "ParticipanteRemovido" => $"{linha.ParticipanteNome} saiu do encontro",
        "CampoAlterado" => $"{linha.ParticipanteNome} — {linha.Campo}: {linha.ValorAntes} → {linha.ValorDepois}",
        "CondicaoAdicionada" => $"{linha.ParticipanteNome} recebeu a condição {linha.Texto}",
        "CondicaoRemovida" => $"{linha.ParticipanteNome} perdeu a condição {linha.Texto}",
        "RecursoDaFicha" => $"{linha.ParticipanteNome} — {linha.Campo}: {linha.ValorAntes} → {linha.ValorDepois} (na ficha)",
        _ => linha.Tipo,
    };
}

public sealed record TurnoDoLog(int Turno, List<VezDoLog> Vezes);

public sealed record VezDoLog(string? VezNome, List<EncounterLogEntryResponse> Linhas);
