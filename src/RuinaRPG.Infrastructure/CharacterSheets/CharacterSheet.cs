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
    public int Circulo { get; set; }
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

    // R0002
    public int? LastDismissedLevelUpLevel { get; set; }
}
