using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Invites;

namespace RuinaRPG.Infrastructure.Persistence;

public class RuinaRpgDbContext(DbContextOptions<RuinaRpgDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();
    public DbSet<Image> Images => Set<Image>();

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
    }
}
