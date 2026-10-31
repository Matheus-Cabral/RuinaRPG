using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Quanto um elemento do catálogo é usado (Auditoria de Regras R0018), para recusar a remoção:
/// <list type="bullet">
/// <item><b>fichas</b>: Personagens e NPCs com pontos nele ou com ele como <i>Afinidade</i> (cada ficha uma vez) e
/// Criaturas com ele como <i>Afinidade</i>;</item>
/// <item><b>Condições</b>: as do catálogo (não excluídas) com uma linha de alvo <i>Afinidade elemental</i> ou
/// <i>Dano elemental</i> nele;</item>
/// <item><b>Passivas</b>: todo requisito que o exige — Passivas do Banco, as cópias nas fichas, itens do Catálogo e
/// itens fixos dos kits de Equipagem (os requisitos de equipamento são os mesmos das Passivas).</item>
/// </list>
/// Cruzamentos e ligações de visibilidade não contam: saem junto com o elemento.
/// </summary>
public static class UsosDoElemento
{
    public static async Task<(int Fichas, int Condicoes, int Passivas)> ContarAsync(RuinaRpgDbContext db, int id)
    {
        var fichas =
            await db.CharacterPontosDeAfinidade.Where(p => p.ElementoId == id).Select(p => p.SheetId)
                .Union(db.CharacterSheets.Where(s => s.Afinidade == id).Select(s => s.Id)).CountAsync()
            + await db.NpcPontosDeAfinidade.Where(p => p.ElementoId == id).Select(p => p.SheetId)
                .Union(db.NpcSheets.Where(s => s.Afinidade == id).Select(s => s.Id)).CountAsync()
            + await db.CreatureSheets.CountAsync(s => s.Afinidade == id);

        var alvo = id.ToString(CultureInfo.InvariantCulture);
        var condicoes = await db.Condicoes.CountAsync(c => !c.IsDeleted && db.CondicaoModificadores.Any(m =>
            m.CondicaoId == c.Id && (m.Alvo == AlvoDeCondicao.AfinidadeElemental || m.Alvo == AlvoDeCondicao.DanoElemental)
            && m.AlvoEspecifico == alvo));

        var passivas =
            await ComRequisitoAsync(db.SpellAbilityBankEntries, e => e.Requisitos, id)
            + await ComRequisitoAsync(db.CharacterSpellAbilities, e => e.Requisitos, id)
            + await ComRequisitoAsync(db.NpcSpellAbilities, e => e.Requisitos, id)
            + await ComRequisitoAsync(db.CreatureSpellAbilities, e => e.Requisitos, id)
            + await ComRequisitoAsync(db.Items, e => e.Requisitos, id)
            + await ComRequisitoAsync(db.EquipmentKitFixedItems, e => e.Requisitos, id);

        return (fichas, condicoes, passivas);
    }

    // Os requisitos são uma coluna jsonb lida inteira pelo conversor: filtra-se a Afinidade em memória.
    private static async Task<int> ComRequisitoAsync<T>(IQueryable<T> fonte, Expression<Func<T, RequisitosDePassiva?>> requisitos, int id) =>
        (await fonte.Select(requisitos).Where(r => r != null).ToListAsync()).Count(r => r!.Afinidade == id);
}
