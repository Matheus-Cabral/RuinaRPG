using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Encounters;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class LogDoEncontroAgrupadorTests
{
    private static long _seq;

    private static EncounterLogEntryResponse Linha(int turno, string? vez, string tipo = "VezPassada", string? participante = "Goblin",
        string? campo = null, string? antes = null, string? depois = null, string? texto = null, string? vezId = null) =>
        new(++_seq, DateTime.UtcNow, turno, vez, tipo, participante, campo, antes, depois, texto, vezId);

    [Fact]
    public void Groups_by_Turno_ascending_and_then_by_vez_in_order_of_occurrence()
    {
        var linhas = new[]
        {
            Linha(2, "A"), Linha(1, "A"), Linha(1, "A"), Linha(1, "B"), Linha(1, "A"),
        };

        var grupos = LogDoEncontroAgrupador.PorTurno(linhas);

        grupos.Select(g => g.Turno).Should().Equal(1, 2);
        grupos[0].Vezes.Select(v => v.VezNome).Should().Equal("A", "B", "A");
        grupos[0].Vezes.Select(v => v.Linhas.Count).Should().Equal(2, 1, 1);
        grupos[1].Vezes.Should().ContainSingle();
    }

    [Fact]
    public void Consecutive_vezes_of_different_participants_with_the_same_name_are_separate_groups()
    {
        var grupos = LogDoEncontroAgrupador.PorTurno(new[]
        {
            Linha(1, "Goblin", vezId: "1"), Linha(1, "Goblin", vezId: "1"), Linha(1, "Goblin", vezId: "2"),
        });

        grupos[0].Vezes.Select(v => v.Linhas.Count).Should().Equal(2, 1);
    }

    [Fact]
    public void Lines_without_a_vez_form_their_own_group()
    {
        var grupos = LogDoEncontroAgrupador.PorTurno(new[] { Linha(0, null, "Iniciado"), Linha(1, "A"), Linha(1, null, "Finalizado") });

        grupos[1].Vezes.Select(v => v.VezNome).Should().Equal("A", null);
    }

    public static IEnumerable<object[]> Textos() => new[]
    {
        new object[] { Linha(1, null, "Iniciado", null), "Encontro iniciado" },
        new object[] { Linha(1, null, "Finalizado", null), "Encontro finalizado" },
        new object[] { Linha(1, null, "Reaberto", null), "Encontro reaberto" },
        new object[] { Linha(3, null, "NovoTurno", null), "Início do Turno 3" },
        new object[] { Linha(1, "Goblin", "VezPassada"), "Vez de Goblin" },
        new object[] { Linha(1, "Goblin", "VezVoltou"), "A vez voltou para Goblin" },
        new object[] { Linha(1, "Goblin", "VezEscolhida"), "A vez foi passada para Goblin" },
        new object[] { Linha(1, "A", "ParticipanteAdicionado"), "Goblin entrou no encontro" },
        new object[] { Linha(1, "A", "ParticipanteRemovido"), "Goblin saiu do encontro" },
        new object[] { Linha(1, "A", "CampoAlterado", campo: "PV", antes: "10", depois: "7"), "Goblin — PV: 10 → 7" },
        new object[] { Linha(1, "A", "CondicaoAdicionada", texto: "Atordoado"), "Goblin recebeu a condição Atordoado" },
        new object[] { Linha(1, "A", "CondicaoRemovida", texto: "Atordoado"), "Goblin perdeu a condição Atordoado" },
        new object[] { Linha(1, "A", "RecursoDaFicha", campo: "Vitalidade", antes: "20", depois: "15"), "Goblin — Vitalidade: 20 → 15 (na ficha)" },
        new object[] { Linha(1, "A", "CondicaoExpirada", texto: "Veneno"), "Goblin: a condição Veneno expirou" },
        new object[] { Linha(1, "A", "AjusteDeCondicao", campo: "Vitalidade", antes: "20", depois: "17", texto: "Veneno"), "Goblin — Vitalidade: 20 → 17 (Veneno)" },
        new object[] { Linha(1, "A", "AjusteDeCondicao", texto: "Veneno: Vitalidade já está no mínimo"), "Goblin — Veneno: Vitalidade já está no mínimo" },
        new object[] { Linha(1, "A", "Outra"), "Outra" },
    };

    [Theory]
    [MemberData(nameof(Textos))]
    public void Texto_follows_the_table(EncounterLogEntryResponse linha, string esperado) =>
        LogDoEncontroAgrupador.Texto(linha).Should().Be(esperado);

    private static EncounterLogEntryResponse EmUtc(DateTime utc, int turno = 1, string tipo = "VezPassada", string? campo = null) =>
        new(++_seq, DateTime.SpecifyKind(utc, DateTimeKind.Utc), turno, "A", tipo, "Goblin", campo, "1", "2", null, "p1");

    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    [Fact]
    public void PorDia_groups_by_local_day_newest_day_first_and_keeps_Turno_vez_grouping_chronological_inside_a_day()
    {
        var linhas = new[]
        {
            EmUtc(new DateTime(2026, 10, 12, 15, 0, 0), turno: 1),
            EmUtc(new DateTime(2026, 10, 13, 2, 30, 0), turno: 1),  // 23:30 do dia 12 em Sao Paulo
            EmUtc(new DateTime(2026, 10, 13, 15, 0, 0), turno: 2),
            EmUtc(new DateTime(2026, 10, 13, 16, 0, 0), turno: 3),
        };

        var dias = LogDoEncontroAgrupador.PorDia(linhas, Fuso);

        dias.Select(d => d.Dia).Should().Equal(new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 12));
        dias[0].Turnos.Select(t => t.Turno).Should().Equal(2, 3);
        dias[1].Turnos.Should().ContainSingle().Which.Vezes.Single().Linhas.Should().HaveCount(2);
    }

    [Fact]
    public void Filtrar_by_period_uses_local_day_boundaries_inclusive_of_the_last_day()
    {
        var antes = EmUtc(new DateTime(2026, 10, 10, 2, 59, 0));   // 23:59 do dia 9
        var inicio = EmUtc(new DateTime(2026, 10, 10, 3, 0, 0));   // 00:00 do dia 10
        var fim = EmUtc(new DateTime(2026, 10, 12, 2, 59, 0));     // 23:59 do dia 11, o último do período
        var depois = EmUtc(new DateTime(2026, 10, 12, 3, 0, 0));   // 00:00 do dia 12

        var r = LogDoEncontroAgrupador.Filtrar(new[] { antes, inicio, fim, depois }, new DateTime(2026, 10, 10), new DateTime(2026, 10, 11), true, true, Fuso);

        r.Should().Equal(inicio, fim);
    }

    [Theory]
    [InlineData("RecursoDaFicha", "Vitalidade")]
    [InlineData("RecursoDaFicha", "Foco (PF)")]
    [InlineData("RecursoDaFicha", "Adrenalina (PA)")]
    [InlineData("CampoAlterado", "PV")]
    [InlineData("CampoAlterado", "PF")]
    [InlineData("CampoAlterado", "PA")]
    [InlineData("AjusteDeCondicao", "PV")]
    [InlineData("AjusteDeCondicao", "Vitalidade")]
    [InlineData("AjusteDeCondicao", "Foco")]
    [InlineData("AjusteDeCondicao", "Adrenalina")]
    [InlineData("RecursoDaFicha", "Arcana (PF)")]
    [InlineData("AjusteDeCondicao", "Arcana (PF)")]
    public void The_resources_checkbox_hides_only_Vitalidade_Foco_Adrenalina_lines(string tipo, string campo)
    {
        var recurso = EmUtc(DateTime.UtcNow, tipo: tipo, campo: campo);
        var evento = EmUtc(DateTime.UtcNow, tipo: "CondicaoAdicionada");
        var iniciativa = EmUtc(DateTime.UtcNow, tipo: "CampoAlterado", campo: "Iniciativa");

        var r = LogDoEncontroAgrupador.Filtrar(new[] { recurso, evento, iniciativa }, null, null, false, true, Fuso);

        r.Should().Equal(evento, iniciativa);
    }

    [Theory]
    [InlineData("RecursoDaFicha")]
    [InlineData("CampoAlterado")]
    [InlineData("AjusteDeCondicao")]
    public void The_estresse_checkbox_hides_only_Estresse_lines(string tipo)
    {
        var estresse = EmUtc(DateTime.UtcNow, tipo: tipo, campo: "Estresse");
        var vida = EmUtc(DateTime.UtcNow, tipo: tipo, campo: "PV");
        var evento = EmUtc(DateTime.UtcNow, tipo: "VezPassada");

        var r = LogDoEncontroAgrupador.Filtrar(new[] { estresse, vida, evento }, null, null, true, false, Fuso);

        r.Should().Equal(vida, evento);
    }
}
