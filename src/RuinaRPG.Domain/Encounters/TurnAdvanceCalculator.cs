namespace RuinaRPG.Domain.Encounters;

public static class TurnAdvanceCalculator
{
    public static (int NextRound, int NextParticipantIndex) Advance(int currentRound, int currentParticipantIndex, int participantCount)
    {
        var nextIndex = currentParticipantIndex + 1;
        return nextIndex < participantCount
            ? (currentRound, nextIndex)
            : (currentRound + 1, 0);
    }
}
