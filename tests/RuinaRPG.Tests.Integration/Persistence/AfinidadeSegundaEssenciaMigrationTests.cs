using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class AfinidadeSegundaEssenciaMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public AfinidadeSegundaEssenciaMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_converts_legacy_rows_to_the_two_essencias()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var anterior = db.Database.GetMigrations().Single(m => m.EndsWith("_AddSheetHistoria"));
        await migrator.MigrateAsync(anterior);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@essencias.com", Email = "gm@essencias.com", Nickname = "EssGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "p@essencias.com", Email = "p@essencias.com", Nickname = "EssPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        // Fichas também via SQL cru: a entidade ganha colunas em migrations posteriores (ex:
        // AfinidadeAdicional) que ainda não existem neste ponto do schema.
        var sheet = new { Id = Guid.NewGuid() };
        await InsertAtCurrentSchemaAsync(db, "CharacterSheets", new() { ["Id"] = sheet.Id, ["CampaignId"] = campaign.Id, ["OwnerId"] = player.Id });
        var npc = new { Id = Guid.NewGuid() };
        await InsertAtCurrentSchemaAsync(db, "NpcSheets", new() { ["Id"] = npc.Id, ["GmId"] = gm.Id });

        // Legacy rows via raw SQL — the entity already has the new columns, which don't exist yet at this migration.
        var rows = new (Guid Id, int? Elemento, int? SubElemento, string? Caminho)[]
        {
            (Guid.NewGuid(), 0, 0, null),                  // Ar + Gelo            → Essência 2 = Agua
            (Guid.NewGuid(), 1, 0, null),                  // Agua + Gelo          → Essência 2 = Ar (symmetric pair)
            (Guid.NewGuid(), 2, 9, "Vida"),                // Fogo + Curar + Vida  → Essência 2 = Vida
            (Guid.NewGuid(), 2, null, "Vida"),             // Fogo + Caminho Vida  → Essência 2 = Vida, Sub = Curar
            (Guid.NewGuid(), 0, 9, null),                  // Ar + Curar (bad)     → untouched, Sub kept
            (Guid.NewGuid(), 3, null, "Caminho da Fênix"), // free text            → untouched
            (Guid.NewGuid(), 0, 4, null),                  // Ar + legacy Alma     → untouched, Sub kept
            (Guid.NewGuid(), 0, null, "Vida"),             // Ar + Vida (no cross) → untouched
        };
        foreach (var r in rows)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"CharacterAffinities\" (\"Id\", \"CharacterSheetId\", \"Elemento\", \"SubElemento\", \"CaminhoNome\") VALUES ({r.Id}, {sheet.Id}, {r.Elemento}, {r.SubElemento}, {r.Caminho})");
        var npcRow = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"NpcAffinities\" (\"Id\", \"NpcSheetId\", \"Elemento\", \"SubElemento\", \"CaminhoNome\") VALUES ({npcRow}, {npc.Id}, {3}, {(int?)null}, {"Mundano"})");

        await migrator.MigrateAsync();

        var after = await db.CharacterAffinities.AsNoTracking().ToDictionaryAsync(a => a.Id);
        after[rows[0].Id].SegundaEssencia.Should().Be(EssenciaBasica.Agua);
        after[rows[1].Id].SegundaEssencia.Should().Be(EssenciaBasica.Ar);
        after[rows[2].Id].SegundaEssencia.Should().Be(EssenciaBasica.Vida);
        after[rows[3].Id].SegundaEssencia.Should().Be(EssenciaBasica.Vida);
        after[rows[3].Id].SubElemento.Should().Be(SubElemento.Curar);
        after[rows[4].Id].SegundaEssencia.Should().BeNull();
        after[rows[4].Id].SubElemento.Should().Be(SubElemento.Curar);
        after[rows[5].Id].SegundaEssencia.Should().BeNull();
        after[rows[5].Id].SubElemento.Should().BeNull();
        after[rows[6].Id].SegundaEssencia.Should().BeNull();
        after[rows[6].Id].SubElemento.Should().Be(SubElemento.Alma);
        after[rows[7].Id].SegundaEssencia.Should().BeNull();
        after[rows[7].Id].SubElemento.Should().BeNull();

        var npcAfter = await db.NpcAffinities.AsNoTracking().SingleAsync(a => a.Id == npcRow);
        npcAfter.SegundaEssencia.Should().Be(EssenciaBasica.Mundano);
        npcAfter.SubElemento.Should().Be(SubElemento.Invocacao);
    }

    // INSERT com os valores dados, preenchendo cada coluna NOT NULL sem default com o "zero" do seu tipo.
    private static async Task InsertAtCurrentSchemaAsync(RuinaRpgDbContext db, string table, Dictionary<string, object> values)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using (var query = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = @t AND is_nullable = 'NO' AND column_default IS NULL", connection))
        {
            query.Parameters.AddWithValue("t", table);
            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var column = reader.GetString(0);
                if (values.ContainsKey(column))
                    continue;
                values[column] = reader.GetString(1) switch
                {
                    "boolean" => false,
                    "text" or "character varying" => "",
                    "numeric" => 0m,
                    "timestamp with time zone" => DateTime.UtcNow,
                    _ => 0,
                };
            }
        }

        var columns = values.Keys.ToList();
        await using var insert = new NpgsqlCommand(
            $"INSERT INTO \"{table}\" ({string.Join(", ", columns.Select(c => $"\"{c}\""))}) VALUES ({string.Join(", ", columns.Select((_, i) => $"@p{i}"))})", connection);
        for (var i = 0; i < columns.Count; i++)
            insert.Parameters.AddWithValue($"p{i}", values[columns[i]]);
        await insert.ExecuteNonQueryAsync();
    }
}
