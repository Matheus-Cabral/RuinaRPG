using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A base das três páginas de ficha (<c>Pages/FichaDe*.razor</c>, 2.1.0 C.1): a coordenação que vale para todo tipo —
/// a carga, o auto-save, a recarga pelas Condições (aviso em tempo real), o alerta de erro e os salvamentos com endpoint
/// próprio (Pontos de Ignição, Nível, Experiência). O que muda entre os tipos vem dos fatos de
/// <see cref="TipoDeFichaCliente"/>, nunca do tipo em si. A página guarda só o que é dela: o record da ficha
/// (<see cref="PreencherFormulario"/>), o PUT da ficha (<see cref="SaveIfValidAsync"/>), as abas e a coordenação exclusiva
/// de um tipo (o cemitério do Personagem).
/// </summary>
/// <typeparam name="TFicha">O record que <c>GET {rota}/{id}</c> devolve.</typeparam>
public abstract class PaginaDeFicha<TFicha> : ComponentBase, IDisposable where TFicha : class
{
    [Parameter] public string SheetId { get; set; } = "";

    [Inject] protected HttpClient Http { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected SecretNoteNotifier Notifier { get; set; } = default!;
    [Inject] protected PericiaCatalogo Pericias { get; set; } = default!;
    [Inject] protected AuthenticationStateProvider AuthProvider { get; set; } = default!;

    /// <summary>O formulário da página (o tipo vem com ele; o valor inicial de cada campo antes da carga é da página).</summary>
    protected abstract FormularioDaFicha Formulario { get; }

    protected TipoDeFichaCliente Tipo => Formulario.Tipo;

    /// <summary>O estado que as abas compartilhadas recebem em cascata: a página e as abas leem e escrevem o mesmo.</summary>
    protected FichaEstado Estado { get; private set; } = default!;

    protected AutoSaveCoordinator AutoSave { get; } = new();

    /// <summary>
    /// Condições mudam no servidor (ação do GM no encontro): a ficha se recarrega sem salvar nada e sem trocar o campo
    /// que está sendo editado.
    /// </summary>
    protected RecargaAdiada Recarga { get; private set; } = default!;

    protected List<BreadcrumbItem> Trilha => Estado.Trilha(Formulario.Nome);

    protected override void OnInitialized()
    {
        Estado = new FichaEstado(Http, Tipo, SheetId);
        Estado.RecarregarPontosDeIgnicao = RecarregarPontosDeIgnicaoAsync;
        Estado.Alterado += OnEstadoAlterado;
        AutoSave.StateChanged += StateHasChangedFromAutoSave;
        Recarga = new RecargaAdiada(RecarregarPorCondicoesAsync, () => JS.InvokeAsync<bool>("ruinaFoco.campoEmEdicao").AsTask(), () => AutoSave.Ocupado,
            aoFalhar: falha => _ = AvisarFalhaDaRecargaAsync());
        Notifier.CondicoesAlteradas += OnCondicoesAlteradas;
    }

    public virtual void Dispose()
    {
        Estado.Alterado -= OnEstadoAlterado;
        AutoSave.StateChanged -= StateHasChangedFromAutoSave;
        Notifier.CondicoesAlteradas -= OnCondicoesAlteradas;
        Recarga.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        await Pericias.CarregarAsync();
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        Estado.IsGmCaller = authState.User.IsInRole("GM");

        // LoadSheetAsync sets the CampaignId, which LoadImagesAsync needs (a Jogador's image list is scoped to that
        // campaign). LoadSheetAsync also sets the form's ImageId directly from the sheet's own ImageId, so it no longer
        // needs to be resolved by matching the gallery.
        await LoadSheetAsync();
        await LoadImagesAsync();

        await Task.WhenAll(CargasDasAbas());
    }

    /// <summary>O que as abas do tipo leem ao abrir a página, cada leitura onde o tipo tem a parte que a usa.</summary>
    private IEnumerable<Task> CargasDasAbas()
    {
        yield return Estado.CarregarAvisoDeNovoNivelAsync();
        yield return Estado.CarregarAtributosECombateAsync();
        if (Tipo.TemLinhagemEVocacao)
        {
            yield return Estado.CarregarHabilidadeRacialAsync();
            yield return Estado.CarregarVariantesLiberadasAsync();
        }
        yield return Estado.CarregarMagiasAsync();
        if (Tipo.TemRunas)
            yield return Estado.CarregarRunasAsync();
        yield return Estado.CarregarMaestriasAsync();
        if (Tipo.TemInventario)
            yield return Estado.CarregarInventarioAsync();
        if (Tipo.TemEspolios)
            yield return Estado.CarregarEspoliosAsync();
        yield return Estado.CarregarArtefatosAsync();
        yield return Estado.CarregarAfeicoesAsync();
        yield return Estado.CarregarCaracteristicasAsync();
        if (Tipo.TemDiario)
            yield return Estado.CarregarDiarioAsync();
        if (Tipo.CriaCaracteristicaNoPicker)
            yield return Estado.CarregarAuditorDeRegrasAsync();
        yield return CarregarOQueEDaPaginaAsync();
        if (Tipo.TemTracosRaciais)
            yield return Estado.CarregarEscolhaRacialPendenteAsync();
    }

    /// <summary>Uma leitura que só a página do tipo faz ao abrir (junto com as das abas).</summary>
    protected virtual Task CarregarOQueEDaPaginaAsync() => Task.CompletedTask;

    private async Task LoadImagesAsync()
    {
        var url = Estado.IsGmCaller ? "images/mine" : $"campaigns/{Estado.CampaignId}/available-images";
        var response = await Http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            Estado.MinhasImagens = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>() ?? new();
    }

    // ---------------- A ficha ----------------

    /// <summary>Relê a ficha pelo auto-save: um campo com edição pendente ou em envio nunca é sobrescrito (spec 2.1.0 A.4).</summary>
    protected Task LoadSheetAsync() => AutoSave.RecarregarAsync(LerFichaAsync, ficha =>
    {
        AplicarFicha(ficha);
        return Task.CompletedTask;
    });

    protected async Task<TFicha?> LerFichaAsync()
    {
        var response = await Http.GetAsync(Tipo.Url(SheetId));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TFicha>() : null;
    }

    private const string ErroAoCarregarAFicha = "Não foi possível carregar a ficha.";

    protected void AplicarFicha(TFicha? ficha)
    {
        if (ficha is null)
        {
            Estado.Erro = ErroAoCarregarAFicha;
            return;
        }

        PreencherFormulario(ficha);
        // Só some o erro da própria releitura: uma recusa de outro salvamento (ex.: o 409 do cemitério) continua à vista.
        if (Estado.Erro == ErroAoCarregarAFicha)
            Estado.Erro = null;
    }

    /// <summary>Copia a ficha lida para o formulário e o estado (o record é do tipo).</summary>
    protected abstract void PreencherFormulario(TFicha ficha);

    /// <summary>O saldo de Pontos de Ignição no record do tipo.</summary>
    protected abstract int PontosDeIgnicaoDa(TFicha ficha);

    /// <summary>
    /// A API mexe no saldo de PI ao adicionar, editar e remover uma Magia/Habilidade (4.b), e é ela quem guarda o valor:
    /// o formulário só o exibe. Relê o saldo depois de cada operação que o altera. Se a releitura falhar, o campo fica
    /// como está — a operação em si já deu certo.
    /// </summary>
    private Task RecarregarPontosDeIgnicaoAsync() => AutoSave.RecarregarAsync(LerFichaAsync, ficha =>
    {
        if (ficha is not null)
            Formulario.PontosDeIgnicaoAtual = PontosDeIgnicaoDa(ficha);
        return Task.CompletedTask;
    });

    // ---------------- Condições (aviso em tempo real) ----------------

    private void OnCondicoesAlteradas(CondicoesAlteradasNotification notificacao)
    {
        if (notificacao.Kind != Tipo.TipoDaNotificacao || notificacao.SheetId != SheetId)
            return;

        _ = InvokeAsync(async () =>
        {
            // O aviso e a lista de Passivas mudam já; a recarga da ficha espera se há um campo em edição.
            Estado.CondicoesVersao++;
            StateHasChanged();
            try
            {
                await Recarga.SolicitarAsync();
            }
            catch (Exception)
            {
                Estado.Erro = "Não foi possível atualizar a ficha com as Condições.";
            }
            StateHasChanged();
        });
    }

    // Relê os números e as listas que as Condições mexem. Não passa pelo auto-save: nada é gravado.
    private Task RecarregarPorCondicoesAsync() => InvokeAsync(async () =>
    {
        await LoadSheetAsync();
        await Estado.CarregarAtributosECombateAsync();
        await Estado.CarregarMagiasAsync();
        if (Tipo.TemRunas)
            await Estado.CarregarRunasAsync();
        await Estado.CarregarArtefatosAsync();
        Estado.PenalidadesVersao++;
        StateHasChanged();
    });

    // Erro de uma recarga adiada (RecargaAdiada nunca chama isto depois do Dispose). Ninguém espera esta tarefa, então
    // ela não pode falhar: se a página sair no meio-tempo, não há onde mostrar o erro.
    private async Task AvisarFalhaDaRecargaAsync()
    {
        try
        {
            await InvokeAsync(() =>
            {
                Estado.Erro = "Não foi possível atualizar a ficha com as Condições.";
                StateHasChanged();
            });
        }
        catch (Exception)
        {
        }
    }

    private void StateHasChangedFromAutoSave() => InvokeAsync(StateHasChanged);

    // Uma aba filha mudou o estado fora de um evento da página (ex.: Erro depois de um POST recusado).
    private void OnEstadoAlterado() => InvokeAsync(StateHasChanged);

    // ---------------- Salvamentos ----------------

    /// <summary>O PUT da ficha inteira (o record do tipo); <c>false</c> sem salvar quando falta um campo obrigatório.</summary>
    protected abstract Task<bool> SaveIfValidAsync();

    protected Task NotifySavedAsync()
    {
        AutoSave.NotifyChanged(SaveIfValidAsync);
        return Task.CompletedTask;
    }

    /// <summary>
    /// A falha de um salvamento, para lançar (o auto-save mostra "Erro ao salvar" no indicador). A página de um tipo
    /// pode também levar a recusa ao alerta.
    /// </summary>
    protected virtual async Task<InvalidOperationException> FalhaAoSalvarAsync(HttpResponseMessage response) =>
        new(await response.Content.ReadErrorMessageAsync() ?? "Não foi possível salvar a ficha.");

    /// <summary>
    /// Depois do PUT da ficha: relê a ficha, a escolha racial pendente (uma troca de Variante pode ter limpado ou criado
    /// uma) e tudo o que depende do Nível.
    /// </summary>
    protected async Task RecarregarDepoisDeSalvarAsync()
    {
        await LoadSheetAsync();
        if (Tipo.TemTracosRaciais)
            await Estado.CarregarEscolhaRacialPendenteAsync();
        await RecarregarOQueDependeDoNivelAsync();
    }

    // A Experiência Atual (e o que mais o salvamento mexe) pode ter mudado o Nível: relê o aviso de novo nível, os
    // orçamentos de Atributos/Perícias e de Características e as Evoluções de Arca liberadas.
    private async Task RecarregarOQueDependeDoNivelAsync()
    {
        await Estado.CarregarAvisoDeNovoNivelAsync();
        await Estado.CarregarAtributosECombateAsync();
        Estado.PenalidadesVersao++;
        await Estado.CarregarCaracteristicasAsync();
        if (Tipo.TemLinhagemEVocacao)
            await Estado.CarregarHabilidadeRacialAsync();
    }

    /// <summary>
    /// Runs SaveIfValidAsync outside the autosave flow (i.e. not via AutoSave.NotifyChanged), for the immediate-save
    /// handlers that saved before the sheet had autosave: through SalvarAgora, the tabs' Sina, "Aplicar" of XP/PI,
    /// Ciclos and Arca rolada; through SalvarERecarregarAbasAsync, the Cobertura, the Afinidade and the Acerto Crítico.
    /// Preserves their original behavior of surfacing a save failure in the page alert instead of letting
    /// SaveIfValidAsync's exception go unhandled.
    /// </summary>
    protected async Task SaveIgnoringResultAsync()
    {
        try
        {
            await SaveIfValidAsync();
        }
        catch (InvalidOperationException ex)
        {
            Estado.Erro = ex.Message;
        }
    }

    /// <summary>
    /// A Cobertura e a Afinidade da aba Combate e o "Aplicar" do Acerto Crítico: salva na hora e relê as abas 2 e 3
    /// (a Defesa Natural, a Eficiência e o Dano Elemental vêm com os Sub-Atributos, o orçamento de Perícias com as
    /// Perícias; o salvamento só relê a ficha), num evento só da página.
    /// </summary>
    protected async Task SalvarERecarregarAbasAsync()
    {
        await SaveIgnoringResultAsync();
        await Estado.CarregarAtributosECombateAsync();
    }

    protected async Task OnEquipagemChosenAsync()
    {
        await LoadSheetAsync();
        await Task.WhenAll(Estado.CarregarAtributosECombateAsync(), Estado.CarregarInventarioAsync(), Estado.CarregarArtefatosAsync());
        Estado.PenalidadesVersao++;
    }

    // O saldo de Pontos de Ignição tem endpoint próprio: o servidor o altera sozinho (nível, bônus manual,
    // Magias/Habilidades), e o PUT genérico o ignora para que um formulário desatualizado nunca escreva por cima disso.
    // Editar o campo à mão salva só ele.
    protected Task NotifyPontosDeIgnicaoChangedAsync()
    {
        AutoSave.NotifyChanged(SavePontosDeIgnicaoAsync, "pi");
        return Task.CompletedTask;
    }

    private async Task<bool> SavePontosDeIgnicaoAsync()
    {
        if (Formulario.PontosDeIgnicaoAtual is null)
        {
            Estado.Erro = $"Informe os {Tipo.RotuloDosPontosDeIgnicao}.";
            return false;
        }

        var response = await Http.PutAsJsonAsync(Tipo.Url(SheetId, "pontos-de-ignicao"), Formulario.PontosDeIgnicaoAtual.Value);
        if (!response.IsSuccessStatusCode)
            throw await FalhaAoSalvarAsync(response);

        Estado.Erro = null;
        await RecarregarPontosDeIgnicaoAsync();
        return true;
    }

    // Onde o tipo edita Nível e Experiência à mão (TipoDeFichaCliente.EditaNivelAManual), cada um tem endpoint próprio,
    // não o PUT da ficha: aquele envia todos os campos de uma vez, sem como distinguir "o GM acabou de editar o Nível" de
    // "o Nível não foi tocado e ainda tem o valor antigo". Estas chamadas dizem qual campo mudou, e o servidor puxa o outro
    // pela mesma regra de limiar de XP da Ficha de Personagem (Requisitos - Ficha de NPCs / de Criaturas).
    protected Task NotifyNivelChangedAsync()
    {
        AutoSave.NotifyChanged(SaveNivelAsync, "nivel");
        return Task.CompletedTask;
    }

    protected Task NotifyExperienciaAtualChangedAsync()
    {
        AutoSave.NotifyChanged(SaveExperienciaAtualAsync, "xp");
        return Task.CompletedTask;
    }

    private async Task<bool> SaveNivelAsync()
    {
        if (Formulario.Nivel is null)
        {
            Estado.Erro = "Informe o Nível.";
            return false;
        }

        return await SalvarCampoDoNivelAsync("nivel", Formulario.Nivel.Value);
    }

    private async Task<bool> SaveExperienciaAtualAsync()
    {
        if (Formulario.ExperienciaAtual is null)
        {
            Estado.Erro = "Informe a Experiência Atual.";
            return false;
        }

        return await SalvarCampoDoNivelAsync("experiencia-atual", Formulario.ExperienciaAtual.Value);
    }

    private async Task<bool> SalvarCampoDoNivelAsync(string endpoint, int valor)
    {
        var response = await Http.PutAsJsonAsync(Tipo.Url(SheetId, endpoint), valor);
        if (!response.IsSuccessStatusCode)
            throw await FalhaAoSalvarAsync(response);

        Estado.Erro = null;
        await LoadSheetAsync();
        await RecarregarOQueDependeDoNivelAsync();
        return true;
    }
}
