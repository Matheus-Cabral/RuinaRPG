using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api;

/// <summary>
/// Backs Program.cs's --grant-rules-auditor/--revoke-rules-auditor one-shot CLI args (see the
/// Makefile's grant-rules-auditor/revoke-rules-auditor targets, which exec into the running
/// container to invoke them). Extracted to a plain static method — rather than inlined in
/// Program.cs's top-level statements, which can't otherwise be exercised by a test — so this is
/// directly testable against a real database.
/// </summary>
public static class RulesAuditorCli
{
    /// <summary>Returns null on success, or a human-readable error message otherwise.</summary>
    public static async Task<string?> SetRulesAuditorAsync(RuinaRpgDbContext db, string email, bool grant)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user is null)
            return $"Nenhum usuário encontrado com o e-mail \"{email}\".";

        // Only grant is restricted to GM — revoke is always safe (it only ever turns access off,
        // and a Jogador should never have had it in the first place, but clearing a stray true is
        // harmless).
        if (grant && user.Role != UserRole.GM)
            return $"\"{email}\" não é uma conta de GM — só um GM pode ser Auditor de Regras.";

        user.IsRulesAuditor = grant;
        await db.SaveChangesAsync();
        return null;
    }
}
