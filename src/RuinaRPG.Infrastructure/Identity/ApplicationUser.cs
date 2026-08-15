using Microsoft.AspNetCore.Identity;
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string Nickname { get; set; }
    public UserRole Role { get; set; }
    public Guid? InvitedByGmId { get; set; }
}
