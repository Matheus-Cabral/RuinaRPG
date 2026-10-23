using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.ReferenceData;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Services;

/// <summary>O que o núcleo de estatísticas compartilha sem depender do tipo de ficha.</summary>
public static class FichaStats
{
    /// <summary>Os Sub-Atributos da resposta, indexados por SubAtributo (o formato de FichaParaRequisitos).</summary>
    internal static Dictionary<SubAtributo, int> SubAtributosPorEnum(SubAttributesResponse sub) => new()
    {
        [SubAtributo.Iniciativa] = sub.Iniciativa,
        [SubAtributo.Movimentacao] = sub.Movimentacao,
        [SubAtributo.EsquivaNatural] = sub.EsquivaNatural,
        [SubAtributo.DefesaNatural] = sub.DefesaNatural,
        [SubAtributo.ReducaoFisica] = sub.ReducaoFisica,
        [SubAtributo.ReducaoMagica] = sub.ReducaoMagica,
    };

    /// <summary>
    /// Os nomes do enum Vocacao não têm acento (Campeao, Cacador), mas IRulesDataProvider.Vocacoes vem direto da
    /// Tabela de Vocação, com acento (Campeão, Caçador): sem este mapa, as duas dariam 0 de Vida/Foco. Só para essa consulta.
    /// </summary>
    internal static string VocacaoTabelaName(Vocacao vocacao) => vocacao switch
    {
        Vocacao.Campeao => "Campeão",
        Vocacao.Cacador => "Caçador",
        _ => vocacao.ToString()
    };
}

/// <summary>O que os endpoints comuns das fichas (FichasController) leem das estatísticas, para qualquer tipo.</summary>
public interface IFichaStats<in TSheet> where TSheet : class, IFicha
{
    Task<SubAttributesResponse> SubAtributosAsync(TSheet sheet, EfeitosDeCondicao? efeitos = null);
    Task<List<PenalidadeAtiva>> PenalidadesAtivasAsync(TSheet sheet, EfeitosDeCondicao? efeitos = null);
    Task<List<ArtifactBonusInput>> ModificadoresAsync(TSheet sheet, EfeitosDeCondicao? efeitos = null);
}

/// <summary>
/// Núcleo comum de CharacterSheetStats, NpcSheetStats e CreatureSheetStats (spec D.4): Sub-Atributos, o retrato
/// FichaParaRequisitos, Artefatos, equipamentos e penalidades, máximos de recurso, Atributos e Perícias calculados.
/// Cada tipo diz só o que muda (tabelas, Histórico, carga, Afinidades, identidade de personagem, ordem dos Atributos).
/// Somente leitura, tudo derivado ao vivo — nada aqui é persistido.
/// </summary>
public abstract class FichaStats<TSheet, TAtributo, TLinhaAtributo, TLinhaPericia, TEscudo, TSlot, TArtefato>(
    RuinaRpgDbContext db, IRulesDataProvider rules, IPericiaCatalogo pericias, EquipmentPenaltyService penalidades)
    : IFichaStats<TSheet>
    where TSheet : class, IFicha
    where TAtributo : struct, Enum
    where TLinhaAtributo : class, IAtributoDaFicha<TAtributo>
    where TLinhaPericia : class, IPericiaDaFicha<TAtributo>
    where TEscudo : class, IEquipavelDoCatalogo
    where TSlot : class, ISlotDeArmaduraDaFicha
    where TArtefato : class, IArtefatoDaFicha
{
    // Falham cedo (TypeInitializationException) se o enum de atributos da ficha não tiver algum desses nomes.
    private static readonly TAtributo Agilidade = Enum.Parse<TAtributo>(nameof(Atributo.Agilidade));
    private static readonly TAtributo Vigor = Enum.Parse<TAtributo>(nameof(Atributo.Vigor));
    private static readonly TAtributo Forca = Enum.Parse<TAtributo>(nameof(Atributo.Forca));
    private static readonly TAtributo Astucia = Enum.Parse<TAtributo>(nameof(Atributo.Astucia));

    protected RuinaRpgDbContext Db { get; } = db;
    protected IRulesDataProvider Rules { get; } = rules;

    /// <summary>Peso carregado e máximo que a Movimentação usa; <c>Exibida</c> falso deixa os dois fora da resposta (Criatura).</summary>
    protected record Carga(decimal PesoAtual, decimal PesoMaximo, bool Exibida = true, decimal BonusDeCargaDaCampanha = 0m);

    /// <summary>O Histórico da ficha, que dá o bônus +6/+3 de Perícia. Criatura não tem Histórico: null.</summary>
    protected abstract Guid? HistoricoId(TSheet sheet);

    /// <summary>Peso Total Carregado (2.b) e Peso Máximo, com os totais de Força e Vigor já calculados.</summary>
    protected abstract Task<Carga> CargaAsync(TSheet sheet, int forca, int vigor);

    /// <summary>Eficiência e Dano Elemental (2.b); a Criatura não tem Afinidades: os dois ficam nulos.</summary>
    protected abstract Task<(int? Eficiencia, int? Dano)> ElementalAsync(TSheet sheet, EfeitosDeCondicao efeitos);

    /// <summary>As Armas da ficha que são itens de catálogo (a Criatura deixa de fora os ataques naturais).</summary>
    protected abstract Task<List<(Guid Id, Guid ItemId, bool IsEquipped)>> ArmasDoCatalogoAsync(Guid sheetId);

    /// <summary>O retrato montado com a identidade de personagem do tipo (a Criatura não tem).</summary>
    protected abstract Task<FichaParaRequisitos> RetratoAsync(TSheet sheet, Dictionary<Atributo, int> atributos, Dictionary<SubAtributo, int> subAtributos, Dictionary<int, int?> pericias);

    /// <summary>Posição do Atributo na lista exibida.</summary>
    protected abstract int OrdemDeExibicao(TAtributo atributo);

    /// <summary>Só as Perícias com DisponivelParaCriaturas (Auditoria de Regras R0005)? Só Criatura.</summary>
    protected virtual bool SoPericiasDeCriatura => false;

    /// <remarks>
    /// <paramref name="efeitos"/> são as Condições ativas da ficha (CondicoesAtivasDaFicha); sem elas, o valor sem Condições.
    /// As penalidades de equipamento são avaliadas antes, sobre a ficha sem Condições, e as Condições entram por cima.
    /// </remarks>
    public async Task<SubAttributesResponse> SubAtributosAsync(TSheet sheet, EfeitosDeCondicao? efeitos = null) =>
        await SubAtributosAsync(sheet, await ModificadoresAsync(sheet, efeitos), efeitos ?? EfeitosDeCondicao.Nenhum);

    /// <summary>
    /// "Bruto [Perícia]" (Prontidão, Reflexos, Fortitude) é só o Modificador daquela Perícia (Sistema Básico §2 /
    /// SkillFormulas.Modificador), lido das linhas de Perícia da própria ficha.
    /// </summary>
    private async Task<SubAttributesResponse> SubAtributosAsync(TSheet sheet, IReadOnlyList<ArtifactBonusInput> artefatos, EfeitosDeCondicao efeitos)
    {
        var id = sheet.Id;
        var agilidade = await AtributoTotalAsync(id, Agilidade, artefatos, efeitos);
        var vigor = await AtributoTotalAsync(id, Vigor, artefatos, efeitos);
        var forca = await AtributoTotalAsync(id, Forca, artefatos, efeitos);

        var historico = await HistoricoAsync(sheet);
        var brutoSkills = await Db.Set<TLinhaPericia>()
            .Where(s => s.SheetId == id && (s.PericiaId == PericiasDeSistema.Prontidao || s.PericiaId == PericiasDeSistema.Reflexos || s.PericiaId == PericiasDeSistema.Fortitude))
            .ToListAsync();
        int BrutoOf(int periciaId) => SkillFormulas.Modificador(
            brutoSkills.Single(s => s.PericiaId == periciaId).Gasto,
            HistoricoBonusCalculator.For(periciaId, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));

        var brutoProntidao = BrutoOf(PericiasDeSistema.Prontidao);
        var brutoReflexos = BrutoOf(PericiasDeSistema.Reflexos);
        var brutoFortitude = BrutoOf(PericiasDeSistema.Fortitude);

        var carga = await CargaAsync(sheet, forca, vigor);

        // Escudo e Armadura desabilitados por Condição não estão em uso (a linha guardada não muda).
        var equippedShield = (await Db.Set<TEscudo>()
            .Where(s => s.SheetId == id && s.IsEquipped)
            .Join(Db.Set<Escudo>(), s => s.ItemId, i => i.Id, (s, i) => new { s.Id, i.BonusDefesa })
            .ToListAsync())
            .Where(s => efeitos.DesabilitadoPor(CategoriaDesabilitavel.Escudo, s.Id) is null)
            .Select(s => s.BonusDefesa)
            .FirstOrDefault();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Todo slot de Armadura com ItemId está vestido (não há IsEquipped, ao contrário de Armas/Escudos).
        var armorRfRm = await Db.Set<TSlot>()
            .Where(a => a.SheetId == id && a.ItemId != null)
            .Join(Db.Set<Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { a.Id, i.RF, i.RM })
            .ToListAsync();
        armorRfRm = armorRfRm.Where(a => efeitos.DesabilitadoPor(CategoriaDesabilitavel.Armadura, a.Id) is null).ToList();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        var (eficienciaElemental, danoElemental) = await ElementalAsync(sheet, efeitos);

        return new SubAttributesResponse(
            Iniciativa: efeitos.NoSubAtributo(SubAtributoAlvo.Iniciativa, SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa))),
            Movimentacao: efeitos.NaMovimentacao(SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), carga.PesoAtual, carga.PesoMaximo)),
            // penalidadeArmadura fica 0: penalidades de equipamento chegam como modificadores negativos de Sub-Atributo (ver EquipmentPenaltyService), não por este termo.
            EsquivaNatural: efeitos.NoSubAtributo(SubAtributoAlvo.EsquivaNatural, SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0)),
            DefesaNatural: efeitos.NoSubAtributo(SubAtributoAlvo.DefesaNatural, SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus)),
            ReducaoFisica: efeitos.NoSubAtributo(SubAtributoAlvo.ReducaoFisica, SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf)),
            ReducaoMagica: efeitos.NoSubAtributo(SubAtributoAlvo.ReducaoMagica, SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm)),
            PesoAtual: carga.Exibida ? carga.PesoAtual : null,
            PesoMaximo: carga.Exibida ? carga.PesoMaximo : null,
            EficienciaElemental: eficienciaElemental,
            DanoElemental: danoElemental,
            BonusDeCargaDaCampanha: carga.BonusDeCargaDaCampanha);
    }

    /// <summary>
    /// O que a ficha tem para comparar com Requisitos (de Passiva e de equipamento). Sempre SEM penalidades de
    /// equipamento e SEM Condições, para uma penalidade nunca derrubar outro requisito. Atributos/Perícias são os
    /// mesmos das listas da ficha; só entram os Atributos que existem na Ficha de Personagem (o Ego da Criatura fica de fora).
    /// </summary>
    public async Task<FichaParaRequisitos> FichaParaRequisitosAsync(TSheet sheet)
    {
        var artefatos = await ArtefatosAsync(sheet.Id);
        var totaisDeAtributo = await Db.Set<TLinhaAtributo>().Where(a => a.SheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));
        var atributos = totaisDeAtributo
            .Where(kv => Enum.TryParse<Atributo>(kv.Key.ToString(), out _))
            .ToDictionary(kv => Enum.Parse<Atributo>(kv.Key.ToString()), kv => kv.Value);

        var historico = await HistoricoAsync(sheet);
        var skills = await Db.Set<TLinhaPericia>().Where(s => s.SheetId == sheet.Id).ToDictionaryAsync(s => s.PericiaId);
        // Toda Perícia ativa entra (linha ausente = Gasto 0 e Atributo sugerido, como na lista da ficha);
        // removidas ficam de fora e o avaliador ignora requisitos sobre elas.
        var totaisDePericia = (await PericiasAtivasAsync()).ToDictionary(p => p.Id, p =>
        {
            skills.TryGetValue(p.Id, out var s);
            var modificador = SkillFormulas.Modificador(s?.Gasto ?? 0, HistoricoBonusCalculator.For(p.Id, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));
            var atributo = s?.AtributoEscolhido ?? Sugerido(p);
            return atributo is { } chosen && totaisDeAtributo.TryGetValue(chosen, out var atributoTotal)
                ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave))
                : (int?)null;
        });

        var sub = await SubAtributosAsync(sheet, artefatos, EfeitosDeCondicao.Nenhum);
        return await RetratoAsync(sheet, atributos, FichaStats.SubAtributosPorEnum(sub), totaisDePericia);
    }

    /// <summary>
    /// Os Artefatos da ficha, reduzidos a (TipoDeAlvo, Alvo, Valor) — Posses 5.b não tem equipar/desequipar Artefato:
    /// estar na ficha é estar equipado. Só Artefatos: o que as telas exibem soma também as penalidades (<see cref="ModificadoresAsync"/>).
    /// </summary>
    public async Task<List<ArtifactBonusInput>> ArtefatosAsync(Guid sheetId, EfeitosDeCondicao? efeitos = null)
    {
        var linhas = await Db.Set<TArtefato>()
            .Where(a => a.SheetId == sheetId)
            .Join(Db.Set<Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => new { a.Id, i })
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
        var armas = (await ArmasDoCatalogoAsync(sheetId))
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.IsEquipped && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Arma, x.Id) is null)).ToList();
        var escudos = (await Db.Set<TEscudo>().Where(s => s.SheetId == sheetId).Select(s => new { s.Id, s.ItemId, EmUso = s.IsEquipped }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, x.EmUso && efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Escudo, x.Id) is null)).ToList();
        var armaduras = (await Db.Set<TSlot>().Where(a => a.SheetId == sheetId && a.ItemId != null).Select(a => new { a.Id, ItemId = a.ItemId!.Value }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Armadura, x.Id) is null)).ToList();
        var artefatos = (await Db.Set<TArtefato>().Where(a => a.SheetId == sheetId).Select(a => new { a.Id, ItemId = a.ArtifactItemId }).ToListAsync())
            .Select(x => new EquipamentoDaFicha(x.ItemId, efeitos?.DesabilitadoPor(CategoriaDesabilitavel.Artefato, x.Id) is null)).ToList();
        return [.. armas, .. escudos, .. armaduras, .. artefatos];
    }

    public async Task<List<PenalidadeAtiva>> PenalidadesAtivasAsync(TSheet sheet, EfeitosDeCondicao? efeitos = null) =>
        await penalidades.AtivasAsync(await EquipamentosAsync(sheet.Id, efeitos), () => FichaParaRequisitosAsync(sheet));

    /// <summary>Artefatos + penalidades de equipamento ativas — o que as fórmulas exibidas somam no termo Artefatos.</summary>
    public async Task<List<ArtifactBonusInput>> ModificadoresAsync(TSheet sheet, EfeitosDeCondicao? efeitos = null)
    {
        var modificadores = await ArtefatosAsync(sheet.Id, efeitos);
        modificadores.AddRange(await penalidades.ComoModificadoresAsync(await PenalidadesAtivasAsync(sheet, efeitos)));
        return modificadores;
    }

    /// <summary>
    /// Os quatro totais de recurso exibidos: Artefatos + penalidades de equipamento ativas e, por cima, as Condições de
    /// <paramref name="efeitos"/>. O Status de classe Vida/Foco vem do tipo (Tabela de Vocação ou de Arquétipos).
    /// </summary>
    protected async Task<(int Vitalidade, int Foco, int Adrenalina, int Estresse)> MaximosComStatusAsync(TSheet sheet, EfeitosDeCondicao efeitos, (int Vida, int Foco) status)
    {
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        var vigorTotal = await AtributoTotalAsync(sheet.Id, Vigor, artefatos, efeitos);
        var astuciaTotal = await AtributoTotalAsync(sheet.Id, Astucia, artefatos, efeitos);
        var artefatoBonusParaAdrenalina = ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Adrenalina);
        return efeitos.NosMaximos(vigorTotal, status.Vida, astuciaTotal, status.Foco, artefatoBonusParaAdrenalina);
    }

    /// <summary>"Status de classe Vida/Foco" pela Tabela de Vocação (Vocação × Nível), pela Vocação base — não pela SubVocacao (aproximação aceita).</summary>
    protected (int Vida, int Foco) StatusDaVocacao(Vocacao? vocacao, int nivel) =>
        vocacao is not null ? VidaEArcanaPorNivel.Vocacao(Rules.Vocacoes, FichaStats.VocacaoTabelaName(vocacao.Value), nivel) : (0, 0);

    /// <summary>Os Atributos da ficha na ordem de exibição, com o total calculado com <paramref name="efeitos"/> — a conta da lista de Atributos e do resumo do encontro.</summary>
    public async Task<List<AtributoCalculado>> AtributosAsync(TSheet sheet, EfeitosDeCondicao efeitos)
    {
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        return (await Db.Set<TLinhaAtributo>().Where(a => a.SheetId == sheet.Id).ToListAsync())
            .OrderBy(a => OrdemDeExibicao(a.Atributo))
            .Select(a => new AtributoCalculado(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                efeitos.NoAtributo(a.Atributo.ToString(), AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                    artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())))))
            .ToList();
    }

    /// <summary>Todas as Perícias ativas do tipo, por nome, com o total calculado com <paramref name="efeitos"/> — a conta da lista de Perícias e do resumo do encontro.</summary>
    public async Task<List<PericiaCalculada>> PericiasAsync(TSheet sheet, EfeitosDeCondicao efeitos)
    {
        var ativas = await PericiasAtivasAsync();
        var linhas = await Db.Set<TLinhaPericia>().Where(s => s.SheetId == sheet.Id).ToDictionaryAsync(s => s.PericiaId);
        var artefatos = await ModificadoresAsync(sheet, efeitos);
        var totaisDeAtributo = await Db.Set<TLinhaAtributo>().Where(a => a.SheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => efeitos.NoAtributo(a.Atributo.ToString(), AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString()))));
        var historico = await HistoricoAsync(sheet);

        return ativas
            .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p =>
            {
                linhas.TryGetValue(p.Id, out var s);
                var gasto = s?.Gasto ?? 0;
                var atributo = s?.AtributoEscolhido ?? Sugerido(p);
                var historicoBonus = HistoricoBonusCalculator.For(p.Id, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId);
                var modificador = SkillFormulas.Modificador(gasto, historicoBonus);
                var total = atributo is not null && totaisDeAtributo.TryGetValue(atributo.Value, out var atributoTotal)
                    ? efeitos.NaPericia(p.Chave, SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave)))
                    : (int?)null;
                return new PericiaCalculada(p.Chave, p.Nome, p.Descricao, gasto, modificador, atributo?.ToString(), total, historicoBonus > 0);
            })
            .ToList();
    }

    /// <summary>
    /// Peso Total 2.b do Personagem e do NPC (docs/superpowers/archive/specs/2026-09-08-inventory-weight-and-capacity-design.md):
    /// Inventário (5.a) + Armas/Escudos DESequipados. Armaduras nunca contam — slot com ItemId está vestido. Um item de
    /// Capacidade Extra ("mochila") não soma o próprio Peso: aumenta o Peso Máximo.
    /// </summary>
    protected async Task<Carga> CargaDoInventarioAsync<TArma, TItem>(Guid id, int forca, int vigor, decimal bonusDeCarga)
        where TArma : class, IEquipavelDoCatalogo
        where TItem : class, IItemDoInventarioDaFicha
    {
        var weapons = await Db.Set<TArma>().Where(w => w.SheetId == id).Join(Db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var shields = await Db.Set<TEscudo>().Where(s => s.SheetId == id).Join(Db.Items, s => s.ItemId, i => i.Id, (s, i) => new { s.IsEquipped, i.Peso }).ToListAsync();
        var inventoryItems = await Db.Set<TItem>().Where(i => i.SheetId == id)
            .Join(Db.Set<ItemGeral>(), i => i.ItemId, g => g.Id, (i, g) => new { i.Qtd, g.Peso, g.CapacidadeExtra })
            .ToListAsync();

        var pesoAtual = inventoryItems.Where(i => CarryWeightCalculator.CountsTowardPesoAtual(i.CapacidadeExtra)).Sum(i => i.Peso * i.Qtd)
            + weapons.Where(w => !w.IsEquipped).Sum(w => w.Peso)
            + shields.Where(s => !s.IsEquipped).Sum(s => s.Peso);
        var capacidadeExtraTotal = inventoryItems.Sum(i => (i.CapacidadeExtra ?? 0m) * i.Qtd);
        return new Carga(pesoAtual, CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal, bonusDeCarga), BonusDeCargaDaCampanha: bonusDeCarga);
    }

    /// <summary>
    /// Eficiência e Dano Elemental do Personagem e do NPC: saem do valor da Afinidade correspondente já com as
    /// Condições (o mesmo que a lista 2.c exibe).
    /// </summary>
    protected async Task<(int? Eficiencia, int? Dano)> ElementalDasAfinidadesAsync<TAfinidade>(Guid id, AfinidadeElemental? afinidade, EfeitosDeCondicao efeitos, TabelaDeAfinidadesProvider tabelaDeAfinidades)
        where TAfinidade : class, IAfinidadeDaFicha
    {
        var linhasDeAfinidade = await Db.Set<TAfinidade>().Where(a => a.SheetId == id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor))
            .ToListAsync();
        var valorDaAfinidade = SubAttributeFormulas.ValorDaAfinidadeCorrespondente(afinidade, efeitos.NasAfinidades(linhasDeAfinidade));
        var linhasDaTabelaDeAfinidades = await tabelaDeAfinidades.LinhasAsync();
        return (SubAttributeFormulas.EficienciaElemental(valorDaAfinidade, linhasDaTabelaDeAfinidades), SubAttributeFormulas.DanoElemental(valorDaAfinidade, linhasDaTabelaDeAfinidades));
    }

    /// <summary>As Armas de catálogo de Personagem e NPC (todas as linhas têm ItemId).</summary>
    protected async Task<List<(Guid Id, Guid ItemId, bool IsEquipped)>> ArmasDoCatalogoAsync<TArma>(Guid sheetId)
        where TArma : class, IEquipavelDoCatalogo =>
        (await Db.Set<TArma>().Where(w => w.SheetId == sheetId).Select(w => new { w.Id, w.ItemId, w.IsEquipped }).ToListAsync())
            .Select(x => (x.Id, x.ItemId, x.IsEquipped)).ToList();

    private async Task<Historico?> HistoricoAsync(TSheet sheet) =>
        HistoricoId(sheet) is { } historicoId ? await Db.Historicos.FindAsync(historicoId) : null;

    private async Task<List<PericiaDefinicao>> PericiasAtivasAsync() =>
        (await pericias.PorIdAsync()).Values.Where(p => !p.IsDeleted && (!SoPericiasDeCriatura || p.DisponivelParaCriaturas)).ToList();

    /// <summary>O Atributo sugerido da Perícia neste tipo de ficha; nulo se o tipo não tem esse Atributo.</summary>
    private static TAtributo? Sugerido(PericiaDefinicao p) =>
        p.AtributoSugerido is { } a && Enum.TryParse<TAtributo>(a.ToString(), out var t) ? t : null;

    private async Task<int> AtributoTotalAsync(Guid sheetId, TAtributo atributo, IReadOnlyList<ArtifactBonusInput> artefatos, EfeitosDeCondicao efeitos)
    {
        var attribute = await Db.Set<TLinhaAtributo>().SingleAsync(a => a.SheetId == sheetId && a.Atributo.Equals(atributo));
        return efeitos.NoAtributo(atributo.ToString(), AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString())));
    }
}
