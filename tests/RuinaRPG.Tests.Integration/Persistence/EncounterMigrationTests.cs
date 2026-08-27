using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class EncounterMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EncounterMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Encounters_tables_and_round_trips_an_independent_participant_with_a_condition()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddEncounters"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@encountertest.com", Email = "gm@encountertest.com", Nickname = "EncounterTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "A Ruína Aguarda", Descricao = "Uma campanha de teste." };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var encounter = new Encounter { Id = Guid.NewGuid(), CampaignId = campaign.Id, Nome = "Emboscada na Estrada", CurrentRound = 1, CurrentParticipantIndex = 0 };
        db.Encounters.Add(encounter);
        await db.SaveChangesAsync();

        var participant = new EncounterParticipant
        {
            Id = Guid.NewGuid(),
            EncounterId = encounter.Id,
            SourceCharacterSheetId = null,
            SourceNpcSheetId = null,
            SourceCreatureSheetId = null,
            Nome = "Lobo Selvagem",
            Iniciativa = 15,
            PVAtual = 20,
            PFAtual = 5,
            PAAtual = 2,
            AcoesRestantes = 1,
        };
        db.EncounterParticipants.Add(participant);
        await db.SaveChangesAsync();

        var condition = new EncounterParticipantCondition { Id = Guid.NewGuid(), EncounterParticipantId = participant.Id, Texto = "Sangrando" };
        db.EncounterParticipantConditions.Add(condition);
        await db.SaveChangesAsync();

        var reloadedEncounter = await db.Encounters.SingleAsync();
        reloadedEncounter.Nome.Should().Be("Emboscada na Estrada");
        reloadedEncounter.CampaignId.Should().Be(campaign.Id);
        reloadedEncounter.CurrentRound.Should().Be(1);
        reloadedEncounter.CurrentParticipantIndex.Should().Be(0);

        var reloadedParticipant = await db.EncounterParticipants.SingleAsync();
        reloadedParticipant.EncounterId.Should().Be(encounter.Id);
        reloadedParticipant.SourceCharacterSheetId.Should().BeNull();
        reloadedParticipant.SourceNpcSheetId.Should().BeNull();
        reloadedParticipant.SourceCreatureSheetId.Should().BeNull();
        reloadedParticipant.Nome.Should().Be("Lobo Selvagem");
        reloadedParticipant.Iniciativa.Should().Be(15);
        reloadedParticipant.PVAtual.Should().Be(20);
        reloadedParticipant.PFAtual.Should().Be(5);
        reloadedParticipant.PAAtual.Should().Be(2);
        reloadedParticipant.AcoesRestantes.Should().Be(1);

        var reloadedCondition = await db.EncounterParticipantConditions.SingleAsync();
        reloadedCondition.EncounterParticipantId.Should().Be(participant.Id);
        reloadedCondition.Texto.Should().Be("Sangrando");
    }
}
