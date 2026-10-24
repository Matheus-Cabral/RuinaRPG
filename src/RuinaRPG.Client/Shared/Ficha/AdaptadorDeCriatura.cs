using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// Os records da Criatura que continuam separados no fio (E.2: o JSON não muda) viram os records compartilhados que
/// as abas mostram. Campo a campo; <c>DurabilidadeMaximo</c> vira <c>DurabilidadeMaxima</c>, e o que a Criatura não
/// tem recebe um valor neutro: <c>Peso</c> 0 (sem coluna Peso na Criatura), <c>ContaHistorico</c> false (o
/// Modificador sem negrito), <c>IsRacial</c> false (Criatura não tem Característica Racial).
/// </summary>
public static class AdaptadorDeCriatura
{
    /// <summary>
    /// O ataque natural (manual) mantém <c>ItemId</c> nulo, embora o record compartilhado o declare não anulável: é por
    /// ele que a linha mostra "Ataque natural" em vez da Durabilidade (a aba testa <c>ItemId is null</c>). A Durabilidade
    /// nula (o ataque natural não tem, e uma arma do Catálogo também pode não ter) fica 0 aqui; esse 0 não vai para a
    /// tela: <see cref="LinhaDeArma"/> guarda a nula, que a tela mostra em branco (R-E2).
    /// </summary>
    public static WeaponResponse ParaArma(CreatureWeaponResponse arma) => new(
        Id: arma.Id,
        ItemId: arma.ItemId!,
        Nome: arma.Nome,
        TipoDeDano: arma.TipoDeDano,
        Alcance: arma.Alcance,
        Dados: arma.Dados,
        Dano: arma.Dano,
        Critico: arma.Critico,
        Rank: arma.Rank,
        Peso: 0m,
        IsEquipped: arma.IsEquipped,
        DurabilidadeAtual: arma.DurabilidadeAtual ?? 0,
        DurabilidadeMaxima: arma.DurabilidadeMaximo ?? 0,
        ImageUrl: arma.ImageUrl,
        Descricao: arma.Descricao,
        Inquebravel: arma.Inquebravel,
        Requisitos: arma.Requisitos,
        RequisitosPendentes: arma.RequisitosPendentes,
        Penalidade: arma.Penalidade,
        OutrasPenalidades: arma.OutrasPenalidades,
        DesabilitadoPor: arma.DesabilitadoPor);

    public static ArmorSlotResponse ParaSlotDeArmadura(CreatureArmorSlotResponse slot) => new(
        Slot: slot.Slot,
        ItemId: slot.ItemId,
        Nome: slot.Nome,
        Categoria: slot.Categoria,
        Defesa: slot.Defesa,
        RF: slot.RF,
        RM: slot.RM,
        Peso: slot.Peso,
        DurabilidadeAtual: slot.DurabilidadeAtual,
        DurabilidadeMaxima: slot.DurabilidadeMaximo,
        ImageUrl: slot.ImageUrl,
        Descricao: slot.Descricao,
        Inquebravel: slot.Inquebravel,
        Requisitos: slot.Requisitos,
        RequisitosPendentes: slot.RequisitosPendentes,
        Penalidade: slot.Penalidade,
        OutrasPenalidades: slot.OutrasPenalidades,
        DesabilitadoPor: slot.DesabilitadoPor);

    public static SkillResponse ParaPericia(CreatureSkillResponse pericia) => new(
        Pericia: pericia.Pericia,
        Gasto: pericia.Gasto,
        Modificador: pericia.Modificador,
        AtributoEscolhido: pericia.AtributoEscolhido,
        Total: pericia.Total,
        ContaHistorico: false,
        Nome: pericia.Nome,
        Descricao: pericia.Descricao);

    public static TraitDaFichaResponse ParaCaracteristica(CreatureTraitResponse caracteristica) => new(
        Id: caracteristica.Id,
        TraitId: caracteristica.TraitId,
        Nome: caracteristica.Nome,
        Descricao: caracteristica.Descricao,
        Custo: caracteristica.Custo,
        Polaridade: caracteristica.Polaridade,
        Especificacao: caracteristica.Especificacao,
        RequerEspecificacao: caracteristica.RequerEspecificacao,
        IsRacial: false);

    /// <summary>Uma lista que o JSON omitiu vira uma lista vazia (a aba mostra nada, como antes).</summary>
    public static TraitsListResponse ParaCaracteristicas(CreatureTraitsListResponse lista) => new(
        Positivas: lista.Positivas?.Select(ParaCaracteristica).ToList() ?? new(),
        TotalPositivas: lista.TotalPositivas,
        Negativas: lista.Negativas?.Select(ParaCaracteristica).ToList() ?? new(),
        TotalNegativas: lista.TotalNegativas,
        PontosDisponiveis: lista.PontosDisponiveis,
        PontosDisponiveisNegativas: lista.PontosDisponiveisNegativas);
}
