using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Domain.Invites;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Invites;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/invite-codes")]
[Authorize(Roles = "GM")]
public class InviteCodesController(RuinaRpgDbContext db) : ControllerBase
{
    private const string CodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int CodeLength = 8;
    private const int MaxGenerationAttempts = 5;

    [HttpPost]
    public async Task<ActionResult<InviteCodeResponse>> Generate()
    {
        var gmId = CurrentGmId();

        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var code = new InviteCode
            {
                Id = Guid.NewGuid(),
                Code = GenerateRandomCode(),
                GmId = gmId,
                GeneratedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(48)
            };

            db.InviteCodes.Add(code);

            try
            {
                await db.SaveChangesAsync();
                return Created(string.Empty, ToResponse(code));
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Não foi possível gerar um código de convite único após várias tentativas.");
    }

    [HttpGet]
    public async Task<ActionResult<List<InviteCodeResponse>>> List()
    {
        var gmId = CurrentGmId();

        var codes = await db.InviteCodes
            .Where(c => c.GmId == gmId)
            .OrderByDescending(c => c.GeneratedAt)
            .ToListAsync();

        var redeemerIds = codes.Where(c => c.RedeemedByUserId is not null).Select(c => c.RedeemedByUserId!.Value).ToList();
        var redeemers = await db.Users.Where(u => redeemerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        return codes.Select(c => ToResponse(c, redeemers)).ToList();
    }

    [HttpPost("{code}/revoke")]
    public async Task<IActionResult> Revoke(string code)
    {
        var gmId = CurrentGmId();
        var inviteCode = await db.InviteCodes.SingleOrDefaultAsync(c => c.Code == code && c.GmId == gmId);

        if (inviteCode is null)
            return NotFound();

        var status = InviteCodeStatusCalculator.Compute(inviteCode.RevokedAt, inviteCode.RedeemedByUserId, inviteCode.ExpiresAt, DateTime.UtcNow);
        if (status != InviteCodeStatus.Ativo)
            return BadRequest("Somente um código Ativo pode ser revogado.");

        inviteCode.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static string GenerateRandomCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(CodeLength);
        return new string(bytes.Select(b => CodeChars[b % CodeChars.Length]).ToArray());
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static InviteCodeResponse ToResponse(InviteCode code, Dictionary<Guid, ApplicationUser>? redeemers = null)
    {
        var status = InviteCodeStatusCalculator.Compute(code.RevokedAt, code.RedeemedByUserId, code.ExpiresAt, DateTime.UtcNow);
        ApplicationUser? redeemer = null;
        if (code.RedeemedByUserId is not null)
            redeemers?.TryGetValue(code.RedeemedByUserId.Value, out redeemer);

        return new InviteCodeResponse(
            code.Code,
            status.ToString(),
            code.GeneratedAt,
            status == InviteCodeStatus.Ativo ? code.ExpiresAt : null,
            redeemer?.Nickname,
            redeemer?.Email,
            code.RedeemedAt);
    }
}
