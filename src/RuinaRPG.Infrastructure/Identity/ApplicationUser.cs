using Microsoft.AspNetCore.Identity;
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    private string _nickname = string.Empty;

    /// <summary>
    /// The display form of the nickname, exactly as the user typed it.
    /// </summary>
    public required string Nickname
    {
        get => _nickname;
        set
        {
            _nickname = value;
            NormalizedNickname = Normalize(value);
        }
    }

    /// <summary>
    /// Upper-invariant form of <see cref="Nickname"/>, kept in sync by the setter above.
    /// It carries the unique index that enforces Login e Cadastro R0003 case-insensitively
    /// (Postgres text comparison is case-sensitive, so a plain index on Nickname would let
    /// "Mestre" and "mestre" coexist), and it is what nickname lookups match against.
    /// Mirrors how Identity itself pairs UserName/NormalizedUserName.
    /// </summary>
    public string NormalizedNickname { get; private set; } = string.Empty;

    public UserRole Role { get; set; }
    public Guid? InvitedByGmId { get; set; }

    // Granted via `make grant-rules-auditor EMAIL=...` (Program.cs's --grant-rules-auditor arg) —
    // checked directly against this column on every request (AuthController.Me, and the new
    // Traits/RulebookDocuments admin endpoints), never baked into the JWT, so a grant/revoke takes
    // effect on the very next request instead of waiting for the holder to log in again.
    public bool IsRulesAuditor { get; set; }

    // Null-tolerant: a request body that omits the nickname binds null here, and that must
    // surface as a 400 from the not-null/unique constraints, not as an NRE in this setter.
    public static string Normalize(string? nickname) => nickname?.ToUpperInvariant() ?? string.Empty;
}
