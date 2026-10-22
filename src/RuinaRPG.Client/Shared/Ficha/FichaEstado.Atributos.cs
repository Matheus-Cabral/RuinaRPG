using System.Net.Http.Json;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A aba Atributos &amp; Perícias: os Atributos e o orçamento deles, as Perícias (com o orçamento onde
/// <see cref="TipoDeFichaCliente.TemOrcamentoDePericias"/>) e as Afinidades (onde
/// <see cref="TipoDeFichaCliente.TemAfinidades"/>), e a recarga das abas 2 e 3 que tudo o que mexe nelas pede
/// (<see cref="CarregarAtributosECombateAsync"/>). Toda edição termina em <see cref="NotificarAlteracao"/>: a
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

    public List<AffinityResponse> Afinidades { get; set; } = new();

    public AffinityFormModel FormularioDeAfinidade { get; } = new();

    /// <summary>
    /// O Combate (Armas, Armaduras, Escudos, Sub-Atributos, Modificador de Dano) ainda é da página: ela o liga, e a
    /// recarga das abas 2 e 3 o chama depois das listas desta aba, na ordem de hoje. Toda página o liga
    /// (FichaEstadoNaPaginaTests confere); sai quando o Combate vier para o estado (Task 12).
    /// </summary>
    public Func<Task>? CarregarCombate { get; set; }

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
            Afinidades = await Http.GetFromJsonAsync<List<AffinityResponse>>(Tipo.Url(SheetId, "affinities")) ?? new();
        if (CarregarCombate is not null)
            await CarregarCombate();
    }

    // O bônus de Histórico no Modificador (ContaHistorico) só existe onde a ficha tem Antecedentes; o record da
    // Criatura não o traz e passa pelo adaptador.
    private async Task<List<SkillResponse>> CarregarPericiasAsync()
    {
        var url = Tipo.Url(SheetId, "skills");
        if (Tipo.TemAntecedentes)
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

    /// <summary>O "—" (vazio) tira o atributo da perícia. Como sempre, a resposta não mexe no alerta.</summary>
    public async Task AtualizarAtributoDaPericiaAsync(string pericia, object? valor)
    {
        var atual = Pericias.Single(s => s.Pericia == pericia);
        var atributoEscolhido = valor?.ToString();
        await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"skills/{pericia}"),
            new UpdateSkillRequest(atual.Gasto, string.IsNullOrEmpty(atributoEscolhido) ? null : atributoEscolhido));
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

    public async Task AdicionarAfinidadeAsync()
    {
        var form = FormularioDeAfinidade;
        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "affinities"),
            new AddAffinityRequest(form.Elemento, form.ElementoValor, form.SegundaEssencia, form.SegundaEssenciaValor, form.SubElementoValor, form.Experiencia));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro(await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a afinidade.");
            return;
        }

        form.Limpar();
        await RecarregarAbasAsync();
    }

    /// <summary>
    /// Grava um campo da linha. Trocar a Essência 1 descarta a Essência 2 que não cruza mais com ela, e sem a Essência 2
    /// o valor dela também vai embora (como na linha nova).
    /// </summary>
    public async Task AtualizarAfinidadeAsync(string id, string campo, object? valor)
    {
        var atual = Afinidades.Single(a => a.Id == id);
        var elemento = campo == "Elemento" ? valor?.ToString() : atual.Elemento;
        var elementoValor = campo == "ElementoValor" ? valor as int? : atual.ElementoValor;
        var segunda = campo == "SegundaEssencia" ? valor?.ToString() : atual.SegundaEssencia;
        var segundaValor = campo == "SegundaEssenciaValor" ? valor as int? : atual.SegundaEssenciaValor;
        var subElementoValor = campo == "SubElementoValor" ? valor as int? : atual.SubElementoValor;
        var experiencia = campo == "Experiencia" ? valor as int? : atual.Experiencia;

        if (campo == "Elemento" && !AfinidadeEssenciaField.OpcoesSegundaEssencia(elemento, null).Any(o => o.Valor == segunda))
            segunda = null;
        if (segunda is null)
            segundaValor = null;

        var response = await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"affinities/{id}"),
            new UpdateAffinityRequest(elemento, elementoValor, segunda, segundaValor, subElementoValor, experiencia));
        Erro = response.IsSuccessStatusCode ? null : await response.Content.ReadErrorMessageAsync();
        await RecarregarAbasAsync();
    }

    public async Task RemoverAfinidadeAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"affinities/{id}"));
        await RecarregarAbasAsync();
    }

    private async Task RecarregarAbasAsync()
    {
        await CarregarAtributosECombateAsync();
        NotificarAlteracao();
    }
}
