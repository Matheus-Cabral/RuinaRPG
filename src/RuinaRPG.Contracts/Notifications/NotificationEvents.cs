namespace RuinaRPG.Contracts.Notifications;

/// <summary>Event names of the notifications hub, shared by the API (sender) and the client (listener).</summary>
public static class NotificationEvents
{
    public const string SecretNoteReceived = "SecretNoteReceived";
    public const string SecretNotesChanged = "SecretNotesChanged";
    public const string CondicoesAlteradas = "CondicoesAlteradas";
    public const string FichaNoCemiterioAlterada = "FichaNoCemiterioAlterada";

    /// <summary>O jogador foi removido de uma campanha (também ao ter a conta desativada ou excluída) ou foi adicionado a uma, inclusive ao voltar a ela (Campanha R0018): o painel aberto recarrega as campanhas e as fichas.</summary>
    public const string CampanhasDoJogadorAlteradas = "CampanhasDoJogadorAlteradas";
}
