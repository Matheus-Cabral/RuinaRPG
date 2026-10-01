using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// O que uma ficha tem para comparar com os requisitos de uma Passiva.
/// <para><see cref="TemIdentidadeDePersonagem"/> é falso na Criatura, que não tem Vocação, Classe, Linhagem,
/// Variante, Grau/Círculo, Coração de Mana, Estrela nem Histórico — requisitos nesses campos são ignorados.
/// Nos dicionários, só entram as chaves que aquele tipo de ficha tem: uma chave ausente é ignorada; um
/// valor presente mas nulo (Perícia sem atributo escolhido, sem Total) não cumpre. As Perícias são chaveadas pelo Id
/// da tabela Pericias.</para>
/// </summary>
public sealed record FichaParaRequisitos(
    bool TemIdentidadeDePersonagem,
    int Nivel,
    Vocacao? Vocacao,
    string? Classe,
    Linhagem? Linhagem,
    Variante? Variante,
    int Graduacao,
    bool PossuiCoracaoDeMana,
    AfinidadeElemental? Afinidade,
    Estrela? Estrela,
    Guid? HistoricoId,
    IReadOnlyDictionary<Atributo, int> Atributos,
    IReadOnlyDictionary<SubAtributo, int> SubAtributos,
    IReadOnlyDictionary<int, int?> Pericias);
