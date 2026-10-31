namespace RuinaRPG.Contracts.Rules;

/// <summary>Liga (<see cref="Visivel"/> = true) ou desliga um Caminho/Sub-Elemento de uma Vocação mágica ou Classe.</summary>
public record AlterarVisibilidadeDeElementoRequest(string Origem, string Chave, int ElementoId, bool Visivel);
