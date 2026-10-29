using System.Net.Http.Json;
using RuinaRPG.Contracts.CharacterSheets;
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

    /// <summary>Os Sub-Atributos, relidos com o Combate (abas 2 e 3); a aba Posses mostra o Peso deles.</summary>
    public SubAttributesResponse? SubAtributos { get; set; }

    /// <summary>
    /// A ficha está no cemitério (Ficha de Personagem R0010): as abas ficam desabilitadas em cascata e nada é salvo. Vem
    /// com a ficha (<see cref="DefinirCemiterio"/>); onde o tipo não tem cemitério, sempre falso.
    /// </summary>
    public bool SomenteLeitura { get; private set; }

    /// <summary>Os motivos do cemitério como o servidor os manda (<c>Morte</c>, <c>RemovidoDaCampanha</c>, <c>ContaInativa</c>).</summary>
    public IReadOnlyList<string> MotivosDoCemiterio { get; private set; } = [];

    /// <summary>A caixa "Morto" do GM: marcada quando um dos motivos é a morte.</summary>
    public bool Morto => MotivosDoCemiterio.Contains("Morte");

    /// <summary>O rótulo de um motivo do cemitério, como o cliente o mostra.</summary>
    public static string RotuloDoMotivo(string motivo) => motivo switch
    {
        "Morte" => "Personagem morto",
        "RemovidoDaCampanha" => "Jogador removido da campanha",
        "ContaInativa" => "Conta do jogador inativa",
        _ => motivo,
    };

    /// <summary>
    /// O estado do cemitério que veio com a ficha. Só leitura fecha a escolha pendente de Característica Racial (um
    /// diálogo fora das abas, que a cascata não alcança).
    /// </summary>
    public void DefinirCemiterio(bool noCemiterio, IReadOnlyList<string>? motivos)
    {
        if (!Tipo.TemCemiterio)
            return;
        SomenteLeitura = noCemiterio;
        MotivosDoCemiterio = motivos ?? [];
        if (SomenteLeitura)
            EscolhaRacialAberta = false;
    }

    /// <summary>A recusa de escrita do servidor numa ficha no cemitério (409).</summary>
    public const string RecusaDoCemiterio = "Esta ficha está no cemitério e não pode ser editada.";

    /// <summary>
    /// A ficha acabou de entrar no cemitério (<see cref="RecarregarCemiterioAsync"/>): a página relê o formulário assim
    /// que o auto-save terminar, para que nada recusado fique na tela. Um evento, não um gancho obrigatório: só a página
    /// de um tipo com cemitério o assina.
    /// </summary>
    public event Action? EntrouNoCemiterio;

    /// <summary>
    /// Relê só o estado do cemitério (o aviso em tempo real, a caixa "Morto"); o alerta de erro fica como está, exceto a
    /// recusa do cemitério, que sai quando a ficha sai do cemitério.
    /// </summary>
    public async Task RecarregarCemiterioAsync()
    {
        if (!Tipo.TemCemiterio)
            return;
        var response = await Http.GetAsync(Tipo.Url(SheetId));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível atualizar a ficha.");
            return;
        }
        var ficha = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        if (ficha is not null)
        {
            var antes = SomenteLeitura;
            DefinirCemiterio(ficha.NoCemiterio, ficha.MotivosDoCemiterio);
            if (!antes && SomenteLeitura)
                EntrouNoCemiterio?.Invoke();
            if (antes && !SomenteLeitura && Erro == RecusaDoCemiterio)
                Erro = null;
        }
        NotificarAlteracao();
    }

    /// <summary>Algo mudou fora de um evento da própria página (uma aba filha): a página re-renderiza.</summary>
    public event Action? Alterado;

    public void NotificarAlteracao() => Alterado?.Invoke();

    /// <summary>Mostra <paramref name="erro"/> no alerta da página (um erro de qualquer aba ou seção filha).</summary>
    public void MostrarErro(string erro)
    {
        Erro = erro;
        NotificarAlteracao();
    }
}
