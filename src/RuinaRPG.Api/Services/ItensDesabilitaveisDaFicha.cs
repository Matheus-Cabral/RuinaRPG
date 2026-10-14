using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Services;

/// <summary>
/// As linhas da ficha de um participante que uma Condição pode desabilitar, por categoria. Cada tipo de ficha
/// tem as suas tabelas; um participante cuja ficha de origem sumiu devolve as oito categorias vazias.
/// </summary>
public class ItensDesabilitaveisDaFicha(RuinaRpgDbContext db)
{
    public async Task<Dictionary<CategoriaDesabilitavel, List<(Guid Id, string Nome)>>> ListarAsync(EncounterParticipant participante)
    {
        var porCategoria = Enum.GetValues<CategoriaDesabilitavel>().ToDictionary(c => c, _ => new List<(Guid Id, string Nome)>());

        if (participante.SourceCharacterSheetId is { } personagemId)
        {
            porCategoria[CategoriaDesabilitavel.Arma] = await Nomear(db.CharacterWeapons.Where(w => w.CharacterSheetId == personagemId).Select(w => new LinhaDeItem(w.Id, w.ItemId)));
            porCategoria[CategoriaDesabilitavel.Armadura] = await Nomear(db.CharacterArmorSlots.Where(s => s.CharacterSheetId == personagemId && s.ItemId != null).Select(s => new LinhaDeItem(s.Id, s.ItemId!.Value)));
            porCategoria[CategoriaDesabilitavel.Escudo] = await Nomear(db.CharacterShields.Where(s => s.CharacterSheetId == personagemId).Select(s => new LinhaDeItem(s.Id, s.ItemId)));
            porCategoria[CategoriaDesabilitavel.Artefato] = await Nomear(db.CharacterArtifacts.Where(a => a.CharacterSheetId == personagemId).Select(a => new LinhaDeItem(a.Id, a.ArtifactItemId)));
            PorTipo(porCategoria, await db.CharacterSpellAbilities.Where(a => a.CharacterSheetId == personagemId).Select(a => new { a.Id, a.Nome, a.Tipo }).ToListAsync(), a => (a.Id, a.Nome, a.Tipo));
            porCategoria[CategoriaDesabilitavel.Runa] = (await db.CharacterRunes.Where(r => r.CharacterSheetId == personagemId).Select(r => new { r.Id, r.Nome }).ToListAsync()).Select(r => (r.Id, r.Nome)).ToList();
        }
        else if (participante.SourceNpcSheetId is { } npcId)
        {
            porCategoria[CategoriaDesabilitavel.Arma] = await Nomear(db.NpcWeapons.Where(w => w.NpcSheetId == npcId).Select(w => new LinhaDeItem(w.Id, w.ItemId)));
            porCategoria[CategoriaDesabilitavel.Armadura] = await Nomear(db.NpcArmorSlots.Where(s => s.NpcSheetId == npcId && s.ItemId != null).Select(s => new LinhaDeItem(s.Id, s.ItemId!.Value)));
            porCategoria[CategoriaDesabilitavel.Escudo] = await Nomear(db.NpcShields.Where(s => s.NpcSheetId == npcId).Select(s => new LinhaDeItem(s.Id, s.ItemId)));
            porCategoria[CategoriaDesabilitavel.Artefato] = await Nomear(db.NpcArtifacts.Where(a => a.NpcSheetId == npcId).Select(a => new LinhaDeItem(a.Id, a.ArtifactItemId)));
            PorTipo(porCategoria, await db.NpcSpellAbilities.Where(a => a.NpcSheetId == npcId).Select(a => new { a.Id, a.Nome, a.Tipo }).ToListAsync(), a => (a.Id, a.Nome, a.Tipo));
            porCategoria[CategoriaDesabilitavel.Runa] = (await db.NpcRunes.Where(r => r.NpcSheetId == npcId).Select(r => new { r.Id, r.Nome }).ToListAsync()).Select(r => (r.Id, r.Nome)).ToList();
        }
        else if (participante.SourceCreatureSheetId is { } criaturaId)
        {
            // Um ataque natural não tem Item: usa o nome manual. A Criatura não tem Runas.
            var armas = await db.CreatureWeapons.Where(w => w.CreatureSheetId == criaturaId)
                .Select(w => new { w.Id, w.ItemId, w.ManualNome }).ToListAsync();
            var nomesDosItens = await NomesDosItensAsync(armas.Where(w => w.ItemId != null).Select(w => w.ItemId!.Value));
            porCategoria[CategoriaDesabilitavel.Arma] = armas.Select(w => (w.Id, w.ItemId is { } i ? nomesDosItens.GetValueOrDefault(i, "") : w.ManualNome ?? "")).ToList();
            porCategoria[CategoriaDesabilitavel.Armadura] = await Nomear(db.CreatureArmorSlots.Where(s => s.CreatureSheetId == criaturaId && s.ItemId != null).Select(s => new LinhaDeItem(s.Id, s.ItemId!.Value)));
            porCategoria[CategoriaDesabilitavel.Escudo] = await Nomear(db.CreatureShields.Where(s => s.CreatureSheetId == criaturaId).Select(s => new LinhaDeItem(s.Id, s.ItemId)));
            porCategoria[CategoriaDesabilitavel.Artefato] = await Nomear(db.CreatureArtifacts.Where(a => a.CreatureSheetId == criaturaId).Select(a => new LinhaDeItem(a.Id, a.ArtifactItemId)));
            PorTipo(porCategoria, await db.CreatureSpellAbilities.Where(a => a.CreatureSheetId == criaturaId).Select(a => new { a.Id, a.Nome, a.Tipo }).ToListAsync(), a => (a.Id, a.Nome, a.Tipo));
        }

        return porCategoria;
    }

    // Racial nunca entra: não é Magia, Habilidade nem Passiva.
    private static void PorTipo<T>(Dictionary<CategoriaDesabilitavel, List<(Guid Id, string Nome)>> porCategoria, List<T> linhas, Func<T, (Guid Id, string Nome, SpellAbilityTipo Tipo)> ler)
    {
        foreach (var linha in linhas.Select(ler))
        {
            var categoria = linha.Tipo switch
            {
                SpellAbilityTipo.Magia => CategoriaDesabilitavel.Magia,
                SpellAbilityTipo.Habilidade => CategoriaDesabilitavel.Habilidade,
                SpellAbilityTipo.Passiva => CategoriaDesabilitavel.Passiva,
                _ => (CategoriaDesabilitavel?)null
            };
            if (categoria is { } c)
                porCategoria[c].Add((linha.Id, linha.Nome));
        }
    }

    // Linhas (Id da linha, Id do Item do catálogo) → (Id da linha, nome do Item).
    private async Task<List<(Guid Id, string Nome)>> Nomear(IQueryable<LinhaDeItem> consulta)
    {
        var linhas = await consulta.ToListAsync();
        var nomes = await NomesDosItensAsync(linhas.Select(l => l.ItemId));
        return linhas.Select(l => (l.Id, nomes.GetValueOrDefault(l.ItemId, ""))).ToList();
    }

    private sealed record LinhaDeItem(Guid Id, Guid ItemId);

    private async Task<Dictionary<Guid, string>> NomesDosItensAsync(IEnumerable<Guid> itemIds)
    {
        var ids = itemIds.Distinct().ToList();
        return await db.Items.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Nome);
    }
}
