using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
