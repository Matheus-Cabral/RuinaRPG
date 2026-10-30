using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules.ReferenceData;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Afinidades;

/// <summary>
/// Semeia o total de afinidade por Círculo a partir da coluna <i>Afinidade (Valores Absolutos)</i> da Tabela de
/// Círculo e Grau por EAP, só enquanto a tabela está vazia: depois o Auditor é o dono (Auditoria de Regras R0018),
/// e editar o Markdown não a altera mais.
/// </summary>
public static class TotalDeAfinidadePorCirculoSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db, IReadOnlyList<CirculoGrauPorEap> tabela)
    {
        if (await db.TotaisDeAfinidadePorCirculo.AnyAsync())
            return 0;

        db.TotaisDeAfinidadePorCirculo.AddRange(tabela.Select(r => new TotalDeAfinidadePorCirculo { Circulo = r.CirculoOuGrau, Total = r.AfinidadeAbsoluta }));
        await db.SaveChangesAsync();
        return tabela.Count;
    }
}
