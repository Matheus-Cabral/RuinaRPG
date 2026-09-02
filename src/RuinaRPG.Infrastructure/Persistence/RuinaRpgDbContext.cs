using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Diary;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Invites;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.Persistence;

public class RuinaRpgDbContext(DbContextOptions<RuinaRpgDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<SpellAbilityBankEntry> SpellAbilityBankEntries => Set<SpellAbilityBankEntry>();
    public DbSet<SpellAbilityBankEffect> SpellAbilityBankEffects => Set<SpellAbilityBankEffect>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();
    public DbSet<CampaignAttachment> CampaignAttachments => Set<CampaignAttachment>();
    public DbSet<CharacterSheet> CharacterSheets => Set<CharacterSheet>();
    public DbSet<CharacterAttribute> CharacterAttributes => Set<CharacterAttribute>();
    public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();
    public DbSet<CharacterAffinity> CharacterAffinities => Set<CharacterAffinity>();
    public DbSet<CharacterRune> CharacterRunes => Set<CharacterRune>();
    public DbSet<CharacterMastery> CharacterMasteries => Set<CharacterMastery>();
    public DbSet<CharacterWeapon> CharacterWeapons => Set<CharacterWeapon>();
    public DbSet<CharacterArmorSlot> CharacterArmorSlots => Set<CharacterArmorSlot>();
    public DbSet<CharacterShield> CharacterShields => Set<CharacterShield>();
    public DbSet<CharacterInventoryItem> CharacterInventoryItems => Set<CharacterInventoryItem>();
    public DbSet<CharacterArtifact> CharacterArtifacts => Set<CharacterArtifact>();
    public DbSet<CharacterSpellAbility> CharacterSpellAbilities => Set<CharacterSpellAbility>();
    public DbSet<CharacterSpellAbilityEffect> CharacterSpellAbilityEffects => Set<CharacterSpellAbilityEffect>();
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
    public DbSet<DiaryEntryImage> DiaryEntryImages => Set<DiaryEntryImage>();
    public DbSet<DiaryEntryRecipient> DiaryEntryRecipients => Set<DiaryEntryRecipient>();
    public DbSet<Trait> Traits => Set<Trait>();
    public DbSet<CharacterAffection> CharacterAffections => Set<CharacterAffection>();
    public DbSet<CharacterTrait> CharacterTraits => Set<CharacterTrait>();
    public DbSet<NpcSheet> NpcSheets => Set<NpcSheet>();
    public DbSet<NpcAttribute> NpcAttributes => Set<NpcAttribute>();
    public DbSet<NpcSkill> NpcSkills => Set<NpcSkill>();
    public DbSet<NpcAffinity> NpcAffinities => Set<NpcAffinity>();
    public DbSet<NpcRune> NpcRunes => Set<NpcRune>();
    public DbSet<NpcMastery> NpcMasteries => Set<NpcMastery>();
    public DbSet<NpcWeapon> NpcWeapons => Set<NpcWeapon>();
    public DbSet<NpcArmorSlot> NpcArmorSlots => Set<NpcArmorSlot>();
    public DbSet<NpcShield> NpcShields => Set<NpcShield>();
    public DbSet<NpcInventoryItem> NpcInventoryItems => Set<NpcInventoryItem>();
    public DbSet<NpcArtifact> NpcArtifacts => Set<NpcArtifact>();
    public DbSet<NpcSpellAbility> NpcSpellAbilities => Set<NpcSpellAbility>();
    public DbSet<NpcSpellAbilityEffect> NpcSpellAbilityEffects => Set<NpcSpellAbilityEffect>();
    public DbSet<NpcAffection> NpcAffections => Set<NpcAffection>();
    public DbSet<NpcTrait> NpcTraits => Set<NpcTrait>();
    public DbSet<CreatureSheet> CreatureSheets => Set<CreatureSheet>();
    public DbSet<CreatureAttribute> CreatureAttributes => Set<CreatureAttribute>();
    public DbSet<CreatureSkill> CreatureSkills => Set<CreatureSkill>();
    public DbSet<CreatureMastery> CreatureMasteries => Set<CreatureMastery>();
    public DbSet<CreatureWeapon> CreatureWeapons => Set<CreatureWeapon>();
    public DbSet<CreatureArmorSlot> CreatureArmorSlots => Set<CreatureArmorSlot>();
    public DbSet<CreatureShield> CreatureShields => Set<CreatureShield>();
    public DbSet<CreatureSpoil> CreatureSpoils => Set<CreatureSpoil>();
    public DbSet<CreatureArtifact> CreatureArtifacts => Set<CreatureArtifact>();
    public DbSet<CreatureSpellAbility> CreatureSpellAbilities => Set<CreatureSpellAbility>();
    public DbSet<CreatureSpellAbilityEffect> CreatureSpellAbilityEffects => Set<CreatureSpellAbilityEffect>();
    public DbSet<CreatureAffection> CreatureAffections => Set<CreatureAffection>();
    public DbSet<CreatureTrait> CreatureTraits => Set<CreatureTrait>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<EncounterParticipant> EncounterParticipants => Set<EncounterParticipant>();
    public DbSet<EncounterParticipantCondition> EncounterParticipantConditions => Set<EncounterParticipantCondition>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            // Login e Cadastro R0003 - Nickname é único no sistema. The index sits on the
            // normalized (upper-invariant) column so the uniqueness is case-insensitive.
            entity.Property(u => u.NormalizedNickname).IsRequired();
            entity.HasIndex(u => u.NormalizedNickname).IsUnique();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InviteCode>(entity =>
        {
            entity.Property(c => c.Code).HasMaxLength(8);
            entity.HasIndex(c => c.Code).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.GmId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.RedeemedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Image>(entity =>
        {
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(i => i.UploadedByUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Item>(entity =>
        {
            entity.HasDiscriminator<string>("Tipo")
                .HasValue<ItemGeral>("ItemGeral")
                .HasValue<Arma>("Arma")
                .HasValue<Armadura>("Armadura")
                .HasValue<Escudo>("Escudo")
                .HasValue<Artefato>("Artefato");
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(i => i.GmId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Image>()
                .WithMany()
                .HasForeignKey(i => i.ImageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SpellAbilityBankEntry>(entity =>
        {
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.GmId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Efeitos)
                .WithOne()
                .HasForeignKey(ef => ef.SpellAbilityBankEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Campaign>(entity =>
        {
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.GmId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Image>()
                .WithMany()
                .HasForeignKey(c => c.ImageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CharacterSheet>(entity =>
        {
            entity.HasOne<Campaign>()
                .WithMany()
                .HasForeignKey(s => s.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Image>()
                .WithMany()
                .HasForeignKey(s => s.ImageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CharacterAttribute>(entity =>
        {
            entity.HasIndex(a => new { a.CharacterSheetId, a.Atributo }).IsUnique();
            entity.HasOne<CharacterSheet>()
                .WithMany()
                .HasForeignKey(a => a.CharacterSheetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CharacterSkill>(entity =>
        {
            entity.HasIndex(s => new { s.CharacterSheetId, s.Pericia }).IsUnique();
            entity.HasOne<CharacterSheet>()
                .WithMany()
                .HasForeignKey(s => s.CharacterSheetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CharacterAffinity>(entity =>
        {
            entity.HasOne<CharacterSheet>()
                .WithMany()
                .HasForeignKey(a => a.CharacterSheetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CharacterRune>(entity => entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(r => r.CharacterSheetId).OnDelete(DeleteBehavior.Cascade));
        builder.Entity<CharacterMastery>(entity => entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(m => m.CharacterSheetId).OnDelete(DeleteBehavior.Cascade));

        builder.Entity<CharacterWeapon>(entity =>
        {
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(w => w.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(w => w.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CharacterArmorSlot>(entity =>
        {
            entity.HasIndex(a => new { a.CharacterSheetId, a.Slot }).IsUnique();
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(a => a.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CharacterShield>(entity =>
        {
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(s => s.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(s => s.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CharacterInventoryItem>(entity =>
        {
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(i => i.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CharacterArtifact>(entity =>
        {
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(a => a.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ArtifactItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CharacterSpellAbility>(entity =>
        {
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(e => e.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Efeitos).WithOne().HasForeignKey(ef => ef.CharacterSpellAbilityId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CampaignMember>(entity =>
        {
            entity.HasIndex(m => new { m.CampaignId, m.UserId }).IsUnique();
            entity.HasOne<Campaign>()
                .WithMany()
                .HasForeignKey(m => m.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CampaignAttachment>(entity =>
        {
            entity.HasOne<Campaign>().WithMany().HasForeignKey(a => a.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(a => a.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(a => a.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<SpellAbilityBankEntry>().WithMany().HasForeignKey(a => a.SpellAbilityBankEntryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Image>().WithMany().HasForeignKey(a => a.ImageId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DiaryEntry>(entity =>
        {
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(d => d.AuthorUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Campaign>()
                .WithMany()
                .HasForeignKey(d => d.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CharacterSheet>()
                .WithMany()
                .HasForeignKey(d => d.CharacterSheetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DiaryEntryImage>(entity =>
        {
            entity.HasKey(i => new { i.DiaryEntryId, i.ImageId });
            entity.HasOne<DiaryEntry>()
                .WithMany()
                .HasForeignKey(i => i.DiaryEntryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Image>()
                .WithMany()
                .HasForeignKey(i => i.ImageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CharacterAffection>(entity =>
        {
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(a => a.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CharacterTrait>(entity =>
        {
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(t => t.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Trait>().WithMany().HasForeignKey(t => t.TraitId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DiaryEntryRecipient>(entity =>
        {
            entity.HasKey(r => new { r.DiaryEntryId, r.UserId });
            entity.HasOne<DiaryEntry>()
                .WithMany()
                .HasForeignKey(r => r.DiaryEntryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NpcSheet>(entity =>
        {
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.GmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Image>().WithMany().HasForeignKey(s => s.ImageId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CreatureSheet>(entity =>
        {
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.GmId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Image>().WithMany().HasForeignKey(s => s.ImageId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<NpcAttribute>(entity =>
        {
            entity.HasIndex(a => new { a.NpcSheetId, a.Atributo }).IsUnique();
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(a => a.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NpcSkill>(entity =>
        {
            entity.HasIndex(s => new { s.NpcSheetId, s.Pericia }).IsUnique();
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(s => s.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NpcAffinity>(entity => entity.HasOne<NpcSheet>().WithMany().HasForeignKey(a => a.NpcSheetId).OnDelete(DeleteBehavior.Cascade));

        builder.Entity<NpcRune>(entity => entity.HasOne<NpcSheet>().WithMany().HasForeignKey(r => r.NpcSheetId).OnDelete(DeleteBehavior.Cascade));
        builder.Entity<NpcMastery>(entity => entity.HasOne<NpcSheet>().WithMany().HasForeignKey(m => m.NpcSheetId).OnDelete(DeleteBehavior.Cascade));

        builder.Entity<NpcWeapon>(entity =>
        {
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(w => w.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(w => w.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NpcArmorSlot>(entity =>
        {
            entity.HasIndex(a => new { a.NpcSheetId, a.Slot }).IsUnique();
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(a => a.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NpcShield>(entity =>
        {
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(s => s.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(s => s.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NpcInventoryItem>(entity =>
        {
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(i => i.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NpcArtifact>(entity =>
        {
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(a => a.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ArtifactItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NpcSpellAbility>(entity =>
        {
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(e => e.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Efeitos).WithOne().HasForeignKey(ef => ef.NpcSpellAbilityId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NpcAffection>(entity => entity.HasOne<NpcSheet>().WithMany().HasForeignKey(a => a.NpcSheetId).OnDelete(DeleteBehavior.Cascade));

        builder.Entity<NpcTrait>(entity =>
        {
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(t => t.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Trait>().WithMany().HasForeignKey(t => t.TraitId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CreatureAttribute>(entity =>
        {
            entity.HasIndex(a => new { a.CreatureSheetId, a.Atributo }).IsUnique();
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(a => a.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CreatureSkill>(entity =>
        {
            entity.HasIndex(s => new { s.CreatureSheetId, s.Pericia }).IsUnique();
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(s => s.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CreatureMastery>(entity => entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(m => m.CreatureSheetId).OnDelete(DeleteBehavior.Cascade));

        builder.Entity<CreatureWeapon>(entity =>
        {
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(w => w.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(w => w.ItemId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CreatureArmorSlot>(entity =>
        {
            entity.HasIndex(a => new { a.CreatureSheetId, a.Slot }).IsUnique();
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(a => a.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CreatureShield>(entity =>
        {
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(s => s.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(s => s.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CreatureSpoil>(entity =>
        {
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(i => i.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CreatureArtifact>(entity =>
        {
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(a => a.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ArtifactItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CreatureSpellAbility>(entity =>
        {
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(e => e.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Efeitos).WithOne().HasForeignKey(ef => ef.CreatureSpellAbilityId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CreatureAffection>(entity => entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(a => a.CreatureSheetId).OnDelete(DeleteBehavior.Cascade));

        builder.Entity<CreatureTrait>(entity =>
        {
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(t => t.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Trait>().WithMany().HasForeignKey(t => t.TraitId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Encounter>(entity =>
        {
            entity.HasOne<Campaign>().WithMany().HasForeignKey(e => e.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EncounterParticipant>().WithMany().HasForeignKey(e => e.CurrentParticipantId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<EncounterParticipant>(entity =>
        {
            entity.HasOne<Encounter>().WithMany().HasForeignKey(p => p.EncounterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(p => p.SourceCharacterSheetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<NpcSheet>().WithMany().HasForeignKey(p => p.SourceNpcSheetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(p => p.SourceCreatureSheetId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<EncounterParticipantCondition>(entity =>
            entity.HasOne<EncounterParticipant>().WithMany().HasForeignKey(c => c.EncounterParticipantId).OnDelete(DeleteBehavior.Cascade));
    }
}
