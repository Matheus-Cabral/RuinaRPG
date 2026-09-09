using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSheet
{
    // 1.a Identidade
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ImageId { get; set; }
    public string? Nome { get; set; }
    public Linhagem? Linhagem { get; set; }
    public Variante? Variante { get; set; }
    public Vocacao? Vocacao { get; set; }
    public string? SubVocacao { get; set; }
    public AfinidadeElemental? Afinidade { get; set; }
    public string? Propriedade { get; set; }

    // 1.b Nível e Progressão
    public int Nivel { get; set; } = 1;

    /// <summary>
    /// Currently unused — nothing writes this column, so it is always 0. The authoritative
    /// value is <c>Graduacao</c> on <c>CharacterSheetResponse</c>, computed on read by
    /// <see cref="RuinaRPG.Domain.CharacterSheets.GraduacaoCalculator"/> from EAPAtual/Vocacao.
    /// Do not read this property expecting a live value.
    /// </summary>
    public int Circulo { get; set; }

    /// <summary>
    /// Currently unused — nothing writes this column, so it is always 0. The authoritative
    /// value is <c>Graduacao</c> on <c>CharacterSheetResponse</c>, computed on read by
    /// <see cref="RuinaRPG.Domain.CharacterSheets.GraduacaoCalculator"/> from EAPAtual/Vocacao.
    /// Do not read this property expecting a live value.
    /// </summary>
    public int Grau { get; set; }
    public bool PossuiCoracaoDeMana { get; set; }
    public int ExperienciaAtual { get; set; }
    public int EAPAtual { get; set; }
    public int NucleosRankF { get; set; }
    public int NucleosRankE { get; set; }
    public int NucleosRankD { get; set; }
    public int NucleosRankC { get; set; }
    public int NucleosRankB { get; set; }
    public int NucleosRankA { get; set; }
    public int NucleosRankS { get; set; }
    public int PontosDeIgnicaoAtual { get; set; }

    /// <summary>
    /// Currently unused — nothing writes this column, so it stays at whatever it was before
    /// Pontos de Ignição Total became computed. The authoritative value is
    /// <c>PontosDeIgnicaoTotal</c> on <c>CharacterSheetResponse</c>, computed on read by
    /// <see cref="RuinaRPG.Domain.CharacterSheets.PontosDeIgnicaoCalculator"/> from Nível and
    /// <see cref="PontosDeIgnicaoBonusManual"/>. Do not read this property expecting a live value.
    /// </summary>
    public int PontosDeIgnicaoTotal { get; set; }

    /// <summary>Extra PI the GM grants on top of the level-derived total (1.b, "o GM também pode
    /// conceder PI para os jogadores acrescentarem neste contador").</summary>
    public int PontosDeIgnicaoBonusManual { get; set; }

    /// <summary>Skill points earned from rolling max on the dice (§2 of Ruína RPG - Sistema
    /// Básico) — subtracted from the Perícia budget's Gasto Total (2.d) so the level-derived
    /// budget doesn't flag legitimately-earned points as over budget.</summary>
    public int PontosDePericiaBonusCritico { get; set; }

    // 1.c Recursos (Atual only — see plan Architecture note on máximo)
    public int VitalidadeAtual { get; set; }
    public int FocoAtual { get; set; }
    public int AdrenalinaAtual { get; set; }
    public int EstresseAtual { get; set; }

    // Referenced by later tabs (2.b Cobertura; 5.a Ciclos) but stored on the root per Modelo de Dados
    public Cobertura Cobertura { get; set; }
    public int Ciclos { get; set; }

    // 1-18, matches "the tabela de Arcas" (Ruína RPG - Sistema Básico.md §7, Sinir/Laonir's
    // "Role 1d18 na tabela de Arcas") — only meaningful when Linhagem is Humano, but not
    // restricted at the schema level (the GM/racial-ability endpoint enforces that).
    public int? ArcaRolada { get; set; }

    // R0002
    public int? LastDismissedLevelUpLevel { get; set; }
}
