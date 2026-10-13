using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class EncounterPhasesMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EncounterPhasesMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_converts_existing_encounters_to_Preparacao_or_EmAndamento()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var anterior = db.Database.GetMigrations().Single(m => m.EndsWith("_AddSheetLogEntries"));
        await migrator.MigrateAsync(anterior);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@fases.com", Email = "gm@fases.com", Nickname = "FasesGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        var campaignId = Guid.NewGuid();
        var parado = Guid.NewGuid();
        var emCurso = Guid.NewGuid();
        var participante = Guid.NewGuid();
        // Raw SQL: the entities already carry columns this point of the schema doesn't have yet.
        await SchemaInsert.AtCurrentSchemaAsync(db, "Campaigns", new() { ["Id"] = campaignId, ["GmId"] = gm.Id, ["Nome"] = "Teste", ["Descricao"] = "" });
        await SchemaInsert.AtCurrentSchemaAsync(db, "Encounters", new() { ["Id"] = parado, ["CampaignId"] = campaignId, ["CurrentRound"] = 1 });
        await SchemaInsert.AtCurrentSchemaAsync(db, "Encounters", new() { ["Id"] = emCurso, ["CampaignId"] = campaignId, ["CurrentRound"] = 3 });
        await SchemaInsert.AtCurrentSchemaAsync(db, "EncounterParticipants", new() { ["Id"] = participante, ["EncounterId"] = emCurso, ["Nome"] = "Goblin" });
        await db.Database.ExecuteSqlAsync($"UPDATE \"Encounters\" SET \"CurrentParticipantId\" = {participante} WHERE \"Id\" = {emCurso}");

        await migrator.MigrateAsync();

        var antes = await db.Encounters.AsNoTracking().SingleAsync(e => e.Id == parado);
        antes.Status.Should().Be(EncounterStatus.Preparacao);
        antes.CurrentRound.Should().Be(0);
        antes.FinalizadoEm.Should().BeNull();
        var durante = await db.Encounters.AsNoTracking().SingleAsync(e => e.Id == emCurso);
        durante.Status.Should().Be(EncounterStatus.EmAndamento);
        durante.CurrentRound.Should().Be(3);
        durante.FinalizadoEm.Should().BeNull();
    }
}
