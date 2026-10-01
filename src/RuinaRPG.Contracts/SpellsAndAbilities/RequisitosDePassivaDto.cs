namespace RuinaRPG.Contracts.SpellsAndAbilities;

/// <summary>Um item das listas de requisito: Alvo é o nome do enum (Atributo, SubAtributo) ou a Chave de uma Perícia.</summary>
public record RequisitoMinimoDto(string Alvo, int Minimo);

/// <summary>Requisitos de uma Passiva. Campo nulo/vazio = não é requisito. Enums trafegam como texto.</summary>
public record RequisitosDePassivaDto(
    int? Nivel = null,
    string? Vocacao = null,
    string? Classe = null,
    string? Linhagem = null,
    string? Variante = null,
    int? Graduacao = null,
    bool? CoracaoDeMana = null,
    string? Afinidade = null,
    string? Estrela = null,
    string? HistoricoId = null,
    List<RequisitoMinimoDto>? Atributos = null,
    List<RequisitoMinimoDto>? SubAtributos = null,
    List<RequisitoMinimoDto>? Pericias = null);
