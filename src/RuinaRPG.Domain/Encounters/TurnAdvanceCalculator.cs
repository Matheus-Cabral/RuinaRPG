namespace RuinaRPG.Domain.Encounters;

public static class TurnAdvanceCalculator
{
    /// <summary>
    /// Advances by participant identity, not a raw stored position — a mid-round Add or
    /// Iniciativa edit can reorder the participant list without AdvanceTurn itself being called,
    /// so "next" must always be resolved against the CURRENT ordering, never a stale index
    /// (Épico 5 item 2 of the gap audit: a raw index silently mis-targeted whoever now happened
    /// to sit at that position after such a reorder).
    /// </summary>
    /// <param name="currentParticipantId">
    /// Whose turn it currently is, or null for a fresh encounter that has never advanced — which
    /// implicitly starts on the FIRST participant's turn (matching the old CurrentParticipantIndex
    /// column's 0 default), so the first-ever AdvanceTurn call moves past them to the second.
    /// </param>
    /// <param name="orderedParticipantIds">
    /// The live ordering (Iniciativa descending, then Id — the encounter's own stable tie-break),
    /// evaluated fresh by the caller for this call. Must be non-empty.
    /// </param>
    public static (int NextRound, Guid NextParticipantId) Advance(int currentRound, Guid? currentParticipantId, IReadOnlyList<Guid> orderedParticipantIds)
    {
        int currentIndex;
        if (currentParticipantId is null)
        {
            currentIndex = 0;
        }
        else
        {
            var found = FindIndex(currentParticipantId.Value, orderedParticipantIds);
            // Not found: the current participant was removed or reordered out from under us
            // mid-round — there's no reliable "next" to count forward from, so land on the first
            // participant of the SAME round rather than guessing or skipping one.
            currentIndex = found < 0 ? -1 : found;
        }

        var nextIndex = currentIndex + 1;
        return nextIndex < orderedParticipantIds.Count
            ? (currentRound, orderedParticipantIds[nextIndex])
            : (currentRound + 1, orderedParticipantIds[0]);
    }

    /// <summary>
    /// The current participant's live position in the current ordering, for display only (e.g.
    /// highlighting the active row) — 0 if unset or no longer present in the ordering.
    /// </summary>
    public static int IndexOf(Guid? currentParticipantId, IReadOnlyList<Guid> orderedParticipantIds)
    {
        if (currentParticipantId is null)
            return 0;

        var index = FindIndex(currentParticipantId.Value, orderedParticipantIds);
        return index < 0 ? 0 : index;
    }

    private static int FindIndex(Guid id, IReadOnlyList<Guid> ids)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
                return i;
        }
        return -1;
    }
}
