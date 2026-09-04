using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class TipoChipTests : MudBunitContext
{
    // Two value domains show up as "Tipo" across the app: campaign-attachment Tipo
    // (Item/SpellAbilityBankEntry/Image/NpcSheet/CreatureSheet, from CampaignAttachmentResponse
    // and PublicAttachmentSummary) and grant Tipo (Npc/Creature, from GrantSummaryResponse).
    [Theory]
    [InlineData("Item", "Item")]
    [InlineData("Image", "Imagem")]
    [InlineData("SpellAbilityBankEntry", "Magia/Habilidade")]
    [InlineData("NpcSheet", "NPC")]
    [InlineData("CreatureSheet", "Criatura")]
    [InlineData("Npc", "NPC")]
    [InlineData("Creature", "Criatura")]
    public void Renders_a_friendly_label_for_a_known_Tipo(string tipo, string expectedLabel)
    {
        var cut = Render<TipoChip>(p => p.Add(x => x.Tipo, tipo));

        cut.Markup.Should().Contain(expectedLabel);
    }

    [Fact]
    public void Falls_back_to_the_raw_value_for_an_unknown_Tipo()
    {
        var cut = Render<TipoChip>(p => p.Add(x => x.Tipo, "AlgoNovo"));

        cut.Markup.Should().Contain("AlgoNovo");
    }
}
