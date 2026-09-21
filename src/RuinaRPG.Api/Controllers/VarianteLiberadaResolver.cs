using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// A Variante solar de Alóra (Variante.AloraSolar) só existe nas fichas depois que o GM dá um nome
/// a ela na página de Habilidades Raciais (Requisitos - Habilidades Raciais R0005). Compartilhado por
/// CharacterSheetsController e NpcSheetsController.
/// </summary>
internal static class VarianteLiberadaResolver
{
    public const Variante Personalizada = Variante.AloraSolar;

    public static async Task<List<VarianteLiberadaResponse>> ListAsync(RuinaRpgDbContext db, Guid gmId)
    {
        var nome = await NomeAsync(db, gmId);
        return nome is null ? [] : [new VarianteLiberadaResponse(Personalizada.ToString(), nome)];
    }

    /// <summary>
    /// Só uma escolha NOVA é bloqueada: uma ficha que já usa a variante continua válida mesmo se o
    /// GM apagar o nome depois (mesma regra "não invalida dado antigo" de Afinidades).
    /// </summary>
    public static async Task<bool> EscolhaBloqueadaAsync(RuinaRpgDbContext db, Guid gmId, Variante? nova, Variante? atual) =>
        nova == Personalizada && nova != atual && await NomeAsync(db, gmId) is null;

    private static async Task<string?> NomeAsync(RuinaRpgDbContext db, Guid gmId)
    {
        var nome = await db.RacialAbilityOverrides
            .Where(o => o.GmId == gmId && o.Variante == Personalizada)
            .Select(o => o.NomeDaVariante)
            .FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(nome) ? null : nome;
    }
}
