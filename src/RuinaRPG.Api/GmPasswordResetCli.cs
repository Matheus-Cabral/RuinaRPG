using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api;

/// <summary>Null <see cref="Error"/> means success; the other two fields are set only then.</summary>
public sealed record GmPasswordResetResult(string? Error, string? Nickname, string? TemporaryPassword);

/// <summary>
/// Backs Program.cs's --reset-gm-password one-shot CLI arg (see the Makefile's
/// reset-gm-password target, which exec's into the running container to invoke it). There is no
/// mailing service configured (Técnico R0003 doesn't list one), so a GM who forgets their
/// password has no self-service recovery — this gives the operator a console command that prints
/// a temporary password to hand the GM out-of-band, mirroring how PlayersController.ResetPassword
/// lets a GM do the same for their own players.
/// </summary>
public static class GmPasswordResetCli
{
    public static async Task<GmPasswordResetResult> ResetGmPasswordAsync(RuinaRpgDbContext db, UserManager<ApplicationUser> userManager, string email)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user is null)
            return new GmPasswordResetResult($"Nenhum usuário encontrado com o e-mail \"{email}\".", null, null);

        if (user.Role != UserRole.GM)
            return new GmPasswordResetResult($"\"{email}\" não é uma conta de GM — a recuperação de senha via console só se aplica a contas de GM.", null, null);

        var temporaryPassword = GenerateTemporaryPassword();

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, temporaryPassword);
        if (!result.Succeeded)
            return new GmPasswordResetResult(string.Join("; ", result.Errors.Select(e => e.Description)), null, null);

        // The old password is presumed lost/compromised (that's why the operator is running this
        // command), so every existing session is killed rather than left valid until it expires
        // on its own.
        var activeRefreshTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync();
        foreach (var token in activeRefreshTokens)
            token.RevokedAt = DateTime.UtcNow;

        user.MustChangePassword = true;
        await db.SaveChangesAsync();

        return new GmPasswordResetResult(null, user.Nickname, temporaryPassword);
    }

    // Satisfies ASP.NET Core Identity's default password policy (min length 6, at least one
    // upper/lower/digit/non-alphanumeric character) with margin to spare, using a CSPRNG since
    // this is a real, if temporary, credential.
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*";
        const string all = upper + lower + digits + special;

        var chars = new List<char>
        {
            PickRandom(upper),
            PickRandom(lower),
            PickRandom(digits),
            PickRandom(special),
        };
        for (var i = 0; i < 8; i++)
            chars.Add(PickRandom(all));

        // Otherwise the password would always start with one upper/lower/digit/special char in
        // that fixed order.
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }

    private static char PickRandom(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
