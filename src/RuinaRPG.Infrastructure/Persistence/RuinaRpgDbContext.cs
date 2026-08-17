using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
    }
}
