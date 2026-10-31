using System.Net.Http.Json;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A aba Atributos &amp; Perícias: os Atributos e o orçamento deles, as Perícias (com o orçamento onde
/// <see cref="TipoDeFichaCliente.TemOrcamentoDePericias"/>) e as Afinidades (onde
/// <see cref="TipoDeFichaCliente.TemAfinidades"/>), e a recarga das abas 2 e 3 que tudo o que mexe nelas pede
/// (<see cref="CarregarAtributosECombateAsync"/>, que termina com o Combate). Toda edição termina em <see cref="NotificarAlteracao"/>: a
/// página mostra o erro (ou o apaga) e re-renderiza as filhas da aba com os dados novos.
/// </summary>
public sealed partial class FichaEstado
{
    public List<AttributeResponse> Atributos { get; set; } = new();

    public AttributePointBudgetResponse? OrcamentoDeAtributos { get; set; }

    /// <summary>As Perícias da ficha; as da Criatura passam pelo adaptador (sem Histórico, o Modificador fica sem negrito).</summary>
    public List<SkillResponse> Pericias { get; set; } = new();

    /// <summary>"Pontos de Perícia: gasto / disponível" (só onde o tipo tem orçamento de Perícias).</summary>
    public SkillPointBudgetResponse? OrcamentoDePericias { get; set; }

    /// <summary>As Afinidades por elemento (2.c): gasto, máximo, se a seção aparece e os elementos; <c>null</c> até a carga.</summary>
    public AfinidadesDaFichaResponse? Afinidades { get; set; }


    /// <summary>
    /// Os pontos de Acerto Crítico digitados e ainda não aplicados. Moram aqui, e não na seção, para sobreviver à troca de
    /// aba (a aba inativa sai da árvore), como quando eram um campo da página.
    /// </summary>
    public int? DeltaDeAcertoCritico { get; set; }

    /// <summary>
    /// Relê as abas 2 e 3, em sequência: Atributos, o orçamento deles, Perícias, o orçamento delas, Afinidades (o que o
    /// tipo tem) e então o Combate. Não avisa a página: quem chama (a página, ou uma ação que termina avisando) re-renderiza.
    /// </summary>
    public async Task CarregarAtributosECombateAsync()
    {
        Atributos = await Http.GetFromJsonAsync<List<AttributeResponse>>(Tipo.Url(SheetId, "attributes")) ?? new();
        OrcamentoDeAtributos = await Http.GetFromJsonAsync<AttributePointBudgetResponse>(Tipo.Url(SheetId, "attributes/budget"));
        Pericias = await CarregarPericiasAsync();
        if (Tipo.TemOrcamentoDePericias)
            OrcamentoDePericias = await Http.GetFromJsonAsync<SkillPointBudgetResponse>(Tipo.Url(SheetId, "skills/budget"));
        if (Tipo.TemAfinidades)
            Afinidades = await Http.GetFromJsonAsync<AfinidadesDaFichaResponse>(Tipo.Url(SheetId, "afinidades"));
        await CarregarCombateAsync();
    }

    // O bônus de Histórico no Modificador (ContaHistorico) só existe onde a ficha tem Antecedentes; o record da
    // Criatura não o traz e passa pelo adaptador. A escolha do record segue TipoDeFichaCliente.Formato, como a
    // dos records do Combate (FichaEstado.Combate.cs).
    private async Task<List<SkillResponse>> CarregarPericiasAsync()
    {
        var url = Tipo.Url(SheetId, "skills");
        if (Tipo.Formato == FormatoDosDados.Comum)
            return await Http.GetFromJsonAsync<List<SkillResponse>>(url) ?? new();
        var pericias = await Http.GetFromJsonAsync<List<CreatureSkillResponse>>(url) ?? new();
        return pericias.Select(AdaptadorDeCriatura.ParaPericia).ToList();
    }

    // ---------------- Atributos ----------------

    /// <summary>
    /// Grava um campo (<c>Gasto</c>, <c>Bonus</c> ou <c>TemMaestria</c>) da linha do atributo; um número que não se lê
    /// mantém o atual. A mensagem de uma recusa vai para o alerta, um salvamento certo o apaga (A6: em todo tipo).
    /// </summary>
    public async Task AtualizarAtributoAsync(string atributo, string campo, object? valor)
    {
        var atual = Atributos.Single(a => a.Atributo == atributo);
        var gasto = campo == "Gasto" ? (int.TryParse(valor?.ToString(), out var g) ? g : atual.Gasto) : atual.Gasto;
        var bonus = campo == "Bonus" ? (int.TryParse(valor?.ToString(), out var b) ? b : atual.Bonus) : atual.Bonus;
        var temMaestria = campo == "TemMaestria" ? (bool)(valor ?? false) : atual.TemMaestria;

        var response = await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"attributes/{atributo}"), new UpdateAttributeRequest(gasto, bonus, temMaestria));
        Erro = response.IsSuccessStatusCode ? null : await response.Content.ReadErrorMessageAsync();
        await RecarregarEMudarPenalidadesAsync();
    }

    // ---------------- Perícias ----------------

    public async Task AtualizarGastoDaPericiaAsync(string pericia, object? valor)
    {
        var atual = Pericias.Single(s => s.Pericia == pericia);
        var gasto = int.TryParse(valor?.ToString(), out var g) ? g : atual.Gasto;
        var response = await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"skills/{pericia}"), new UpdateSkillRequest(gasto, atual.AtributoEscolhido));
        Erro = response.IsSuccessStatusCode ? null : await response.Content.ReadErrorMessageAsync();
        await RecarregarEMudarPenalidadesAsync();
    }

    /// <summary>O "—" (vazio) tira o atributo da perícia. A mensagem de uma recusa vai para o alerta, um salvamento certo o apaga.</summary>
    public async Task AtualizarAtributoDaPericiaAsync(string pericia, object? valor)
    {
        var atual = Pericias.Single(s => s.Pericia == pericia);
        var atributoEscolhido = valor?.ToString();
        var response = await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"skills/{pericia}"),
            new UpdateSkillRequest(atual.Gasto, string.IsNullOrEmpty(atributoEscolhido) ? null : atributoEscolhido));
        Erro = response.IsSuccessStatusCode ? null : await response.Content.ReadErrorMessageAsync();
        await RecarregarEMudarPenalidadesAsync();
    }

    // Atributos e Perícias mexem nos requisitos dos equipamentos: depois da recarga, o alerta das penalidades relê.
    private async Task RecarregarEMudarPenalidadesAsync()
    {
        await CarregarAtributosECombateAsync();
        PenalidadesVersao++;
        NotificarAlteracao();
    }

    // ---------------- Afinidades ----------------

    /// <summary>Sobe ou desce os pontos de um elemento (o − e o +), com a Experiência que ele tem.</summary>
    public Task MudarPontosDaAfinidadeAsync(int elementoId, int delta) =>
        GravarAfinidadeAsync(elementoId, e => new AtualizarAfinidadeRequest(e.Pontos + delta, e.Experiencia));

    /// <summary>Grava a Experiência de um elemento, com os pontos que ele tem.</summary>
    public Task AtualizarExperienciaDaAfinidadeAsync(int elementoId, int? experiencia) =>
        GravarAfinidadeAsync(elementoId, e => new AtualizarAfinidadeRequest(e.Pontos, experiencia));

    // Uma gravação de cada vez: a seguinte espera a anterior e a recarga dela, e monta o pedido com os valores relidos —
    // um + seguido da Experiência (ou dois +) nunca manda os pontos de antes e desfaz o que o anterior gravou.
    private readonly SemaphoreSlim _filaDeAfinidades = new(1, 1);

    private async Task GravarAfinidadeAsync(int elementoId, Func<ElementoDaFichaResponse, AtualizarAfinidadeRequest> pedido)
    {
        await _filaDeAfinidades.WaitAsync();
        try
        {
            var elemento = Afinidades?.Elementos.SingleOrDefault(e => e.Id == elementoId);
            if (elemento is null)
                return;
            var response = await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"afinidades/{elementoId}"), pedido(elemento));
            if (response.IsSuccessStatusCode)
                Erro = null;
            else
            {
                var mensagem = await response.Content.ReadErrorMessageAsync();
                Erro = string.IsNullOrWhiteSpace(mensagem) ? "Não foi possível gravar a afinidade." : mensagem;
            }
            await CarregarAtributosECombateAsync();
        }
        finally
        {
            _filaDeAfinidades.Release();
        }
        NotificarAlteracao();
    }

    private async Task RecarregarAbasAsync()
    {
        await CarregarAtributosECombateAsync();
        NotificarAlteracao();
    }
}
