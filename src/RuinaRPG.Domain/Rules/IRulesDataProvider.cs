using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.Rules;

public interface IRulesDataProvider
{
    IReadOnlyList<LevelBonus> Niveis { get; }
    IReadOnlyList<VocacaoProgressao> Vocacoes { get; }
    IReadOnlyList<ClasseProgressao> Classes { get; }
    IReadOnlyList<ArquetipoProgressao> Arquetipos { get; }
    IReadOnlyList<CirculoGrauPorEap> CirculoGrauPorEap { get; }
    IReadOnlyList<XpPorNivel> XpPorNivel { get; }
    IReadOnlyList<EapPorNivel> EapPorNivel { get; }
    IReadOnlyList<GraduacaoEfeito> Efeitos { get; }
    IReadOnlyList<RegraEntry> Regras { get; }
}
