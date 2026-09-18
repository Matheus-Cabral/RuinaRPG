using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Domain;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Invites;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
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

        if (string.IsNullOrWhiteSpace(request.Nickname))
            return BadRequest("Nickname é obrigatório.");

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

            // Catálogo "Exemplo de dados" convention: every GM starts with the transcribed
            // ruina-itens.docx catalog (DefaultCatalogItems), editable/deletable like any other
            // item from the moment it's created (Catálogo R0007). GMs that existed before this
            // feature are backfilled separately — see DefaultCatalogSeeder.SeedMissingAsync in
            // Program.cs.
            await DefaultCatalogSeeder.SeedForNewGmAsync(db, user.Id);

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

    [HttpPost("register/jogador")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> RegisterJogador(RegisterJogadorRequest request)
    {
        if (request.Senha != request.ConfirmacaoSenha)
            return BadRequest("A confirmação de senha não confere com a senha.");

        if (string.IsNullOrWhiteSpace(request.Nickname))
            return BadRequest("Nickname é obrigatório.");

        if (await NicknameIsTakenAsync(request.Nickname))
            return BadRequest(NicknameTakenMessage);

        var inviteCode = await db.InviteCodes.SingleOrDefaultAsync(c => c.Code == request.CodigoDeAcesso);
        if (inviteCode is null)
            return BadRequest("Código de acesso inválido.");

        var status = InviteCodeStatusCalculator.Compute(inviteCode.RevokedAt, inviteCode.RedeemedByUserId, inviteCode.ExpiresAt, DateTime.UtcNow);
        var statusError = status switch
        {
            InviteCodeStatus.Usado => "Este código de acesso já foi usado.",
            InviteCodeStatus.Revogado => "Este código de acesso foi revogado.",
            InviteCodeStatus.Expirado => "Este código de acesso expirou.",
            _ => null
        };
        if (statusError is not null)
            return BadRequest(statusError);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Nickname = request.Nickname,
            Role = UserRole.Jogador,
            InvitedByGmId = inviteCode.GmId
        };

        try
        {
            var result = await userManager.CreateAsync(user, request.Senha);
            if (!result.Succeeded)
                return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

            inviteCode.RedeemedByUserId = user.Id;
            inviteCode.RedeemedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Created(string.Empty, await IssueTokensAsync(user));
        }
        catch (DbUpdateException)
        {
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
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var role = User.FindFirstValue("role") ?? string.Empty;

        bool isRulesAuditor = false;
        bool mustChangePassword = false;
        string? lastSeenAppVersion = null;
        if (userId is not null)
        {
            var flags = await db.Users.Where(u => u.Id == Guid.Parse(userId))
                .Select(u => new { u.IsRulesAuditor, u.LastSeenAppVersion, u.MustChangePassword })
                .SingleOrDefaultAsync();
            isRulesAuditor = flags?.IsRulesAuditor ?? false;
            lastSeenAppVersion = flags?.LastSeenAppVersion;
            mustChangePassword = flags?.MustChangePassword ?? false;
        }

        // Only a GM who hasn't dismissed the current version sees the popup — see Requisitos/spec
        // for why Jogador never does.
        var pendingChangelogVersion = role == "GM" && lastSeenAppVersion != AppVersionInfo.Current
            ? AppVersionInfo.Current
            : null;

        return Ok(new MeResponse(
            userId ?? string.Empty,
            User.FindFirstValue("nickname") ?? string.Empty,
            role,
            isRulesAuditor,
            pendingChangelogVersion,
            mustChangePassword));
    }

    /// <summary>
    /// Self-service password change for the caller's own account — the only path a GM has to
    /// clear MustChangePassword after a console-issued temporary password (see
    /// GmPasswordResetCli), but usable any time, not just while that flag is set.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (request.NovaSenha != request.ConfirmacaoNovaSenha)
            return BadRequest("A confirmação de senha não confere com a nova senha.");

        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var user = await userManager.FindByIdAsync(userId!);
        if (user is null)
            return Unauthorized();

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, request.NovaSenha);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = false;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("dismiss-changelog")]
    [Authorize]
    public async Task<IActionResult> DismissChangelog()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null)
            return Unauthorized();

        var user = await db.Users.FindAsync(Guid.Parse(userId));
        if (user is null)
            return Unauthorized();

        user.LastSeenAppVersion = AppVersionInfo.Current;
        await db.SaveChangesAsync();
        return NoContent();
    }

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
