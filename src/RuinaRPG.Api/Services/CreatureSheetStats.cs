using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.ReferenceData;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Estatísticas da Ficha de Criatura sobre o núcleo comum (FichaStats).
///
/// R0005 §2.b: "Sub-Atributos — mesmos campos e fórmulas do Personagem" for 6 of its 9 listed
/// sub-attributes (Iniciativa, Movimentação, Esquiva Natural, Defesa Natural, Redução Física,
/// Redução Mágica); Resistência Física/Arcana and Dano Cortante are explicitly "pendente" — no
/// formula defined yet, so they're not implemented here. A Criatura não tem Histórico, Afinidades (2.c),
/// Inventário nem identidade de personagem; suas Perícias são só as DisponivelParaCriaturas.
/// </summary>
public class CreatureSheetStats(RuinaRpgDbContext db, IRulesDataProvider rules, IPericiaCatalogo pericias, EquipmentPenaltyService penalidades, CatalogoDeAfinidadesService afinidades)
    : FichaStats<CreatureSheet, AtributoCriatura, CreatureAttribute, CreatureSkill, CreatureShield, CreatureArmorSlot, CreatureArtifact>(db, rules, pericias, penalidades, afinidades)
{
    protected override Guid? HistoricoId(CreatureSheet sheet) => null;

    protected override bool SoPericiasDeCriatura => true;

    /// <summary>
    /// Espólios (5.a) é o que a Criatura deixa ao ser derrotada, não inventário carregado: sem Inventário. O Peso
    /// carregado (Armas e Escudos do Catálogo desequipados, como no Personagem) só entra na Movimentação.
    /// </summary>
    protected override Task<List<(int Qtd, decimal Peso, decimal? CapacidadeExtra)>> InventarioAsync(Guid sheetId) => Task.FromResult(new List<(int, decimal, decimal?)>());

    protected override bool ExibePeso => false;

    /// <summary>Eficiência Elemental/Dano Elemental não existem na Ficha de Criatura — ela não tem Vocação nem a lista incremental de Afinidades (2.c).</summary>
    protected override Task<(int? Eficiencia, int? Dano)> ElementalAsync(CreatureSheet sheet, EfeitosDeCondicao efeitos) =>
        Task.FromResult<(int?, int?)>((null, null));

    /// <summary>Ataque natural (ItemId nulo) não é item de catálogo: não tem Requisitos nem Penalidade.</summary>
    protected override async Task<List<(Guid Id, Guid ItemId, bool IsEquipped)>> ArmasDoCatalogoAsync(Guid sheetId) =>
        (await Db.CreatureWeapons.Where(w => w.SheetId == sheetId && w.ItemId != null).Select(w => new { w.Id, ItemId = w.ItemId!.Value, w.IsEquipped }).ToListAsync())
            .Select(x => (x.Id, x.ItemId, x.IsEquipped)).ToList();

    /// <summary>
    /// Criatura has no identidade de personagem (Vocação/Classe/Linhagem/Variante/Grau-Círculo/
    /// Coração de Mana/Estrela/Histórico) — those requisitos are ignored for it (Task 1's
    /// FichaParaRequisitos contract). Only the 5 shared Atributos (sem o Ego) and the Criatura's own
    /// (shorter) Perícia list are populated.
    /// </summary>
    protected override Task<FichaParaRequisitos> RetratoAsync(CreatureSheet sheet, Dictionary<Atributo, int> atributos, Dictionary<SubAtributo, int> subAtributos, Dictionary<int, int?> pericias) =>
        Task.FromResult(new FichaParaRequisitos(
            TemIdentidadeDePersonagem: false, sheet.Nivel, null, null, null, null, 0, false, sheet.Afinidade, null, null,
            atributos, subAtributos, pericias));

    /// <summary>A ordem do enum AtributoCriatura (a do ORDER BY de antes: o Atributo é gravado como inteiro).</summary>
    protected override int OrdemDeExibicao(AtributoCriatura atributo) => (int)atributo;

    /// <summary>
    /// Vitalidade, Foco e Adrenalina exibidos, com as Condições de <paramref name="efeitos"/>. A Criatura não tem
    /// Estresse. Status de Vida/Foco pela Tabela de Arquétipos (Arquétipo × Nível): os nomes do enum (Fisico/Arcano)
    /// são os da tabela, sem mapa de acentos.
    /// </summary>
    public async Task<(int Vitalidade, int Foco, int Adrenalina)> MaximosAsync(CreatureSheet sheet, EfeitosDeCondicao efeitos)
    {
        var status = sheet.Arquetipo is not null ? VidaEArcanaPorNivel.Arquetipo(Rules.Arquetipos, sheet.Arquetipo.Value.ToString(), sheet.Nivel) : (0, 0);
        var (vitalidade, foco, adrenalina, _) = await MaximosComStatusAsync(sheet, efeitos, status);
        return (vitalidade, foco, adrenalina);
    }
}
