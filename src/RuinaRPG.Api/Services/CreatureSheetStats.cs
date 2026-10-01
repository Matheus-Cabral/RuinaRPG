using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Sub-Atributos and the FichaParaRequisitos snapshot for a Ficha de Criatura — mirrors
/// CharacterSheetStats table-for-table, scoped to CreatureAttributes/CreatureSkills/
/// CreatureArtifacts/CreatureWeapons/CreatureArmorSlots/CreatureShields. Moved out of
/// CreatureSheetsController so both /sub-attributes (unchanged response) and the Passiva
/// requisitos check (Task 5) share one live computation. Read-only, everything derived live.
///
/// R0005 §2.b: "Sub-Atributos — mesmos campos e fórmulas do Personagem" for 6 of its 9 listed
/// sub-attributes (Iniciativa, Movimentação, Esquiva Natural, Defesa Natural, Redução Física,
/// Redução Mágica); Resistência Física/Arcana and Dano Cortante are explicitly "pendente" — no
/// formula defined yet, so they're not implemented here.
/// </summary>
public class CreatureSheetStats(RuinaRpgDbContext db, IPericiaCatalogo pericias)
{
    /// <summary>
    /// Ego is a Criatura-only attribute — it has no counterpart in Ficha de Personagem's Atributo
    /// enum, so it's dropped from FichaParaRequisitos.Atributos (Task 1's contract: "só entram as
    /// chaves que aquele tipo de ficha tem"). The other five share their names 1:1 across
    /// AtributoCriatura/Atributo.
    /// </summary>
    private static readonly IReadOnlyDictionary<AtributoCriatura, Atributo> AtributoCriaturaParaAtributo = new Dictionary<AtributoCriatura, Atributo>
    {
        [AtributoCriatura.Forca] = Atributo.Forca,
        [AtributoCriatura.Vigor] = Atributo.Vigor,
        [AtributoCriatura.Agilidade] = Atributo.Agilidade,
        [AtributoCriatura.Destreza] = Atributo.Destreza,
        [AtributoCriatura.Astucia] = Atributo.Astucia,
    };

    public async Task<SubAttributesResponse> SubAtributosAsync(CreatureSheet sheet)
    {
        var id = sheet.Id;
        var artefatos = await GetArtifactBonusInputsAsync(id);
        var agilidade = await GetAttributeTotalAsync(id, AtributoCriatura.Agilidade, artefatos);
        var vigor = await GetAttributeTotalAsync(id, AtributoCriatura.Vigor, artefatos);
        var forca = await GetAttributeTotalAsync(id, AtributoCriatura.Forca, artefatos);

        var brutoSkills = await db.CreatureSkills
            .Where(s => s.CreatureSheetId == id && (s.PericiaId == PericiasDeSistema.Prontidao || s.PericiaId == PericiasDeSistema.Reflexos || s.PericiaId == PericiasDeSistema.Fortitude))
            .ToListAsync();
        int BrutoOf(int periciaId) => SkillFormulas.Modificador(brutoSkills.Single(s => s.PericiaId == periciaId).Gasto, 0);

        var brutoProntidao = BrutoOf(PericiasDeSistema.Prontidao);
        var brutoReflexos = BrutoOf(PericiasDeSistema.Reflexos);
        var brutoFortitude = BrutoOf(PericiasDeSistema.Fortitude);

        // Natural attacks (ItemId null) carry no weight of their own — only Catálogo-linked
        // weapons/armor/shields contribute to Peso Total Carregado.
        var weapons = await db.CreatureWeapons.Where(w => w.CreatureSheetId == id && w.ItemId != null).Join(db.Items, w => w.ItemId!.Value, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var armorSlots = await db.CreatureArmorSlots.Where(a => a.CreatureSheetId == id && a.ItemId != null).Join(db.Items, a => a.ItemId!.Value, i => i.Id, (a, i) => i.Peso).ToListAsync();
        var shields = await db.CreatureShields.Where(s => s.CreatureSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => i.Peso).ToListAsync();
        var pesoTotalCarregado = weapons.Sum(w => w.Peso) + armorSlots.Sum() + shields.Sum();

        var equippedShield = await db.CreatureShields
            .Where(s => s.CreatureSheetId == id && s.IsEquipped)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => i.BonusDefesa)
            .FirstOrDefaultAsync();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Every CreatureArmorSlot with an ItemId is inherently worn (no separate IsEquipped
        // flag, unlike weapons/shields) — so, unlike Peso Total Carregado, this is already
        // scoped to equipped armor only.
        var armorRfRm = await db.CreatureArmorSlots
            .Where(a => a.CreatureSheetId == id && a.ItemId != null)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { i.RF, i.RM })
            .ToListAsync();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual: pesoTotalCarregado, pesoMaximo: CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal: 0m)),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number — same real, still-open gap as
            // the Ficha de NPCs version.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm),
            // Espólios (5.a) is loot dropped when defeated, not a carried inventory — out of scope
            // for this feature. See docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md.
            PesoAtual: null,
            PesoMaximo: null,
            // Eficiência Elemental/Dano Elemental não existem na Ficha de Criatura — ela não tem
            // Vocação nem a lista incremental de Afinidades (2.c).
            EficienciaElemental: null,
            DanoElemental: null);
    }

    /// <summary>
    /// Criatura has no identidade de personagem (Vocação/Classe/Linhagem/Variante/Grau-Círculo/
    /// Coração de Mana/Estrela/Histórico) — those requisitos are ignored for it (Task 1's
    /// FichaParaRequisitos contract). Only the 5 shared Atributos and the Criatura's own (shorter)
    /// Perícia list are populated.
    /// </summary>
    public async Task<FichaParaRequisitos> FichaParaRequisitosAsync(CreatureSheet sheet)
    {
        var artefatos = await GetArtifactBonusInputsAsync(sheet.Id);
        var atributosCriatura = await db.CreatureAttributes.Where(a => a.CreatureSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        var atributos = atributosCriatura
            .Where(kv => AtributoCriaturaParaAtributo.ContainsKey(kv.Key))
            .ToDictionary(kv => AtributoCriaturaParaAtributo[kv.Key], kv => kv.Value);

        var skills = await db.CreatureSkills.Where(s => s.CreatureSheetId == sheet.Id).ToListAsync();
        var porId = await pericias.PorIdAsync();
        // Só Perícias ativas: um requisito sobre uma Perícia removida é ignorado pelo avaliador.
        var totaisDePericia = skills.Where(s => porId.TryGetValue(s.PericiaId, out var p) && !p.IsDeleted).ToDictionary(s => s.PericiaId, s =>
        {
            var modificador = SkillFormulas.Modificador(s.Gasto, 0);
            return s.AtributoEscolhido is { } atributo && atributosCriatura.TryGetValue(atributo, out var atributoTotal)
                ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, porId[s.PericiaId].Chave))
                : (int?)null;
        });

        var sub = await SubAtributosAsync(sheet);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: false, sheet.Nivel, null, null, null, null, 0, false, sheet.Afinidade, null, null,
            atributos, CharacterSheetStats.SubAtributosPorEnum(sub), totaisDePericia);
    }

    /// <summary>
    /// Every CreatureArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor). Moved here
    /// from CreatureSheetsController; that controller keeps its own copy too since
    /// ModificadorDeDano/ToResponseAsync still need it independently of SubAttributes/
    /// FichaParaRequisitos.
    /// </summary>
    private async Task<List<ArtifactBonusInput>> GetArtifactBonusInputsAsync(Guid sheetId) =>
        await db.CreatureArtifacts
            .Where(a => a.CreatureSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    private async Task<int> GetAttributeTotalAsync(Guid sheetId, AtributoCriatura atributo, IReadOnlyList<ArtifactBonusInput> artefatos)
    {
        var attribute = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetId && a.Atributo == atributo);
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString()));
    }
}
