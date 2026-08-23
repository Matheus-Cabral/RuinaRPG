using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSheet
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? ImageId { get; set; }
    public string? Nome { get; set; }
    public string? Raca { get; set; }
    public Arquetipo? Arquetipo { get; set; }
    public string? SubArquetipo { get; set; }
    public AfinidadeElemental? Afinidade { get; set; }
    public string? Propriedade { get; set; }
    public Rank? Rank { get; set; }
    public int Nivel { get; set; } = 1;
    public int ExperienciaAtual { get; set; }
    public int PontosDeIgnicao { get; set; }
    public int VitalidadeAtual { get; set; }
    public int FocoAtual { get; set; }
    public int AdrenalinaAtual { get; set; }
    public Cobertura Cobertura { get; set; }
    public int? LastDismissedLevelUpLevel { get; set; }
}
