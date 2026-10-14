namespace RuinaRPG.Contracts.Condicoes;

/// <summary>Chaves fixas de ícone de uma Condição; o cliente mapeia cada uma para um ícone MudBlazor.</summary>
public static class CondicaoIcones
{
    public static readonly IReadOnlyList<string> Chaves =
    [
        "Veneno", "Fogo", "Gelo", "Atordoado", "Sangramento", "Escudo", "Lentidao", "Cegueira", "Medo", "Bencao",
        "Maldicao", "Sono", "Enraizado", "Enfraquecido", "Fortalecido", "Queimadura", "Eletrificado", "Silenciado",
        "Invisivel", "Marcado"
    ];
}
