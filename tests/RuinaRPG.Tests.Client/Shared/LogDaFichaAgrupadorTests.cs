using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.SheetLogs;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class LogDaFichaAgrupadorTests
{
    private static SheetLogEntryResponse Linha(string id, DateTime utc, string acao = "Alterou", string? campo = "Vitalidade Atual",
        string? itemNome = null, string? antes = "30", string? depois = "25") =>
        new(id, DateTime.SpecifyKind(utc, DateTimeKind.Utc), "Ana", false, "Recursos", acao, "Informações Básicas", campo, itemNome, antes, depois);

    [Fact]
    public void PorDia_splits_days_by_the_given_zone_not_by_the_utc_date()
    {
        var utcMenos3 = TimeZoneInfo.CreateCustomTimeZone("UTC-3", TimeSpan.FromHours(-3), "UTC-3", "UTC-3");
        var itens = new[]
        {
            Linha("a", new DateTime(2026, 10, 10, 2, 30, 0)),   // 23:30 do dia 9
            Linha("b", new DateTime(2026, 10, 10, 3, 10, 0)),   // 00:10 do dia 10
            Linha("c", new DateTime(2026, 10, 10, 9, 0, 0)),    // 06:00 do dia 10
        };

        var grupos = LogDaFichaAgrupador.PorDia(itens, utcMenos3);

        grupos.Select(g => g.Dia).Should().Equal(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 9));
        grupos[0].Linhas.Select(l => l.Id).Should().Equal("c", "b");
        grupos[1].Linhas.Select(l => l.Id).Should().Equal("a");
    }

    [Fact]
    public void Texto_alterou_with_values()
    {
        LogDaFichaAgrupador.Texto(Linha("a", DateTime.UtcNow)).Should().Be("Vitalidade Atual: 30 → 25");
    }

    [Fact]
    public void Texto_alterou_with_values_and_item_name()
    {
        LogDaFichaAgrupador.Texto(Linha("a", DateTime.UtcNow, campo: "Gasto", itemNome: "Força", antes: "2", depois: "3"))
            .Should().Be("Força — Gasto: 2 → 3");
    }

    [Fact]
    public void Texto_alterou_without_values_says_alterado()
    {
        LogDaFichaAgrupador.Texto(Linha("a", DateTime.UtcNow, campo: "História", antes: null, depois: null)).Should().Be("História: alterado");
        LogDaFichaAgrupador.Texto(Linha("a", DateTime.UtcNow, campo: "Efeitos", itemNome: "Bola de Fogo", antes: null, depois: null))
            .Should().Be("Bola de Fogo — Efeitos: alterado");
    }

    [Fact]
    public void Texto_adicionou_removeu_criou()
    {
        LogDaFichaAgrupador.Texto(Linha("a", DateTime.UtcNow, "Adicionou", null, "Rival", null, null)).Should().Be("adicionou Rival");
        LogDaFichaAgrupador.Texto(Linha("a", DateTime.UtcNow, "Removeu", null, "Rival", null, null)).Should().Be("removeu Rival");
        LogDaFichaAgrupador.Texto(Linha("a", DateTime.UtcNow, "Criou", null, null, null, null)).Should().Be("ficha criada");
    }
}
