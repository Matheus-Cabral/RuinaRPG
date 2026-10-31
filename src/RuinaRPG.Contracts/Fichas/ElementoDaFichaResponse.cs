namespace RuinaRPG.Contracts.Fichas;

/// <summary>
/// Um elemento do catálogo na lista de Afinidades da ficha: <see cref="Tipo"/> é <c>Base</c>, <c>Caminho</c> ou
/// <c>SubElemento</c>; <see cref="Teto"/> nulo = sem limite. <see cref="Bloqueado"/> com o <see cref="Motivo"/> quando
/// o elemento não pode subir (requisito não cumprido, fora da Vocação/Classe, acima do teto). <see cref="PontosComCondicoes"/>
/// é o valor exibido com as Condições ativas (Ficha de Personagem R0009); o que se edita é sempre <see cref="Pontos"/>.
/// </summary>
public record ElementoDaFichaResponse(int Id, string Nome, string Tipo, int? Teto, int Pontos, int? Experiencia, bool Bloqueado, string? Motivo, int PontosComCondicoes);
