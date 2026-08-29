using FluentAssertions;
using RuinaRPG.Domain.Encounters;

namespace RuinaRPG.Tests.Unit.Encounters;

public class TurnAdvanceCalculatorTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid D = Guid.Parse("00000000-0000-0000-0000-000000000004");

    [Fact]
    public void Advance_moves_to_the_next_participant_within_the_same_round()
    {
        var (round, nextId) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantId: A, [A, B, C]);

        round.Should().Be(1);
        nextId.Should().Be(B);
    }

    [Fact]
    public void Advance_past_the_last_participant_starts_a_new_round_at_the_first_participant()
    {
        // "Ao passar do último participante da lista, o Encontro inicia uma nova Rodada... e volta ao
        // primeiro participante da ordem." — R0006.
        var (round, nextId) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantId: C, [A, B, C]);

        round.Should().Be(2);
        nextId.Should().Be(A);
    }

    [Fact]
    public void Advance_with_a_single_participant_always_starts_a_new_round()
    {
        var (round, nextId) = TurnAdvanceCalculator.Advance(currentRound: 5, currentParticipantId: A, [A]);

        round.Should().Be(6);
        nextId.Should().Be(A);
    }

    [Fact]
    public void Advance_with_no_current_participant_moves_past_the_implicit_first_turn_to_the_second()
    {
        // A fresh encounter implicitly starts on the first participant's turn (matching the old
        // CurrentParticipantIndex column's 0 default) — the first-ever AdvanceTurn call moves
        // past them, exactly like calling Advance with an explicit currentParticipantId of A.
        var (round, nextId) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantId: null, [A, B, C]);

        round.Should().Be(1);
        nextId.Should().Be(B);
    }

    [Fact]
    public void Advance_with_no_current_participant_and_only_one_participant_starts_a_new_round()
    {
        var (round, nextId) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantId: null, [A]);

        round.Should().Be(2);
        nextId.Should().Be(A);
    }

    [Fact]
    public void Advance_is_unaffected_by_a_participant_being_inserted_ahead_of_the_current_one_mid_round()
    {
        // Épico 5 item 2 of the gap audit: adding D with a higher Iniciativa than B (the current
        // participant) reorders the list from [A, B, C] to [A, D, B, C]. A raw-index scheme would
        // have advanced from index 1 straight to whoever now sits at index 2 (C) — wrong.
        // Resolving by identity must still advance from B to C, D's insertion notwithstanding.
        var (round, nextId) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantId: B, [A, D, B, C]);

        round.Should().Be(1);
        nextId.Should().Be(C);
    }

    [Fact]
    public void Advance_when_the_current_participant_was_removed_falls_back_to_the_first_participant_of_the_same_round()
    {
        // The current participant was deleted mid-round (or SET NULL'd via the FK) — there's no
        // reliable "next" to count forward from, so land on the first participant of the SAME
        // round rather than guessing or silently skipping a round.
        var (round, nextId) = TurnAdvanceCalculator.Advance(currentRound: 3, currentParticipantId: B, [A, C]);

        round.Should().Be(3);
        nextId.Should().Be(A);
    }

    [Fact]
    public void IndexOf_returns_the_current_participants_live_position()
    {
        TurnAdvanceCalculator.IndexOf(B, [A, B, C]).Should().Be(1);
    }

    [Fact]
    public void IndexOf_returns_zero_when_unset_or_no_longer_present()
    {
        TurnAdvanceCalculator.IndexOf(null, [A, B, C]).Should().Be(0);
        TurnAdvanceCalculator.IndexOf(Guid.NewGuid(), [A, B, C]).Should().Be(0);
    }
}
