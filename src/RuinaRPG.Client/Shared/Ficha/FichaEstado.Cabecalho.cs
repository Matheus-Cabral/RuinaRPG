using System.Net.Http.Json;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// O alto da página: o aviso de Novo Nível (todo tipo) e a escolha pendente da Característica Racial (onde
/// <see cref="TipoDeFichaCliente.TemTracosRaciais"/>). A página decide quando reler (carga, depois de salvar); as ações
/// do <see cref="CabecalhoDaFicha"/> e do <see cref="EscolhaRacialDialog"/> terminam em um <see cref="NotificarAlteracao"/>.
/// </summary>
public sealed partial class FichaEstado
{
    /// <summary>Os bônus do Novo Nível; vazio esconde o aviso.</summary>
    public List<string> BonusDeNovoNivel { get; set; } = new();

    public async Task CarregarAvisoDeNovoNivelAsync()
    {
        var response = await Http.GetAsync(Tipo.Url(SheetId, "level-up-notice"));
        if (response.IsSuccessStatusCode)
            BonusDeNovoNivel = (await response.Content.ReadFromJsonAsync<LevelUpNoticeResponse>())!.BonusTexts;
    }

    public async Task FecharAvisoDeNovoNivelAsync()
    {
        await Http.PostAsync(Tipo.Url(SheetId, "dismiss-level-up-notice"), null);
        BonusDeNovoNivel.Clear();
        NotificarAlteracao();
    }

    // ---------------- Característica Racial ----------------
    // Ruína RPG - Sistema Básico §7: toda Variante concede uma Característica Gratuita (escolha de verdade) e, algumas,
    // uma Obrigatória (nenhuma, uma fixa ou também uma escolha) — ver RacialTraitLookup/RacialTraitChoiceResolver. O
    // diálogo abre sempre que a Variante atual ainda tem isso pendente; fechar sem escolher só o reabre na próxima carga.

    public PendingRacialTraitChoiceResponse? EscolhaRacialPendente { get; private set; }

    public bool EscolhaRacialAberta { get; set; }

    public string? GratuitaEscolhida { get; set; }

    public string? ObrigatoriaEscolhida { get; set; }

    public string? EspecificacaoDaGratuita { get; set; }

    public string? EspecificacaoDaObrigatoria { get; set; }

    public async Task CarregarEscolhaRacialPendenteAsync()
    {
        EscolhaRacialPendente = await Http.GetFromJsonAsync<PendingRacialTraitChoiceResponse>(Tipo.Url(SheetId, "racial-traits/pending"));
        EscolhaRacialAberta = EscolhaRacialPendente?.Pending == true;
        GratuitaEscolhida = null;
        ObrigatoriaEscolhida = null;
        EspecificacaoDaGratuita = null;
        EspecificacaoDaObrigatoria = null;
    }

    /// <summary>Grava a escolha e relê a pendência e as Características. Sem a Gratuita (o botão fica desligado), nada.</summary>
    public async Task ConfirmarEscolhaRacialAsync()
    {
        if (GratuitaEscolhida is null)
            return;

        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "racial-traits/resolve"),
            new ResolveRacialTraitChoiceRequest(GratuitaEscolhida, ObrigatoriaEscolhida, EspecificacaoDaGratuita, EspecificacaoDaObrigatoria));
        if (!response.IsSuccessStatusCode)
        {
            Erro = await response.Content.ReadErrorMessageAsync() ?? "Não foi possível registrar a Característica Racial escolhida.";
            NotificarAlteracao();
            return;
        }

        EscolhaRacialAberta = false;
        await CarregarEscolhaRacialPendenteAsync();
        await CarregarCaracteristicasAsync();
        NotificarAlteracao();
    }

    /// <summary>"Decidir depois": fecha sem gravar.</summary>
    public void AdiarEscolhaRacial()
    {
        EscolhaRacialAberta = false;
        NotificarAlteracao();
    }
}
