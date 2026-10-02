using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public static class ArcaTabelaExtensions
{
    /// <summary>O dado da Tabela de Arcas do GM; D20 quando ele nunca escolheu.</summary>
    public static async Task<int> DadoDeArcaDoGmAsync(this RuinaRpgDbContext db, Guid gmId) =>
        await db.ArcaTabelas.Where(t => t.GmId == gmId).Select(t => (int?)t.Dado).FirstOrDefaultAsync() ?? DadoDeArca.Padrao;
}
