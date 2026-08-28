namespace RuinaRPG.Infrastructure.Campaigns;

public class CampaignAttachment
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? NpcSheetId { get; set; }
    public Guid? CreatureSheetId { get; set; }
    public Guid? SpellAbilityBankEntryId { get; set; }
    public Guid? ImageId { get; set; }
    public bool IsPublic { get; set; }
    public bool NpcNomePublico { get; set; }
    public bool NpcImagemPublica { get; set; }
    public bool CreatureNomePublico { get; set; }
    public bool CreatureImagemPublica { get; set; }
}
