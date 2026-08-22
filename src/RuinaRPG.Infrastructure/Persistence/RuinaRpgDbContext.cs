using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Diary;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Invites;
using RuinaRPG.Infrastructure.Items;
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
    public DbSet<CharacterSheet> CharacterSheets => Set<CharacterSheet>();
    public DbSet<CharacterAttribute> CharacterAttributes => Set<CharacterAttribute>();
    public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
    public DbSet<DiaryEntryImage> DiaryEntryImages => Set<DiaryEntryImage>();
    public DbSet<DiaryEntryRecipient> DiaryEntryRecipients => Set<DiaryEntryRecipient>();

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
            // CharacterSheetId's FK is added by the Ficha de Personagem plan once CharacterSheets exists —
            // left as a plain nullable Guid column here, no FK constraint yet.
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
    }
}
