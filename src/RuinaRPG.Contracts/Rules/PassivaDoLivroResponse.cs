namespace RuinaRPG.Contracts.Rules;

/// <summary>
/// Uma Passiva na aba Habilidades Passivas do Livro de Regras. Categoria é o nome do enum; Requisitos já vem por extenso.
/// Vocacao é o rótulo de exibição; Classe, como cadastrada — os dois vêm dos Requisitos e servem aos filtros da aba.
/// </summary>
public record PassivaDoLivroResponse(string Id, string Nome, string Categoria, string Descricao, List<string> Requisitos, string? Vocacao = null, string? Classe = null);
