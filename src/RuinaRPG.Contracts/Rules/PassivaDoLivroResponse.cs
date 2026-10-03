namespace RuinaRPG.Contracts.Rules;

/// <summary>Uma Passiva na aba Habilidades Passivas do Livro de Regras. Categoria é o nome do enum; Requisitos já vem por extenso.</summary>
public record PassivaDoLivroResponse(string Id, string Nome, string Categoria, string Descricao, List<string> Requisitos);
