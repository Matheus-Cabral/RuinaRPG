using RuinaRPG.Contracts.Encounters;

namespace RuinaRPG.Client.Shared;

/// <summary>Lógica pura do popup do log do encontro: filtra por período e recurso, agrupa por dia, Turno e vez e monta o texto de cada linha.</summary>
public static class LogDoEncontroAgrupador
{
    // Os Campo exatos que o servidor grava: o rótulo da ficha (RecursoDaFicha e Atual guardado; a Criatura chama o Foco de "Arcana (PF)") e o PV/PF/PA da instância no encontro.
    private static readonly HashSet<string> CamposDeVfa =
        ["Vitalidade", "Foco (PF)", "Arcana (PF)", "Adrenalina (PA)", "Foco", "Adrenalina", "PV", "PF", "PA"];

    private static bool EhLinhaDeRecurso(EncounterLogEntryResponse l) => l.Tipo is "RecursoDaFicha" or "CampoAlterado" or "AjusteDeCondicao";

    /// <summary>
    /// Aplica o período (dias locais, o último inclusive) e as duas caixas: <paramref name="recursos"/> falso esconde as linhas de
    /// Vitalidade, Foco e Adrenalina; <paramref name="estresse"/> falso esconde as de Estresse. Eventos do encontro nunca somem.
    /// </summary>
    public static List<EncounterLogEntryResponse> Filtrar(IEnumerable<EncounterLogEntryResponse> linhas, DateTime? de, DateTime? ate,
        bool recursos, bool estresse, TimeZoneInfo fuso) =>
        linhas.Where(l =>
        {
            var dia = DiaLocal(l, fuso);
            if (de is { } d && dia < DateOnly.FromDateTime(d)) return false;
            if (ate is { } a && dia > DateOnly.FromDateTime(a)) return false;
            if (!EhLinhaDeRecurso(l)) return true;
            if (!recursos && l.Campo is not null && CamposDeVfa.Contains(l.Campo)) return false;
            if (!estresse && l.Campo == "Estresse") return false;
            return true;
        }).ToList();

    /// <summary>Dias do mais novo para o mais antigo; dentro de cada um, Turno e vez na ordem em que aconteceram (ler Turno/vez de trás para frente confunde).</summary>
    public static List<(DateOnly Dia, List<TurnoDoLog> Turnos)> PorDia(IEnumerable<EncounterLogEntryResponse> linhas, TimeZoneInfo fuso) =>
        linhas
            .GroupBy(l => DiaLocal(l, fuso))
            .OrderByDescending(g => g.Key)
            .Select(g => (g.Key, PorTurno(g)))
            .ToList();

    private static DateOnly DiaLocal(EncounterLogEntryResponse l, TimeZoneInfo fuso) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(l.OccurredAt, DateTimeKind.Utc), fuso));

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
        "CondicaoExpirada" => $"{linha.ParticipanteNome}: a condição {linha.Texto} expirou",
        // Um ajuste pulado não tem valores: o Texto já traz "{condição}: {motivo}".
        "AjusteDeCondicao" when linha.ValorAntes is null && linha.ValorDepois is null => $"{linha.ParticipanteNome} — {linha.Texto}",
        "AjusteDeCondicao" => $"{linha.ParticipanteNome} — {linha.Campo}: {linha.ValorAntes} → {linha.ValorDepois} ({linha.Texto})",
        _ => linha.Tipo,
    };
}

public sealed record TurnoDoLog(int Turno, List<VezDoLog> Vezes);

public sealed record VezDoLog(string? VezNome, List<EncounterLogEntryResponse> Linhas);
