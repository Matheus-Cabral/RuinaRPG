using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>
/// Como um valor de coluna de ficha aparece no log quando a coluna é uma referência: pelo nome do que ela aponta
/// (item do Catálogo, Perícia, Característica, Histórico, Equipagem). Vale por um salvamento: cada referência
/// distinta custa uma consulta, sem rastrear nada no contexto.
/// </summary>
internal sealed class SheetLogNames(RuinaRpgDbContext db)
{
    private const string Removido = "(removido)";
    private readonly Dictionary<(string Propriedade, object Id), string> _cache = new();

    /// <summary>O valor como o log o mostra: o nome referenciado quando a coluna é uma referência, senão SheetLogValues.Render.</summary>
    public async Task<string> RenderAsync(string propriedade, object? valor, CancellationToken ct)
    {
        if (valor is null || !EhReferencia(propriedade))
            return SheetLogValues.Render(valor);
        if (propriedade == "ImageId")
            return "imagem";
        if (!_cache.TryGetValue((propriedade, valor), out var nome))
            _cache[(propriedade, valor)] = nome = await BuscarAsync(propriedade, valor, ct) ?? Removido;
        return nome;
    }

    private static bool EhReferencia(string propriedade) => propriedade is "ImageId" or "ItemId" or "ArtifactItemId" or "PericiaId"
        or "TraitId" or "CreatureExclusiveTraitId" or "HistoricoId" or "EquipmentKitId";

    private Task<string?> BuscarAsync(string propriedade, object id, CancellationToken ct) => (propriedade, id) switch
    {
        ("ItemId" or "ArtifactItemId", Guid g) => db.Items.AsNoTracking().Where(i => i.Id == g).Select(i => i.Nome).FirstOrDefaultAsync(ct),
        ("PericiaId", int p) => db.Pericias.AsNoTracking().Where(x => x.Id == p).Select(x => x.Nome).FirstOrDefaultAsync(ct),
        ("TraitId", Guid g) => db.Traits.AsNoTracking().Where(t => t.Id == g).Select(t => t.Nome).FirstOrDefaultAsync(ct),
        ("CreatureExclusiveTraitId", Guid g) => db.CreatureExclusiveTraits.AsNoTracking().Where(t => t.Id == g).Select(t => t.Nome).FirstOrDefaultAsync(ct),
        ("HistoricoId", Guid g) => db.Historicos.AsNoTracking().Where(h => h.Id == g).Select(h => h.Nome).FirstOrDefaultAsync(ct),
        ("EquipmentKitId", Guid g) => db.EquipmentKits.AsNoTracking().Where(k => k.Id == g).Select(k => k.Nome).FirstOrDefaultAsync(ct),
        _ => Task.FromResult<string?>(null),
    };
}
