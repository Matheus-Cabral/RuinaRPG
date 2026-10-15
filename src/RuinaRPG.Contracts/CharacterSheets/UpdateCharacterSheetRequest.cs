namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>
/// Os <c>*AtualCarregad*</c> são os valores atuais que o formulário tinha ao carregar a ficha: o servidor só grava um
/// Atual quando o enviado difere do carregado (o jogador de fato o editou). Sem eles (cliente antigo), grava sempre.
/// </summary>
public record UpdateCharacterSheetRequest(
    string? ImageId,
    string? Nome,
    string? Linhagem,
    string? Variante,
    string? Vocacao,
    string? SubVocacao,
    string? Afinidade,
    string? Propriedade,
    bool PossuiCoracaoDeMana,
    int ExperienciaAtual,
    int EAPAtual,
    int NucleosRankF,
    int NucleosRankE,
    int NucleosRankD,
    int NucleosRankC,
    int NucleosRankB,
    int NucleosRankA,
    int NucleosRankS,
    int PontosDeIgnicaoAtual,
    int PontosDeIgnicaoBonusManual,
    int VitalidadeAtual,
    int FocoAtual,
    int AdrenalinaAtual,
    int EstresseAtual,
    string Cobertura,
    int Ciclos,
    int PontosDePericiaBonusCritico,
    int? ArcaRolada,
    string? Estrela,
    int SinaAtual,
    string? HistoricoId,
    string? EquipmentKitId,
    int AfinidadeAdicional,
    int? VitalidadeAtualCarregada = null,
    int? FocoAtualCarregado = null,
    int? AdrenalinaAtualCarregada = null,
    int? EstresseAtualCarregado = null);
