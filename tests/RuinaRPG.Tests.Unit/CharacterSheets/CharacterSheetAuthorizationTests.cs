using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class CharacterSheetAuthorizationTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid CampaignGm = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    [Fact]
    public void CanEdit_is_true_for_the_owner()
    {
        CharacterSheetAuthorization.CanEdit(Owner, Owner, CampaignGm).Should().BeTrue();
    }

    [Fact]
    public void CanEdit_is_true_for_the_campaigns_gm()
    {
        CharacterSheetAuthorization.CanEdit(CampaignGm, Owner, CampaignGm).Should().BeTrue();
    }

    [Fact]
    public void CanEdit_is_false_for_anyone_else()
    {
        CharacterSheetAuthorization.CanEdit(Stranger, Owner, CampaignGm).Should().BeFalse();
    }
}
