namespace RuinaRPG.Contracts.Auth;

public record RegisterGmRequest(string Nickname, string Email, string Senha, string ConfirmacaoSenha);
