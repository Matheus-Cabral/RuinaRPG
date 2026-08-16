using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RuinaRPG.Contracts.Players;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/players")]
[Authorize(Roles = "GM")]
public class PlayersController(RuinaRpgDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PlayerSearchResultResponse>>> Search([FromQuery] string? q)
    {
        var gmId = CurrentGmId();

        var query = db.Users.Where(u => u.InvitedByGmId == gmId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var normalized = q.ToUpperInvariant();
            query = query.Where(u =>
                u.NormalizedNickname.Contains(normalized) ||
                u.NormalizedEmail!.Contains(normalized));
        }

        var players = await query
            .OrderBy(u => u.Nickname)
            .Select(u => new PlayerSearchResultResponse(u.Id.ToString(), u.Nickname, u.Email!))
            .ToListAsync();

        return players;
    }

    [HttpPost("{playerId}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid playerId, ResetPlayerPasswordRequest request)
    {
        if (request.NovaSenha != request.ConfirmacaoNovaSenha)
            return BadRequest("A confirmação de senha não confere com a nova senha.");

        var gmId = CurrentGmId();
        var player = await userManager.FindByIdAsync(playerId.ToString());

        if (player is null || player.InvitedByGmId != gmId)
            return NotFound();

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(player);
        var result = await userManager.ResetPasswordAsync(player, resetToken, request.NovaSenha);

        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return NoContent();
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
