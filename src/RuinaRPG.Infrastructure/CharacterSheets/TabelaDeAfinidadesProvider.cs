using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>Tabela de Afinidades carregada uma vez por escopo, para os cálculos de Sub-Atributos.</summary>
public class TabelaDeAfinidadesProvider(RuinaRpgDbContext db)
{
    private IReadOnlyList<LinhaDaTabelaDeAfinidades>? _linhas;

    public async Task<IReadOnlyList<LinhaDaTabelaDeAfinidades>> LinhasAsync() =>
        _linhas ??= await db.TabelaDeAfinidades.AsNoTracking()
            .Select(l => new LinhaDaTabelaDeAfinidades(l.Afinidade, l.Eficiencia, l.Dano)).ToListAsync();
}
