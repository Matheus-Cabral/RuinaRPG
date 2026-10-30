using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Players;
using RuinaRPG.Domain.Auth;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/players")]
[Authorize(Roles = "GM")]
public class PlayersController(RuinaRpgDbContext db, UserManager<ApplicationUser> userManager, SaidaDoJogador saida) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PlayerSearchResultResponse>>> Search([FromQuery] string? q)
    {
        var gmId = CurrentGmId();

        var query = db.Users.Where(u => u.InvitedByGmId == gmId && u.Estado != EstadoDaConta.Excluida);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var normalized = q.ToUpperInvariant();
            query = query.Where(u =>
                u.NormalizedNickname.Contains(normalized) ||
                u.NormalizedEmail!.Contains(normalized));
        }

        var players = await query
            .OrderBy(u => u.Nickname)
            .Select(u => new PlayerSearchResultResponse(u.Id.ToString(), u.Nickname, u.Email!, u.Estado.ToString()))
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

        if (player is null || !ContaDeJogador.PodeGerir(gmId, player.InvitedByGmId) || player.Estado == EstadoDaConta.Excluida)
            return NotFound();

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(player);
        var result = await userManager.ResetPasswordAsync(player, resetToken, request.NovaSenha);

        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return NoContent();
    }

    /// <summary>Convite de Jogador R0008: sai de todas as campanhas do GM e perde as sessões; o login fica recusado.</summary>
    [HttpPost("{playerId}/desativar")]
    [ConflitoDeConcorrenciaDoEncontro]
    public Task<IActionResult> Desativar(Guid playerId) => MudarEstadoAsync(playerId, AcaoDeConta.Desativar);

    /// <summary>Convite de Jogador R0008: volta a Ativa; nenhuma campanha é restaurada.</summary>
    [HttpPost("{playerId}/reativar")]
    public Task<IActionResult> Reativar(Guid playerId) => MudarEstadoAsync(playerId, AcaoDeConta.Reativar);

    /// <summary>
    /// Convite de Jogador R0009: faz o que Desativar faz e apaga os dados de login (o e-mail fica livre); a linha da
    /// conta e o apelido ficam, e o apelido continua reservado. Confirmada pelo apelido do jogador.
    /// </summary>
    [HttpPost("{playerId}/excluir")]
    [ConflitoDeConcorrenciaDoEncontro]
    public Task<IActionResult> Excluir(Guid playerId, ExcluirContaRequest request) =>
        MudarEstadoAsync(playerId, AcaoDeConta.Excluir, request.Nickname);

    private async Task<IActionResult> MudarEstadoAsync(Guid playerId, AcaoDeConta acao, string? confirmacao = null)
    {
        var player = await db.Users.FindAsync(playerId);
        if (player is null || !ContaDeJogador.PodeGerir(CurrentGmId(), player.InvitedByGmId))
            return NotFound();
        if (ContaDeJogador.Aplicar(player.Estado, acao) is not { } novo)
            return Conflict();
        if (acao == AcaoDeConta.Excluir && confirmacao != player.Nickname)
            return BadRequest("O apelido não confere.");

        var saiu = novo != EstadoDaConta.Ativa;
        await using (var transacao = await db.Database.BeginTransactionAsync())
        {
            if (saiu)
            {
                var campanhas = await db.CampaignMembers
                    .Where(m => m.UserId == playerId && db.Campaigns.Any(c => c.Id == m.CampaignId && c.GmId == player.InvitedByGmId))
                    .Select(m => m.CampaignId).ToListAsync();
                await saida.SairAsync(playerId, campanhas);
                var agora = DateTime.UtcNow;
                foreach (var token in await db.RefreshTokens.Where(t => t.UserId == playerId && t.RevokedAt == null).ToListAsync())
                    token.RevokedAt = agora;
            }
            if (novo == EstadoDaConta.Excluida)
                ApagarDadosDeLogin(player);
            player.Estado = novo;
            await db.SaveChangesAsync();
            await transacao.CommitAsync();
        }
        if (saiu)
            await saida.NotificarAsync();
        return NoContent();
    }

    // O nome de usuário do Identity tem de continuar único e não nulo: vem do Id. O apelido fica (e segue reservado).
    private static void ApagarDadosDeLogin(ApplicationUser player)
    {
        player.Email = null;
        player.NormalizedEmail = null;
        player.EmailConfirmed = false;
        player.PasswordHash = null;
        player.UserName = $"excluida-{player.Id:N}";
        player.NormalizedUserName = player.UserName.ToUpperInvariant();
        player.SecurityStamp = Guid.NewGuid().ToString();
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
