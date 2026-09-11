using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration;

public class RulesAuditorCliTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RulesAuditorCliTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }

    [Fact]
    public async Task SetRulesAuditorAsync_grants_a_GM_found_by_email()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "cligrant@teste.com", Email = "cligrant@teste.com", NormalizedEmail = "CLIGRANT@TESTE.COM", Nickname = "CliGrantGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "cligrant@teste.com", grant: true);

        result.Should().BeNull(); // null = success, no error message
        (await db.Users.SingleAsync(u => u.Id == gm.Id)).IsRulesAuditor.Should().BeTrue();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_matches_email_case_insensitively()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clicase@teste.com", Email = "clicase@teste.com", NormalizedEmail = "CLICASE@TESTE.COM", Nickname = "CliCaseGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "CLICASE@TESTE.COM", grant: true);

        result.Should().BeNull();
        (await db.Users.SingleAsync(u => u.Id == gm.Id)).IsRulesAuditor.Should().BeTrue();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_revokes_an_already_granted_GM()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clirevoke@teste.com", Email = "clirevoke@teste.com", NormalizedEmail = "CLIREVOKE@TESTE.COM", Nickname = "CliRevokeGm", Role = UserRole.GM, IsRulesAuditor = true };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "clirevoke@teste.com", grant: false);

        result.Should().BeNull();
        (await db.Users.SingleAsync(u => u.Id == gm.Id)).IsRulesAuditor.Should().BeFalse();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_returns_an_error_for_an_unknown_email()
    {
        await using var db = await NewDbAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "naoexiste@teste.com", grant: true);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_refuses_to_grant_a_Jogador()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "cligmowner@teste.com", Email = "cligmowner@teste.com", NormalizedEmail = "CLIGMOWNER@TESTE.COM", Nickname = "CliGmOwner", Role = UserRole.GM };
        db.Users.Add(gm);
        var jogador = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clijogador@teste.com", Email = "clijogador@teste.com", NormalizedEmail = "CLIJOGADOR@TESTE.COM", Nickname = "CliJogador", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.Add(jogador);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "clijogador@teste.com", grant: true);

        result.Should().NotBeNull();
        (await db.Users.SingleAsync(u => u.Id == jogador.Id)).IsRulesAuditor.Should().BeFalse();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_allows_revoking_a_Jogador_even_though_granting_one_is_refused()
    {
        // Revoke is always safe to allow unconditionally — it only ever turns the flag off,
        // never grants access to someone who shouldn't have it.
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clijgmowner2@teste.com", Email = "clijgmowner2@teste.com", NormalizedEmail = "CLIJGMOWNER2@TESTE.COM", Nickname = "CliGmOwner2", Role = UserRole.GM };
        db.Users.Add(gm);
        var jogador = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clijogador2@teste.com", Email = "clijogador2@teste.com", NormalizedEmail = "CLIJOGADOR2@TESTE.COM", Nickname = "CliJogador2", Role = UserRole.Jogador, InvitedByGmId = gm.Id, IsRulesAuditor = true };
        db.Users.Add(jogador);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "clijogador2@teste.com", grant: false);

        result.Should().BeNull();
        (await db.Users.SingleAsync(u => u.Id == jogador.Id)).IsRulesAuditor.Should().BeFalse();
    }
}
