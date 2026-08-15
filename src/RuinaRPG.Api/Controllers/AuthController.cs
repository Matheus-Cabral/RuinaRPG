using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    [HttpPost("register/gm")]
    public async Task<ActionResult<AuthResponse>> RegisterGm(RegisterGmRequest request)
    {
        if (request.Senha != request.ConfirmacaoSenha)
            return BadRequest("A confirmação de senha não confere com a senha.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Nickname = request.Nickname,
            Role = UserRole.GM
        };

        var result = await userManager.CreateAsync(user, request.Senha);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Created(string.Empty, await IssueTokensAsync(user));
    }

    [HttpPost("login")]
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

    private async Task<ApplicationUser?> FindByNicknameAsync(string nickname) =>
        await db.Users.SingleOrDefaultAsync(u => u.Nickname == nickname);

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
