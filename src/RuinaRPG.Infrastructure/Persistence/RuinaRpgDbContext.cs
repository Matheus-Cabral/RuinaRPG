using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Persistence;

public class RuinaRpgDbContext(DbContextOptions<RuinaRpgDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
    }
}
