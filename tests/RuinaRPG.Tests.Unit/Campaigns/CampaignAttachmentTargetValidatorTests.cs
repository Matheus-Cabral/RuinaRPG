using FluentAssertions;
using RuinaRPG.Domain.Campaigns;

namespace RuinaRPG.Tests.Unit.Campaigns;

public class CampaignAttachmentTargetValidatorTests
{
    [Fact]
    public void ExactlyOneSet_is_true_when_only_one_id_is_provided()
    {
        CampaignAttachmentTargetValidator.ExactlyOneSet("item-1", null, null, null, null).Should().BeTrue();
        CampaignAttachmentTargetValidator.ExactlyOneSet(null, null, null, null, "image-1").Should().BeTrue();
    }

    [Fact]
    public void ExactlyOneSet_is_false_when_none_are_provided()
    {
        CampaignAttachmentTargetValidator.ExactlyOneSet(null, null, null, null, null).Should().BeFalse();
    }

    [Fact]
    public void ExactlyOneSet_is_false_when_more_than_one_is_provided()
    {
        CampaignAttachmentTargetValidator.ExactlyOneSet("item-1", "npc-1", null, null, null).Should().BeFalse();
    }

    [Fact]
    public void ExactlyOneSet_counts_the_rune_bank_target_as_a_sixth_alternative()
    {
        CampaignAttachmentTargetValidator.ExactlyOneSet(null, null, null, null, null, "rune-1").Should().BeTrue();
        CampaignAttachmentTargetValidator.ExactlyOneSet("item-1", null, null, null, null, "rune-1").Should().BeFalse();
        CampaignAttachmentTargetValidator.ExactlyOneSet(null, null, null, null, null, null).Should().BeFalse();
    }
}
