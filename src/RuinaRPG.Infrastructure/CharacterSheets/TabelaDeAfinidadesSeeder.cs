using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>
/// Semeia a Tabela de Afinidades só enquanto ela está vazia. Não insere "as linhas que faltam", como a
/// Durabilidade por Rank: aqui o Auditor pode excluir linhas, e isso as ressuscitaria.
/// </summary>
public static class TabelaDeAfinidadesSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db, string markdown)
    {
        if (await db.TabelaDeAfinidades.AnyAsync())
            return 0;

        var linhas = TabelaDeAfinidadesParser.Parse(markdown);
        foreach (var linha in linhas)
            db.TabelaDeAfinidades.Add(new AfinidadeElementalLinha { Id = Guid.NewGuid(), Afinidade = linha.Afinidade, Eficiencia = linha.Eficiencia, Dano = linha.Dano });
        await db.SaveChangesAsync();
        return linhas.Count;
    }
}
