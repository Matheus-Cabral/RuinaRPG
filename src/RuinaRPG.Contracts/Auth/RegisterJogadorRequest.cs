namespace RuinaRPG.Contracts.Auth;

public record RegisterJogadorRequest(string Nickname, string Email, string Senha, string ConfirmacaoSenha, string CodigoDeAcesso);
