using FluentAssertions;
using RuinaRPG.Domain.Encounters;

namespace RuinaRPG.Tests.Unit.Encounters;

public class TurnAdvanceCalculatorTests
{
    [Fact]
    public void Advance_moves_to_the_next_participant_within_the_same_round()
    {
        var (round, index) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantIndex: 0, participantCount: 3);

        round.Should().Be(1);
        index.Should().Be(1);
    }

    [Fact]
    public void Advance_past_the_last_participant_starts_a_new_round_at_the_first_participant()
    {
        // "Ao passar do último participante da lista, o Encontro inicia uma nova Rodada... e volta ao
        // primeiro participante da ordem." — R0006.
        var (round, index) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantIndex: 2, participantCount: 3);

        round.Should().Be(2);
        index.Should().Be(0);
    }

    [Fact]
    public void Advance_with_a_single_participant_always_starts_a_new_round()
    {
        var (round, index) = TurnAdvanceCalculator.Advance(currentRound: 5, currentParticipantIndex: 0, participantCount: 1);

        round.Should().Be(6);
        index.Should().Be(0);
    }
}
