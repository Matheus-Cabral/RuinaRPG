using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.ReferenceData;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Sub-Atributos and the FichaParaRequisitos snapshot for a Ficha de NPC — mirrors
/// CharacterSheetStats table-for-table, scoped to NpcAttributes/NpcSkills/NpcArtifacts. Moved out
/// of NpcSheetsController so both /sub-attributes (unchanged response) and the Passiva requisitos
/// check (Task 5) share one live computation. Read-only, everything derived live.
/// </summary>
public class NpcSheetStats(RuinaRpgDbContext db, IRulesDataProvider rules, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, EquipmentPenaltyService penalidades, TabelaDeAfinidadesProvider tabelaDeAfinidades)
{
    /// <summary>
    /// VIS/EAP Atual do NPC: base do Nível + Âmbares Absorvidos, exatamente como na Ficha de Personagem.
    /// Único ponto de cálculo (resposta, Graduação, requisitos de Passiva); NpcSheet.EAPAtual é vestigial.
    /// </summary>
    public async Task<int> EapAtualAsync(NpcSheet sheet)
    {
        var tabela = await tabelaDeNiveis.ObterAsync();
        return EapCalculator.Compute(sheet.Nivel, sheet.NucleosRankF, sheet.NucleosRankE, sheet.NucleosRankD, sheet.NucleosRankC, sheet.NucleosRankB, sheet.NucleosRankA, sheet.NucleosRankS, tabela.ComoEapPorNivel());
    }

    /// <remarks>
    /// <paramref name="efeitos"/> são as Condições ativas da ficha (CondicoesAtivasDaFicha); sem elas, o valor sem Condições.
    /// As penalidades de equipamento são avaliadas antes, sobre a ficha sem Condições, e as Condições entram por cima.
    /// </remarks>
    public async Task<SubAttributesResponse> SubAtributosAsync(NpcSheet sheet, EfeitosDeCondicao? efeitos = null) =>
        await SubAtributosAsync(sheet, await ModificadoresAsync(sheet, efeitos), efeitos ?? EfeitosDeCondicao.Nenhum);

    private async Task<SubAttributesResponse> SubAtributosAsync(NpcSheet sheet, IReadOnlyList<ArtifactBonusInput> artefatos, EfeitosDeCondicao efeitos)
    {
        var id = sheet.Id;
        var agilidade = await GetAttributeTotalAsync(id, Atributo.Agilidade, artefatos, efeitos);
        var vigor = await GetAttributeTotalAsync(id, Atributo.Vigor, artefatos, efeitos);
        var forca = await GetAttributeTotalAsync(id, Atributo.Forca, artefatos, efeitos);

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        var brutoSkills = await db.NpcSkills
            .Where(s => s.NpcSheetId == id && (s.PericiaId == PericiasDeSistema.Prontidao || s.PericiaId == PericiasDeSistema.Reflexos || s.PericiaId == PericiasDeSistema.Fortitude))
            .ToListAsync();
        int BrutoOf(int periciaId) => SkillFormulas.Modificador(
            brutoSkills.Single(s => s.PericiaId == periciaId).Gasto,
            HistoricoBonusCalculator.For(periciaId, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));

        var brutoProntidao = BrutoOf(PericiasDeSistema.Prontidao);
        var brutoReflexos = BrutoOf(PericiasDeSistema.Reflexos);
        var brutoFortitude = BrutoOf(PericiasDeSistema.Fortitude);

        var weapons = await db.NpcWeapons.Where(w => w.NpcSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var shields = await db.NpcShields.Where(s => s.NpcSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => new { s.IsEquipped, i.Peso }).ToListAsync();
        var inventoryItems = await db.NpcInventoryItems.Where(i => i.NpcSheetId == id)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>(), i => i.ItemId, g => g.Id, (i, g) => new { i.Qtd, g.Peso, g.CapacidadeExtra })
            .ToListAsync();

        // Peso Total 2.b: Inventário (5.a) + Armas/Escudos DESequipados. Armaduras never count —
        // an NpcArmorSlot with an ItemId is inherently worn (no unequipped state exists for armor
        // in this schema). A Capacidade Extra ("mochila") item doesn't add its own Peso here — it
        // raises pesoMaximo instead.
        var pesoAtual = inventoryItems.Where(i => CarryWeightCalculator.CountsTowardPesoAtual(i.CapacidadeExtra)).Sum(i => i.Peso * i.Qtd)
            + weapons.Where(w => !w.IsEquipped).Sum(w => w.Peso)
            + shields.Where(s => !s.IsEquipped).Sum(s => s.Peso);
        var capacidadeExtraTotal = inventoryItems.Sum(i => (i.CapacidadeExtra ?? 0m) * i.Qtd);
        var pesoMaximo = CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal, bonusDeCarga: 0m);

        // Escudo e Armadura desabilitados por Condição não estão em uso (a linha guardada não muda).
        var equippedShield = (await db.NpcShields
            .Where(s => s.NpcSheetId == id && s.IsEquipped)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => new { s.Id, i.BonusDefesa })
            .ToListAsync())
            .Where(s => efeitos.DesabilitadoPor(CategoriaDesabilitavel.Escudo, s.Id) is null)
            .Select(s => s.BonusDefesa)
            .FirstOrDefault();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Every NpcArmorSlot with an ItemId is inherently worn (no separate IsEquipped flag,
        // unlike weapons/shields) — so, unlike Peso Total Carregado, this is already scoped to
        // equipped armor only.
        var armorRfRm = await db.NpcArmorSlots
            .Where(a => a.NpcSheetId == id && a.ItemId != null)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { a.Id, i.RF, i.RM })
            .ToListAsync();
        armorRfRm = armorRfRm.Where(a => efeitos.DesabilitadoPor(CategoriaDesabilitavel.Armadura, a.Id) is null).ToList();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        var linhasDeAfinidade = await db.NpcAffinities.Where(a => a.NpcSheetId == id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor))
            .ToListAsync();
        // A Eficiência e o Dano Elemental saem do valor da Afinidade já com as Condições (o mesmo que a lista 2.c exibe).
        var valorDaAfinidade = SubAttributeFormulas.ValorDaAfinidadeCorrespondente(sheet.Afinidade, efeitos.NasAfinidades(linhasDeAfinidade));
        var linhasDaTabelaDeAfinidades = await tabelaDeAfinidades.LinhasAsync();

        return new SubAttributesResponse(
            Iniciativa: efeitos.NoSubAtributo(SubAtributoAlvo.Iniciativa, SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa))),
            Movimentacao: efeitos.NaMovimentacao(SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo)),
            // penalidadeArmadura fica 0: penalidades de equipamento chegam como modificadores negativos de Sub-Atributo (ver EquipmentPenaltyService), não por este termo.
            EsquivaNatural: efeitos.NoSubAtributo(SubAtributoAlvo.EsquivaNatural, SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0)),
            DefesaNatural: efeitos.NoSubAtributo(SubAtributoAlvo.DefesaNatural, SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus)),
            ReducaoFisica: efeitos.NoSubAtributo(SubAtributoAlvo.ReducaoFisica, SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf)),
            ReducaoMagica: efeitos.NoSubAtributo(SubAtributoAlvo.ReducaoMagica, SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm)),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo,
            EficienciaElemental: SubAttributeFormulas.EficienciaElemental(valorDaAfinidade, linhasDaTabelaDeAfinidades),
            DanoElemental: SubAttributeFormulas.DanoElemental(valorDaAfinidade, linhasDaTabelaDeAfinidades));
    }

    /// <summary>
    /// Graduação usa o VIS calculado (EapAtualAsync) — o mesmo de
    /// NpcSheetsController.ToResponseAsync e da Ficha de Personagem.
    /// </summary>
    public async Task<FichaParaRequisitos> FichaParaRequisitosAsync(NpcSheet sheet)
    {
        // Sempre SEM penalidades de equipamento: é contra este retrato que os Requisitos são avaliados.
        var artefatos = await ArtefatosAsync(sheet.Id);
        var atributos = await db.NpcAttributes.Where(a => a.NpcSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);
        var skills = await db.NpcSkills.Where(s => s.NpcSheetId == sheet.Id).ToDictionaryAsync(s => s.PericiaId);
        var porId = await pericias.PorIdAsync();
        // Toda Perícia ativa entra (linha ausente = Gasto 0 e Atributo sugerido, como na lista da ficha);
        // removidas ficam de fora e o avaliador ignora requisitos sobre elas.
        var totaisDePericia = porId.Values.Where(p => !p.IsDeleted).ToDictionary(p => p.Id, p =>
        {
            skills.TryGetValue(p.Id, out var s);
            var modificador = SkillFormulas.Modificador(s?.Gasto ?? 0, HistoricoBonusCalculator.For(p.Id, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));
            var atributo = s?.AtributoEscolhido ?? p.AtributoSugerido;
            return atributo is { } chosen && atributos.TryGetValue(chosen, out var atributoTotal)
                ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave))
                : (int?)null;
        });

        // Requisitos são sempre avaliados SEM Condições (como sem penalidades): elas só mudam o que a ficha exibe.
        var sub = await SubAtributosAsync(sheet, artefatos, EfeitosDeCondicao.Nenhum);

        var graduacao = sheet.Vocacao is null ? 0 : GraduacaoCalculator.Compute(sheet.Vocacao.Value, await EapAtualAsync(sheet), sheet.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: true, sheet.Nivel, sheet.Vocacao, sheet.SubVocacao, sheet.Linhagem, sheet.Variante,
            graduacao, sheet.PossuiCoracaoDeMana, sheet.Afinidade, sheet.Estrela, sheet.HistoricoId,
            atributos, CharacterSheetStats.SubAtributosPorEnum(sub), totaisDePericia);
    }

    /// <summary>
    /// Every NpcArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses 5.b has
    /// no equip/unequip toggle for Artefatos, so simply being on the sheet counts as equipped.
    /// Só Artefatos: o que as telas exibem soma também as penalidades (<see cref="ModificadoresAsync"/>).
    /// </summary>
    public async Task<List<ArtifactBonusInput>> ArtefatosAsync(Guid sheetId, EfeitosDeCondicao? efeitos = null)
    {
        var linhas = await db.NpcArtifacts
            .Where(a => a.NpcSheetId == sheetId)
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
        var armas = (await db.NpcWeapons.Where(w => w.NpcSheetId == sheetId).Select(w => new { w.Id, ItemId = w.ItemId, EmUso = w.IsEquipped }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Arma, x.Id) is null)).ToList();
        var escudos = (await db.NpcShields.Where(s => s.NpcSheetId == sheetId).Select(s => new { s.Id, ItemId = s.ItemId, EmUso = s.IsEquipped }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Escudo, x.Id) is null)).ToList();
        var armaduras = (await db.NpcArmorSlots.Where(a => a.NpcSheetId == sheetId && a.ItemId != null).Select(a => new { a.Id, ItemId = a.ItemId!.Value, EmUso = true }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Armadura, x.Id) is null)).ToList();
        var artefatos = (await db.NpcArtifacts.Where(a => a.NpcSheetId == sheetId).Select(a => new { a.Id, ItemId = a.ArtifactItemId, EmUso = true }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Artefato, x.Id) is null)).ToList();
        return [.. armas, .. escudos, .. armaduras, .. artefatos];
    }

    public async Task<List<PenalidadeAtiva>> PenalidadesAtivasAsync(NpcSheet sheet, EfeitosDeCondicao? efeitos = null) =>
        await penalidades.AtivasAsync(await EquipamentosAsync(sheet.Id, efeitos), () => FichaParaRequisitosAsync(sheet));

    /// <summary>Artefatos + penalidades de equipamento ativas — o que as fórmulas exibidas somam no termo Artefatos.</summary>
    public async Task<List<ArtifactBonusInput>> ModificadoresAsync(NpcSheet sheet, EfeitosDeCondicao? efeitos = null)
    {
        var modificadores = await ArtefatosAsync(sheet.Id, efeitos);
        modificadores.AddRange(await penalidades.ComoModificadoresAsync(await PenalidadesAtivasAsync(sheet, efeitos)));
        return modificadores;
    }

    /// <summary>Os quatro totais de recurso exibidos, com as Condições de <paramref name="efeitos"/> — mesma conta de CharacterSheetStats.MaximosAsync.</summary>
    public async Task<(int Vitalidade, int Foco, int Adrenalina, int Estresse)> MaximosAsync(NpcSheet sheet, EfeitosDeCondicao efeitos)
    {
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        var vigorTotal = await GetAttributeTotalAsync(sheet.Id, Atributo.Vigor, artefatos, efeitos);
        var astuciaTotal = await GetAttributeTotalAsync(sheet.Id, Atributo.Astucia, artefatos, efeitos);
        var (statusVida, statusFoco) = sheet.Vocacao is not null ? VidaEArcanaPorNivel.Vocacao(rules.Vocacoes, CharacterSheetStats.VocacaoTabelaName(sheet.Vocacao.Value), sheet.Nivel) : (0, 0);
        var artefatoBonusParaAdrenalina = ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Adrenalina);
        return efeitos.NosMaximos(vigorTotal, statusVida, astuciaTotal, statusFoco, artefatoBonusParaAdrenalina);
    }

    /// <summary>Os Atributos da ficha na ordem de exibição, com o total calculado com <paramref name="efeitos"/> — a conta da lista de Atributos e do resumo do encontro.</summary>
    public async Task<List<AtributoCalculado>> AtributosAsync(NpcSheet sheet, EfeitosDeCondicao efeitos)
    {
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        return (await db.NpcAttributes.Where(a => a.NpcSheetId == sheet.Id).ToListAsync())
            .OrderBy(a => AttributeDisplayOrder.Rank(a.Atributo))
            .Select(a => new AtributoCalculado(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                efeitos.NoAtributo(a.Atributo.ToString(), AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                    artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())))))
            .ToList();
    }

    /// <summary>Todas as Perícias ativas, por nome, com o total calculado com <paramref name="efeitos"/> — a conta da lista de Perícias e do resumo do encontro.</summary>
    public async Task<List<PericiaCalculada>> PericiasAsync(NpcSheet sheet, EfeitosDeCondicao efeitos)
    {
        var ativas = (await pericias.PorIdAsync()).Values.Where(p => !p.IsDeleted).ToList();
        var linhas = await db.NpcSkills.Where(s => s.NpcSheetId == sheet.Id).ToDictionaryAsync(s => s.PericiaId);
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        var totaisDeAtributo = await db.NpcAttributes.Where(a => a.NpcSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => efeitos.NoAtributo(a.Atributo.ToString(), AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString()))));
        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        return ativas
            .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p =>
            {
                linhas.TryGetValue(p.Id, out var s);
                var gasto = s?.Gasto ?? 0;
                var atributo = s?.AtributoEscolhido ?? p.AtributoSugerido;
                var historicoBonus = HistoricoBonusCalculator.For(p.Id, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId);
                var modificador = SkillFormulas.Modificador(gasto, historicoBonus);
                var total = atributo is not null && totaisDeAtributo.TryGetValue(atributo.Value, out var atributoTotal)
                    ? efeitos.NaPericia(p.Chave, SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave)))
                    : (int?)null;
                return new PericiaCalculada(p.Chave, p.Nome, p.Descricao, gasto, modificador, atributo?.ToString(), total, historicoBonus > 0);
            })
            .ToList();
    }

    private async Task<int> GetAttributeTotalAsync(Guid sheetId, Atributo atributo, IReadOnlyList<ArtifactBonusInput> artefatos, EfeitosDeCondicao efeitos)
    {
        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        return efeitos.NoAtributo(atributo.ToString(), AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString())));
    }
}
