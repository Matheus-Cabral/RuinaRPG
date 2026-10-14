using FluentAssertions;
using RuinaRPG.Api.Services;
using RuinaRPG.Infrastructure.Encounters;

namespace RuinaRPG.Tests.Integration.Controllers;

public class MudancaDeVezTests
{
    private static MudancaDeVez Mudanca(int roundAnterior, int roundNovo, MotivoDaVez motivo, bool comParticipante = true) =>
        new(roundAnterior, roundNovo, null, comParticipante ? new EncounterParticipant { Id = Guid.NewGuid(), Nome = "P" } : null, motivo);

    [Fact]
    public void Inicio_reports_entering_Turno_1_and_giving_the_vez()
    {
        var m = Mudanca(0, 1, MotivoDaVez.Inicio);
        m.EntrouEmNovoTurno.Should().BeTrue();
        m.ParticipanteRecebeuAVez.Should().BeTrue();
    }

    [Theory]
    [InlineData(MotivoDaVez.Proxima, true)]
    [InlineData(MotivoDaVez.Escolhida, true)]
    [InlineData(MotivoDaVez.Remocao, true)]
    [InlineData(MotivoDaVez.Anterior, false)]
    public void Only_Anterior_does_not_give_the_vez(MotivoDaVez motivo, bool recebe) =>
        Mudanca(2, 2, motivo).ParticipanteRecebeuAVez.Should().Be(recebe);

    [Fact]
    public void Same_round_is_not_a_new_Turno_and_a_higher_round_is()
    {
        Mudanca(2, 2, MotivoDaVez.Proxima).EntrouEmNovoTurno.Should().BeFalse();
        Mudanca(2, 3, MotivoDaVez.Proxima).EntrouEmNovoTurno.Should().BeTrue();
        Mudanca(3, 2, MotivoDaVez.Anterior).EntrouEmNovoTurno.Should().BeFalse();
    }

    [Fact]
    public void Nobody_on_turn_never_receives_the_vez() =>
        Mudanca(1, 1, MotivoDaVez.Remocao, comParticipante: false).ParticipanteRecebeuAVez.Should().BeFalse();
}
