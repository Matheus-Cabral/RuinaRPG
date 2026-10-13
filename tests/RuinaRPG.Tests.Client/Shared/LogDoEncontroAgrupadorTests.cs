using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Encounters;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class LogDoEncontroAgrupadorTests
{
    private static long _seq;

    private static EncounterLogEntryResponse Linha(int turno, string? vez, string tipo = "VezPassada", string? participante = "Goblin",
        string? campo = null, string? antes = null, string? depois = null, string? texto = null) =>
        new(++_seq, DateTime.UtcNow, turno, vez, tipo, participante, campo, antes, depois, texto);

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
        new object[] { Linha(1, "A", "Outra"), "Outra" },
    };

    [Theory]
    [MemberData(nameof(Textos))]
    public void Texto_follows_the_table(EncounterLogEntryResponse linha, string esperado) =>
        LogDoEncontroAgrupador.Texto(linha).Should().Be(esperado);
}
