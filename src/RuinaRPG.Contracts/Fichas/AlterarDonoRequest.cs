namespace RuinaRPG.Contracts.Fichas;

/// <summary>"Alterar dono" de uma ficha de NPC/Criatura do GM: o id do novo dono (um membro ativo), ou null para devolvê-la ao GM.</summary>
public record AlterarDonoRequest(string? DonoId);
