using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class GrantedSheetAuthorizationTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Gm = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    [Fact]
    public void CanEdit_is_true_for_the_owning_gm()
    {
        GrantedSheetAuthorization.CanEdit(Gm, ownerId: null, Gm).Should().BeTrue();
    }

    [Fact]
    public void CanEdit_is_true_for_the_player_the_sheet_was_granted_to()
    {
        GrantedSheetAuthorization.CanEdit(Owner, Owner, Gm).Should().BeTrue();
    }

    [Fact]
    public void CanEdit_is_false_for_anyone_else()
    {
        GrantedSheetAuthorization.CanEdit(Stranger, Owner, Gm).Should().BeFalse();
    }

    [Fact]
    public void CanEdit_is_false_for_a_stranger_when_the_sheet_has_never_been_granted()
    {
        // OwnerId is null until a grant happens (Campanha R0010) — a null owner must never match.
        GrantedSheetAuthorization.CanEdit(Stranger, ownerId: null, Gm).Should().BeFalse();
    }
}
