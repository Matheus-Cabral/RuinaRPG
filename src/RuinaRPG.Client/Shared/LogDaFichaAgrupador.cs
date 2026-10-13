using System.Globalization;
using RuinaRPG.Contracts.SheetLogs;

namespace RuinaRPG.Client.Shared;

/// <summary>
/// Lógica pura do popup do log da ficha (Campanha R0016): agrupa as linhas por dia no fuso dado
/// e monta o texto de cada linha. O fuso entra como parâmetro para o agrupamento ser testável.
/// </summary>
public static class LogDaFichaAgrupador
{
    public static List<(DateOnly Dia, List<SheetLogEntryResponse> Linhas)> PorDia(IEnumerable<SheetLogEntryResponse> itens, TimeZoneInfo fuso) =>
        itens
            .Select(i => (Local: TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(i.OccurredAt, DateTimeKind.Utc), fuso), Item: i))
            .GroupBy(x => DateOnly.FromDateTime(x.Local))
            .OrderByDescending(g => g.Key)
            .Select(g => (g.Key, g.OrderByDescending(x => x.Local).Select(x => x.Item).ToList()))
            .ToList();

    private static readonly string[] DiasDaSemana =
        ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];

    /// <summary>Cabeçalho do dia ("segunda-feira, 13/10/2026"), sem depender dos dados de cultura do navegador (pt-BR pode não estar carregado no WebAssembly).</summary>
    public static string TituloDoDia(DateOnly dia) =>
        $"{DiasDaSemana[(int)dia.DayOfWeek]}, {dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    public static string Texto(SheetLogEntryResponse linha) => linha.Acao switch
    {
        "Adicionou" => $"adicionou {linha.ItemNome}",
        "Removeu" => $"removeu {linha.ItemNome}",
        "Criou" => "ficha criada",
        _ => TextoDeAlteracao(linha),
    };

    private static string TextoDeAlteracao(SheetLogEntryResponse linha)
    {
        var prefixo = linha.ItemNome is null ? "" : $"{linha.ItemNome} — ";
        var mudanca = linha.ValorAntes is null && linha.ValorDepois is null
            ? "alterado"
            : $"{linha.ValorAntes} → {linha.ValorDepois}";
        return $"{prefixo}{linha.Campo}: {mudanca}";
    }
}
