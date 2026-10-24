using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api;

/// Consolidates a 1.5.0 migration history into the 2.0.0 baseline, applies pending migrations and
/// runs every seeder/backfill, in order. The one place that knows the sequence: `--migrate`
/// (Production, Técnico R0009), Development startup and the integration-test template database all
/// call it.
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await HistoricoDeMigracoes150.ConsolidarAsync(db, logger);
        await db.Database.MigrateAsync();

        var traitSeed = await TraitSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Caracteristicas.md"));
        logger.LogInformation("Trait seed: {InsertedCount} new row(s) inserted, {UpdatedCount} existing row(s) updated", traitSeed.Inserted, traitSeed.Updated);

        var historicoSeed = await HistoricoSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Historico.md"));
        logger.LogInformation("Historico seed: {InsertedCount} new row(s) inserted", historicoSeed.Inserted);

        await EfeitoSeeder.SeedAsync(db);
        logger.LogInformation("Efeito seed: catalog synced");

        var livroEfeitos = await scope.ServiceProvider.GetRequiredService<LivroDeRegrasEfeitosSync>().SincronizarFaltantesAsync();
        logger.LogInformation("Livro de Regras (Graus & Círculos): {InsertedCount} Efeito block(s) added", livroEfeitos);

        var catalogSeed = await DefaultCatalogSeeder.SeedMissingAsync(db);
        logger.LogInformation("Default catalog seed: {SeededGmCount} GM(s) with an empty catalog seeded", catalogSeed);

        var kitSeed = await EquipmentKitSeeder.SeedAsync(db);
        logger.LogInformation("EquipmentKit seed: {InsertedCount} new kit(s) inserted", kitSeed);

        var fixedItemBackfill = await EquipmentKitFixedItemBackfill.RunAsync(db);
        logger.LogInformation("EquipmentKit fixed-item backfill: {CreatedCount} fixed item(s) created", fixedItemBackfill);

        var durabilidadeSeed = await DurabilidadePorRankSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Durabilidade por Rank.md"));
        logger.LogInformation("DurabilidadePorRank seed: {InsertedCount} new row(s) inserted", durabilidadeSeed);

        var afinidadesSeed = await TabelaDeAfinidadesSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Afinidades.md"));
        logger.LogInformation("TabelaDeAfinidades seed: {InsertedCount} new row(s) inserted", afinidadesSeed);

        var rules = scope.ServiceProvider.GetRequiredService<IRulesDataProvider>();
        var niveisSeed = await TabelaDeNiveisSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Níveis.md"), rules.XpPorNivel, rules.EapPorNivel);
        logger.LogInformation("TabelaDeNiveis seed: {InsertedCount} new row(s)/column(s) inserted", niveisSeed);
    }
}
