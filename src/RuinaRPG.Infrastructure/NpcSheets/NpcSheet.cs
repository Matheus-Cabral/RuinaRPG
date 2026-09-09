using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcSheet
{
    // 1.a Identidade
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public Guid? OwnerId { get; set; }
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
    /// value is <c>Graduacao</c> on <c>NpcSheetResponse</c>, computed on read by
    /// <see cref="RuinaRPG.Domain.CharacterSheets.GraduacaoCalculator"/> from EAPAtual/Vocacao.
    /// Do not read this property expecting a live value.
    /// </summary>
    public int Circulo { get; set; }

    /// <summary>
    /// Currently unused — nothing writes this column, so it is always 0. The authoritative
    /// value is <c>Graduacao</c> on <c>NpcSheetResponse</c>, computed on read by
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
    public int PontosDeIgnicaoTotal { get; set; }

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

    /// <summary>
    /// Currently unused — nothing writes this column for NPCs, so it is always null. The
    /// level-up-notice feature is deliberately Personagem-only per spec R0002; nothing on
    /// the NPC code path ever reads or writes this property.
    /// Do not read this property expecting a live value.
    /// </summary>
    public int? LastDismissedLevelUpLevel { get; set; }
}
