using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.ReferenceData;
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
public class CreatureSheetStats(RuinaRpgDbContext db, IRulesDataProvider rules, IPericiaCatalogo pericias, EquipmentPenaltyService penalidades)
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

    /// <remarks>
    /// <paramref name="efeitos"/> são as Condições ativas da ficha (CondicoesAtivasDaFicha); sem elas, o valor sem Condições.
    /// As penalidades de equipamento são avaliadas antes, sobre a ficha sem Condições, e as Condições entram por cima.
    /// </remarks>
    public async Task<SubAttributesResponse> SubAtributosAsync(CreatureSheet sheet, EfeitosDeCondicao? efeitos = null) =>
        await SubAtributosAsync(sheet, await ModificadoresAsync(sheet, efeitos), efeitos ?? EfeitosDeCondicao.Nenhum);

    private async Task<SubAttributesResponse> SubAtributosAsync(CreatureSheet sheet, IReadOnlyList<ArtifactBonusInput> artefatos, EfeitosDeCondicao efeitos)
    {
        var id = sheet.Id;
        var agilidade = await GetAttributeTotalAsync(id, AtributoCriatura.Agilidade, artefatos, efeitos);
        var vigor = await GetAttributeTotalAsync(id, AtributoCriatura.Vigor, artefatos, efeitos);
        var forca = await GetAttributeTotalAsync(id, AtributoCriatura.Forca, artefatos, efeitos);

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

        // Escudo e Armadura desabilitados por Condição não estão em uso (a linha guardada não muda).
        var equippedShield = (await db.CreatureShields
            .Where(s => s.CreatureSheetId == id && s.IsEquipped)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => new { s.Id, i.BonusDefesa })
            .ToListAsync())
            .Where(s => efeitos.DesabilitadoPor(CategoriaDesabilitavel.Escudo, s.Id) is null)
            .Select(s => s.BonusDefesa)
            .FirstOrDefault();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Every CreatureArmorSlot with an ItemId is inherently worn (no separate IsEquipped
        // flag, unlike weapons/shields) — so, unlike Peso Total Carregado, this is already
        // scoped to equipped armor only.
        var armorRfRm = await db.CreatureArmorSlots
            .Where(a => a.CreatureSheetId == id && a.ItemId != null)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { a.Id, i.RF, i.RM })
            .ToListAsync();
        armorRfRm = armorRfRm.Where(a => efeitos.DesabilitadoPor(CategoriaDesabilitavel.Armadura, a.Id) is null).ToList();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        return new SubAttributesResponse(
            Iniciativa: efeitos.NoSubAtributo(SubAtributoAlvo.Iniciativa, SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa))),
            Movimentacao: efeitos.NaMovimentacao(SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual: pesoTotalCarregado, pesoMaximo: CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal: 0m, bonusDeCarga: 0m))),
            // penalidadeArmadura fica 0: penalidades de equipamento chegam como modificadores negativos de Sub-Atributo (ver EquipmentPenaltyService), não por este termo.
            EsquivaNatural: efeitos.NoSubAtributo(SubAtributoAlvo.EsquivaNatural, SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0)),
            DefesaNatural: efeitos.NoSubAtributo(SubAtributoAlvo.DefesaNatural, SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus)),
            ReducaoFisica: efeitos.NoSubAtributo(SubAtributoAlvo.ReducaoFisica, SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf)),
            ReducaoMagica: efeitos.NoSubAtributo(SubAtributoAlvo.ReducaoMagica, SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm)),
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
        // Sempre SEM penalidades de equipamento: é contra este retrato que os Requisitos são avaliados.
        var artefatos = await ArtefatosAsync(sheet.Id);
        var atributosCriatura = await db.CreatureAttributes.Where(a => a.CreatureSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        var atributos = atributosCriatura
            .Where(kv => AtributoCriaturaParaAtributo.ContainsKey(kv.Key))
            .ToDictionary(kv => AtributoCriaturaParaAtributo[kv.Key], kv => kv.Value);

        var skills = await db.CreatureSkills.Where(s => s.CreatureSheetId == sheet.Id).ToDictionaryAsync(s => s.PericiaId);
        var porId = await pericias.PorIdAsync();
        // Toda Perícia ativa entra (linha ausente = Gasto 0 e Atributo sugerido, como na lista da ficha);
        // removidas ficam de fora e o avaliador ignora requisitos sobre elas.
        var totaisDePericia = porId.Values.Where(p => !p.IsDeleted && p.DisponivelParaCriaturas).ToDictionary(p => p.Id, p =>
        {
            skills.TryGetValue(p.Id, out var s);
            var modificador = SkillFormulas.Modificador(s?.Gasto ?? 0, 0);
            var atributo = s?.AtributoEscolhido ?? (p.AtributoSugerido is { } sug && Enum.TryParse<AtributoCriatura>(sug.ToString(), out var sugCriatura) ? sugCriatura : null);
            return atributo is { } chosen && atributosCriatura.TryGetValue(chosen, out var atributoTotal)
                ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave))
                : (int?)null;
        });

        // Requisitos são sempre avaliados SEM Condições (como sem penalidades): elas só mudam o que a ficha exibe.
        var sub = await SubAtributosAsync(sheet, artefatos, EfeitosDeCondicao.Nenhum);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: false, sheet.Nivel, null, null, null, null, 0, false, sheet.Afinidade, null, null,
            atributos, CharacterSheetStats.SubAtributosPorEnum(sub), totaisDePericia);
    }

    /// <summary>
    /// Every CreatureArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses
    /// 5.b has no equip/unequip toggle for Artefatos, so simply being on the sheet counts as
    /// equipped. Só Artefatos: o que as telas exibem soma também as penalidades (<see cref="ModificadoresAsync"/>).
    /// </summary>
    public async Task<List<ArtifactBonusInput>> ArtefatosAsync(Guid sheetId, EfeitosDeCondicao? efeitos = null)
    {
        var linhas = await db.CreatureArtifacts
            .Where(a => a.CreatureSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => new { a.Id, i })
            .Where(x => x.i.TipoDeAlvo != null)
            .Select(x => new { x.Id, Bonus = new ArtifactBonusInput(x.i.TipoDeAlvo!.Value, x.i.Alvo, x.i.Valor ?? 0) })
            .ToListAsync();
        // Artefato desabilitado por Condição não está em uso: o bônus não conta enquanto ela durar.
        return linhas.Where(l => efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Artefato, l.Id) is null).Select(l => l.Bonus).ToList();
    }

    /// <summary>Os equipamentos da ficha e se estão em uso (Requisitos - Catálogo de Itens, Requisitos/Penalidade).</summary>
    private async Task<List<EquipamentoDaFicha>> EquipamentosAsync(Guid sheetId, EfeitosDeCondicao? efeitos = null)
    {
        // Equipamento desabilitado por Condição não está em uso (a linha guardada não muda): sem penalidade enquanto ela durar.
        // Ataque natural (ItemId nulo) não é item de catálogo: não tem Requisitos nem Penalidade.
        var armas = (await db.CreatureWeapons.Where(w => w.CreatureSheetId == sheetId && w.ItemId != null).Select(w => new { w.Id, ItemId = w.ItemId!.Value, EmUso = w.IsEquipped }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Arma, x.Id) is null)).ToList();
        var escudos = (await db.CreatureShields.Where(s => s.CreatureSheetId == sheetId).Select(s => new { s.Id, ItemId = s.ItemId, EmUso = s.IsEquipped }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Escudo, x.Id) is null)).ToList();
        var armaduras = (await db.CreatureArmorSlots.Where(a => a.CreatureSheetId == sheetId && a.ItemId != null).Select(a => new { a.Id, ItemId = a.ItemId!.Value, EmUso = true }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Armadura, x.Id) is null)).ToList();
        var artefatos = (await db.CreatureArtifacts.Where(a => a.CreatureSheetId == sheetId).Select(a => new { a.Id, ItemId = a.ArtifactItemId, EmUso = true }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Artefato, x.Id) is null)).ToList();
        return [.. armas, .. escudos, .. armaduras, .. artefatos];
    }

    public async Task<List<PenalidadeAtiva>> PenalidadesAtivasAsync(CreatureSheet sheet, EfeitosDeCondicao? efeitos = null) =>
        await penalidades.AtivasAsync(await EquipamentosAsync(sheet.Id, efeitos), () => FichaParaRequisitosAsync(sheet));

    /// <summary>Artefatos + penalidades de equipamento ativas — o que as fórmulas exibidas somam no termo Artefatos.</summary>
    public async Task<List<ArtifactBonusInput>> ModificadoresAsync(CreatureSheet sheet, EfeitosDeCondicao? efeitos = null)
    {
        var modificadores = await ArtefatosAsync(sheet.Id, efeitos);
        modificadores.AddRange(await penalidades.ComoModificadoresAsync(await PenalidadesAtivasAsync(sheet, efeitos)));
        return modificadores;
    }

    /// <summary>
    /// Vitalidade, Foco e Adrenalina exibidos, com as Condições de <paramref name="efeitos"/>. A Criatura não tem
    /// Estresse. Status de Vida/Foco pela Tabela de Arquétipos (Arquétipo × Nível): os nomes do enum (Fisico/Arcano)
    /// são os da tabela, sem mapa de acentos.
    /// </summary>
    public async Task<(int Vitalidade, int Foco, int Adrenalina)> MaximosAsync(CreatureSheet sheet, EfeitosDeCondicao efeitos)
    {
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        var vigorTotal = await GetAttributeTotalAsync(sheet.Id, AtributoCriatura.Vigor, artefatos, efeitos);
        var astuciaTotal = await GetAttributeTotalAsync(sheet.Id, AtributoCriatura.Astucia, artefatos, efeitos);
        var (statusVida, statusFoco) = sheet.Arquetipo is not null ? VidaEArcanaPorNivel.Arquetipo(rules.Arquetipos, sheet.Arquetipo.Value.ToString(), sheet.Nivel) : (0, 0);
        var artefatoBonusParaAdrenalina = ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Adrenalina);
        var (vitalidade, foco, adrenalina, _) = efeitos.NosMaximos(vigorTotal, statusVida, astuciaTotal, statusFoco, artefatoBonusParaAdrenalina);
        return (vitalidade, foco, adrenalina);
    }

    /// <summary>Os Atributos da ficha, com o total calculado com <paramref name="efeitos"/> — a conta da lista de Atributos e do resumo do encontro.</summary>
    public async Task<List<AtributoCalculado>> AtributosAsync(CreatureSheet sheet, EfeitosDeCondicao efeitos)
    {
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        return (await db.CreatureAttributes.Where(a => a.CreatureSheetId == sheet.Id).OrderBy(a => a.Atributo).ToListAsync())
            .Select(a => new AtributoCalculado(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                efeitos.NoAtributo(a.Atributo.ToString(), AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                    artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())))))
            .ToList();
    }

    /// <summary>As Perícias disponíveis para Criaturas, por nome, com o total calculado com <paramref name="efeitos"/> — a conta da lista de Perícias e do resumo do encontro.</summary>
    public async Task<List<PericiaCalculada>> PericiasAsync(CreatureSheet sheet, EfeitosDeCondicao efeitos)
    {
        var ativas = (await pericias.PorIdAsync()).Values.Where(p => !p.IsDeleted && p.DisponivelParaCriaturas).ToList();
        var linhas = await db.CreatureSkills.Where(s => s.CreatureSheetId == sheet.Id).ToDictionaryAsync(s => s.PericiaId);
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        var totaisDeAtributo = await db.CreatureAttributes.Where(a => a.CreatureSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => efeitos.NoAtributo(a.Atributo.ToString(), AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString()))));

        return ativas
            .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p =>
            {
                linhas.TryGetValue(p.Id, out var s);
                var gasto = s?.Gasto ?? 0;
                var atributo = s?.AtributoEscolhido ?? SugeridoParaCriatura(p);
                var modificador = SkillFormulas.Modificador(gasto, 0);
                var total = atributo is not null && totaisDeAtributo.TryGetValue(atributo.Value, out var atributoTotal)
                    ? efeitos.NaPericia(p.Chave, SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave)))
                    : (int?)null;
                return new PericiaCalculada(p.Chave, p.Nome, p.Descricao, gasto, modificador, atributo?.ToString(), total, false);
            })
            .ToList();
    }

    /// <summary>Atributo sugerido como Atributo de Criatura; nulo se a Criatura não tem esse Atributo.</summary>
    private static AtributoCriatura? SugeridoParaCriatura(PericiaDefinicao p) =>
        p.AtributoSugerido is { } a && Enum.TryParse<AtributoCriatura>(a.ToString(), out var c) ? c : null;

    private async Task<int> GetAttributeTotalAsync(Guid sheetId, AtributoCriatura atributo, IReadOnlyList<ArtifactBonusInput> artefatos, EfeitosDeCondicao efeitos)
    {
        var attribute = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetId && a.Atributo == atributo);
        return efeitos.NoAtributo(atributo.ToString(), AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString())));
    }
}
