using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    RuinaRpgDbContext db,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private const string NicknameTakenMessage = "Este Nickname já está em uso.";

    [HttpPost("register/gm")]
    public async Task<ActionResult<AuthResponse>> RegisterGm(RegisterGmRequest request)
    {
        if (request.Senha != request.ConfirmacaoSenha)
            return BadRequest("A confirmação de senha não confere com a senha.");

        // Login e Cadastro R0003 - o Nickname é único no sistema. Checked here for a clean
        // message; the unique index on NormalizedNickname is what actually guarantees it,
        // and the catch below turns the race-losing insert into the same 400.
        if (await NicknameIsTakenAsync(request.Nickname))
            return BadRequest(NicknameTakenMessage);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Nickname = request.Nickname,
            Role = UserRole.GM
        };

        try
        {
            var result = await userManager.CreateAsync(user, request.Senha);
            if (!result.Succeeded)
                return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

            return Created(string.Empty, await IssueTokensAsync(user));
        }
        catch (DbUpdateException)
        {
            // The check above is a check-then-insert, so a concurrent registration can still
            // lose the race against the unique index. Translate that into the same 400
            // instead of letting it surface as a 500.
            db.ChangeTracker.Clear();
            if (await NicknameIsTakenAsync(request.Nickname))
                return BadRequest(NicknameTakenMessage);

            throw;
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        // UserName is set to Email at registration (Task 5), so login — which is by
        // Nickname (R0002) — must query by the Nickname column directly, not FindByNameAsync.
        var user = await FindByNicknameAsync(request.Nickname);

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Senha))
            return Unauthorized("Credenciais inválidas.");

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var hash = refreshTokenService.Hash(request.RefreshToken);
        var stored = db.RefreshTokens.SingleOrDefault(t => t.TokenHash == hash);

        if (stored is null || !stored.IsActive)
            return Unauthorized();

        stored.RevokedAt = DateTime.UtcNow;
        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        var response = await IssueTokensAsync(user!);
        await db.SaveChangesAsync();

        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var hash = refreshTokenService.Hash(request.RefreshToken);
        var stored = db.RefreshTokens.SingleOrDefault(t => t.TokenHash == hash);

        if (stored is not null && stored.IsActive)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>
    /// Echoes the caller's own claims. Exists so the authenticated half of the slice
    /// (issue token -> validate token -> read identity) is reachable and testable.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public ActionResult<MeResponse> Me() => Ok(new MeResponse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty,
        User.FindFirstValue("nickname") ?? string.Empty,
        User.FindFirstValue("role") ?? string.Empty));

    // Matches the normalized column that carries the unique index, so the lookup is
    // case-insensitive and can never find more than one row.
    private async Task<ApplicationUser?> FindByNicknameAsync(string nickname)
    {
        var normalized = ApplicationUser.Normalize(nickname);
        return await db.Users.SingleOrDefaultAsync(u => u.NormalizedNickname == normalized);
    }

    private async Task<bool> NicknameIsTakenAsync(string nickname)
    {
        var normalized = ApplicationUser.Normalize(nickname);
        return await db.Users.AnyAsync(u => u.NormalizedNickname == normalized);
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        var accessToken = jwtTokenService.CreateAccessToken(user);
        var (plainTextRefreshToken, refreshTokenEntity) =
            refreshTokenService.Generate(user.Id, jwtOptions.Value.RefreshTokenDays);

        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, plainTextRefreshToken);
    }
}
