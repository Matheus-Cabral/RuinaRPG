namespace RuinaRPG.Contracts.Condicoes;

/// <summary>
/// Chaves fixas de ícone de uma Condição; o cliente mapeia cada uma para um ícone MudBlazor (CondicaoIcone, com um
/// teste que mantém as duas listas iguais). Uma chave nunca muda nem sai: só se acrescentam novas no fim.
/// </summary>
public static class CondicaoIcones
{
    public static readonly IReadOnlyList<string> Chaves =
    [
        "Veneno", "Fogo", "Gelo", "Atordoado", "Sangramento", "Escudo", "Lentidao", "Cegueira", "Medo", "Bencao",
        "Maldicao", "Sono", "Enraizado", "Enfraquecido", "Fortalecido", "Queimadura", "Eletrificado", "Silenciado",
        "Invisivel", "Marcado",
        // Acrescentadas em 1.5.0, sempre depois das de cima (as Condições guardadas usam a chave).
        "Agua", "Terra", "Vento", "Trovao", "Luz", "Trevas", "Acido", "Magma", "Tempestade", "Tornado", "Inundado",
        "Congelado", "Cristalizado", "Petrificado", "Caido", "Agarrado", "Preso", "Imobilizado", "Paralisado",
        "Acorrentado", "Emaranhado", "Exausto", "Revigorado", "Faminto", "Sedento", "Doente", "Ferido", "Inconsciente",
        "Sonolento", "Regenerando", "Curando", "Imune", "Infestado", "Desarmado", "Confuso", "Encantado", "Enfurecido",
        "Panico", "Calmo", "Inspirado", "Focado", "Distraido", "Desorientado", "Enlouquecido", "Dominado",
        "Hipnotizado", "Provocado", "Surdo", "Mudo", "VisaoNoEscuro", "VisaoAgucada", "Acelerado", "Agil", "Protegido",
        "Resistente", "Invulneravel", "Vulneravel", "Exposto", "DefesaQuebrada", "Protegendo", "Voando", "Camuflado",
        "Escondido", "Encolhido", "Aumentado", "Transformado", "Possuido", "Sorte", "Empoderado", "ArmaEncantada",
        "Dissipado", "ManaDrenada", "Runa", "Selo", "Pacto", "Consagrado", "Condenado", "Cacado", "Concentrando",
        "Carregando"
    ];
}
