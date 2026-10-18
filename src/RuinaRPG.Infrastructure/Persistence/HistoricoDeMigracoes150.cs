using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace RuinaRPG.Infrastructure.Persistence;

/// As 84 migrations da 1.5.0 foram consolidadas na baseline da 2.0.0, que cria exatamente o mesmo schema
/// (provado por tools/migrations/verificar-baseline.sh). Um banco que está exatamente na 1.5.0 tem o
/// histórico trocado pela baseline, sem tocar em schema nem dados; qualquer outro estado antigo é recusado.
/// Roda dentro de --migrate (DatabaseInitializer), antes do MigrateAsync.
public static class HistoricoDeMigracoes150
{
    public const string Baseline = "20261017120000_Baseline_2_0_0";

    public static readonly IReadOnlyList<string> Ids =
    [
        "20260815020559_InitialCreate",
        "20260815103216_AddNormalizedNicknameUniqueIndex",
        "20260816033827_AddInviteCodes",
        "20260817123015_AddImages",
        "20260817125005_AddItems",
        "20260817174147_AddSpellAbilityBank",
        "20260821214356_AddCampaigns",
        "20260821215316_AddCampaignMembers",
        "20260821220851_AddDiary",
        "20260822020827_AddCharacterSheets",
        "20260822084307_AddCharacterAttributesAndSkills",
        "20260822091219_AddCharacterAffinities",
        "20260822092330_AddCharacterArsenal",
        "20260822134358_AddCharacterSpellAbilities",
        "20260822140810_AddCharacterRunesAndMasteries",
        "20260822214510_AddCharacterInventoryAndArtifacts",
        "20260822220342_AddTraits",
        "20260822220856_AddCharacterAffectionsAndTraits",
        "20260822224256_AddDiaryEntryCharacterSheetForeignKey",
        "20260823032923_AddNpcSheets",
        "20260823034231_AddNpcChildTables",
        "20260823172112_AddCreatureSheets",
        "20260823173404_AddCreatureChildTables",
        "20260823183411_DropCreatureSheetPropriedade",
        "20260827130556_AddCampaignAttachments",
        "20260827181159_AddEncounters",
        "20260829135336_AddSkillAtributoEscolhido",
        "20260829234234_EncounterCurrentParticipantId",
        "20260902223521_AddCampaignImage",
        "20260903013750_AddCharacterSheetPontosDeIgnicaoBonusManual",
        "20260903022715_AddCharacterSheetPontosDePericiaBonusCritico",
        "20260903161021_AddAffinityElementoAndSubElementoValor",
        "20260903165921_MakeAffinityFieldsNullable",
        "20260908145837_AddItemGeralCapacidadeExtra",
        "20260908205847_AddTraitSpecificationFields",
        "20260909154649_AddRacialAbilityOverrideAndArcaEntry",
        "20260911160057_AddRacialTraits",
        "20260911172033_AddIsRulesAuditor",
        "20260911174405_AddTraitAuditFields",
        "20260911185514_AddRulebookDocumentOverrides",
        "20260914162951_AddCreatureExclusiveTraits",
        "20260914172924_AddCreatureTraitExactlyOneCatalogCheck",
        "20260914204155_AddEfeitos",
        "20260918023137_AddLastSeenAppVersion",
        "20260918140611_AddMustChangePassword",
        "20260921142649_AddNomeDaVarianteToRacialAbilityOverride",
        "20260921160223_AddRuneBank",
        "20260921173257_AddRuneImage",
        "20260922135852_AddCharacterSheetAndNpcSheetEstrelaSina",
        "20260922160637_AddHistoricosAndSheetHistoricoId",
        "20260923020827_AddEquipmentKitsAndSheetEquipmentKitId",
        "20260923144713_AddSubcategoriaOptionsAndArmorSlot",
        "20260926011207_AddSheetHistoria",
        "20260926031550_AddAfinidadeSegundaEssencia",
        "20260926042942_DropAfinidadeCaminhoNome",
        "20260927173324_AddAfinidadeAdicional",
        "20260927194425_AddSpellAbilityBankDeCriatura",
        "20260928194344_AddHabilidadesPassivas",
        "20260929005951_AddDurabilidadePorRank",
        "20260929012531_RemoveDurabilidadeMaximaDosItens",
        "20260929082529_RulebookOverrideAutorOpcional",
        "20260929161238_AddArcaEvolucoes",
        "20261001100828_AddPericias",
        "20261001101601_PericiasComoTabela",
        "20261001113341_AddTabelaDeNiveis",
        "20261001183018_AddTipoDeRuna",
        "20261002005956_AddDadoDaTabelaDeArcas",
        "20261002025413_AddTabelaDeNiveisConfig",
        "20261002032113_AddBonusDeCargaDaCampanha",
        "20261002034641_AddImagensDaHistoria",
        "20261005172005_AddDisciplinaDeRuna",
        "20261005174723_AddRequisitosDeEquipamento",
        "20261005175651_ConverteRequisitosLegadosDeEquipamento",
        "20261005193357_AddEquipmentKitFixedItems",
        "20261009134906_AddTabelaDeAfinidades",
        "20261009180612_AddReadAtToDiaryEntryRecipients",
        "20261013152921_AddCriadaNaFichaToSheetSpellAbilities",
        "20261013171712_AddSheetLogEntries",
        "20261013194126_AddEncounterPhasesAndLog",
        "20261013201021_AddOrdemToEncounterLogEntries",
        "20261014004619_AddEncounterRowVersion",
        "20261014012056_AddCondicoes",
        "20261014020941_AddEncounterParticipantCondicoes",
        "20261014100935_AddAutomaticoToSheetLogEntries",
    ];

    public static async Task ConsolidarAsync(RuinaRpgDbContext db, ILogger logger)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var transaction = await connection.BeginTransactionAsync();

            await using (var existe = new NpgsqlCommand("SELECT to_regclass('\"__EFMigrationsHistory\"') IS NOT NULL", connection, transaction))
            {
                if (!(bool)(await existe.ExecuteScalarAsync())!)
                    return;
            }

            await using (var trava = new NpgsqlCommand("LOCK TABLE \"__EFMigrationsHistory\" IN ACCESS EXCLUSIVE MODE", connection, transaction))
                await trava.ExecuteNonQueryAsync();

            var aplicadas = new List<string>();
            await using (var leitura = new NpgsqlCommand("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\"", connection, transaction))
            await using (var reader = await leitura.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    aplicadas.Add(reader.GetString(0));
            }

            if (aplicadas.Count == 0)
                return;

            if (aplicadas.Contains(Baseline))
            {
                if (aplicadas.Any(Ids.Contains))
                    throw Recusa(aplicadas);
                return;
            }

            if (!aplicadas.ToHashSet().SetEquals(Ids))
                throw Recusa(aplicadas);

            await using (var apaga = new NpgsqlCommand("DELETE FROM \"__EFMigrationsHistory\"", connection, transaction))
                await apaga.ExecuteNonQueryAsync();
            await using (var insere = new NpgsqlCommand("INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@id, @versao)", connection, transaction))
            {
                insere.Parameters.AddWithValue("id", Baseline);
                insere.Parameters.AddWithValue("versao", ProductInfo.GetVersion());
                await insere.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();

            logger.LogInformation("Histórico de migrations consolidado: as {Quantidade} migrations da 1.5.0 viraram a baseline {Baseline}", Ids.Count, Baseline);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static InvalidOperationException Recusa(List<string> aplicadas)
    {
        var faltando = Ids.Except(aplicadas).Order(StringComparer.Ordinal).ToList();
        var inesperadas = aplicadas.Except(Ids).Order(StringComparer.Ordinal).ToList();
        return new(
            $"O histórico de migrations não está na 1.5.0 nem na 2.0.0: esperado exatamente as {Ids.Count} migrations da 1.5.0 " +
            $"(última {Ids[^1]}) ou a baseline {Baseline}; encontradas {aplicadas.Count}, a mais recente {aplicadas.Max(StringComparer.Ordinal)}. " +
            $"Faltando da 1.5.0: {Lista(faltando)}. Inesperadas: {Lista(inesperadas)}. " +
            "Um banco anterior à 1.5.0 precisa ser atualizado com a 1.5.0 antes. " +
            "Nada foi alterado.");
    }

    private static string Lista(List<string> ids) => ids.Count == 0
        ? "nenhuma"
        : string.Join(", ", ids.Take(10)) + (ids.Count > 10 ? $" (+{ids.Count - 10})" : "");
}
