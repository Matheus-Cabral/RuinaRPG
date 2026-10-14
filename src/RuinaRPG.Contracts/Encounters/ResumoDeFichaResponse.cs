using RuinaRPG.Contracts.Condicoes;

namespace RuinaRPG.Contracts.Encounters;

/// <summary>Um número do resumo: o valor com as Condições do participante e o mesmo valor sem elas (o cliente destaca o que mudou).</summary>
public record ValorDoResumo(int Valor, int ValorSemCondicoes);

public record ResumoDeValor(string Nome, ValorDoResumo Valor);

/// <param name="Atual">PV/PF/PA do participante; null quando a ficha de origem não tem o recurso.</param>
public record ResumoDeRecurso(string Nome, int? Atual, ValorDoResumo Total);

public record ResumoDePericia(string Chave, string Nome, int Gasto, string? Atributo, ValorDoResumo Total);

/// <param name="Categoria">Arma, Armadura, Escudo, Artefato, Magia, Habilidade, Passiva ou Runa.</param>
/// <param name="DesabilitadoPor">Nome da Condição que desabilita a linha; null quando está em uso.</param>
public record ResumoDeLinha(string Categoria, string Nome, string? DesabilitadoPor);

public record ResumoDeCaracteristica(string Nome, string Polaridade, string? Especificacao);

/// <summary>
/// O resumo de uma ficha de NPC ou Criatura como participante do encontro, com as Condições do participante aplicadas
/// (leitura; não escreve nada). <c>Tipo</c> é "Npc" ou "Criatura"; <c>Rank</c> só existe na Criatura.
/// </summary>
public record ResumoDeFichaResponse(
    string FichaId,
    string Tipo,
    string Nome,
    string? ImagemUrl,
    int Nivel,
    string? Rank,
    List<ResumoDeValor> Atributos,
    List<ResumoDeValor> SubAtributos,
    List<ResumoDeRecurso> Recursos,
    List<ResumoDePericia> Pericias,
    List<ResumoDeLinha> Equipamentos,
    List<ResumoDeLinha> MagiasEHabilidades,
    List<ResumoDeCaracteristica> Caracteristicas,
    List<CondicaoAtivaNaFichaResponse> Condicoes);
