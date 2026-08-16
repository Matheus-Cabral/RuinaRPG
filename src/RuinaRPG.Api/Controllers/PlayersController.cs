using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RuinaRPG.Contracts.Players;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/players")]
[Authorize(Roles = "GM")]
public class PlayersController(RuinaRpgDbContext db) : ControllerBase
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

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
