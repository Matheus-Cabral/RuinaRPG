using RuinaRPG.Contracts.Images;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// O estado de uma página de ficha que as abas compartilhadas recebem em cascata (<c>CascadingValue</c>). A página
/// cria um por ficha, assina <see cref="Alterado"/> (re-renderiza) e continua dona da coordenação: auto-save,
/// recarga adiada, Condições, SignalR. Este é o núcleo; os dados e as ações de cada aba entram como partes
/// (<c>FichaEstado.&lt;Aba&gt;.cs</c>) quando a aba se torna compartilhada.
/// </summary>
public sealed partial class FichaEstado(HttpClient http, TipoDeFichaCliente tipo, string sheetId)
{
    public TipoDeFichaCliente Tipo { get; } = tipo;

    public string SheetId { get; } = sheetId;

    public HttpClient Http { get; } = http;

    /// <summary>A mensagem do alerta de erro da página (<c>DismissibleAlert</c>); <c>null</c> esconde o alerta.</summary>
    public string? Erro { get; set; }

    /// <summary>Sobe a cada mudança que mexe nas penalidades de equipamento: o alerta das penalidades relê.</summary>
    public int PenalidadesVersao { get; set; }

    /// <summary>Sobe a cada aviso de Condições alteradas: o alerta das Condições e as Passivas releem.</summary>
    public int CondicoesVersao { get; set; }

    /// <summary>Quem abriu a ficha é o GM (os bancos e as galerias são os dele, não os da campanha).</summary>
    public bool IsGmCaller { get; set; }

    /// <summary>A campanha da ficha (vem com a ficha); com ela o jogador busca o que a campanha liberou.</summary>
    public string? CampaignId { get; set; }

    /// <summary>A galeria de imagens: as do GM, ou as liberadas na campanha para o jogador.</summary>
    public List<ImageSummaryResponse> MinhasImagens { get; set; } = new();

    /// <summary>Algo mudou fora de um evento da própria página (uma aba filha): a página re-renderiza.</summary>
    public event Action? Alterado;

    public void NotificarAlteracao() => Alterado?.Invoke();
}
