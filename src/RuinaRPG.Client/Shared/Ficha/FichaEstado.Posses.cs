using System.Net.Http.Json;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A aba Posses: o Inventário (onde <see cref="TipoDeFichaCliente.TemInventario"/>), os Espólios (onde
/// <see cref="TipoDeFichaCliente.TemEspolios"/>), os Artefatos e as Afeições, e a busca no Catálogo de Itens que os
/// seletores (também os do Combate) usam. O Equipamento Inicial e os Ciclos mexem no formulário auto-salvo da página
/// (<see cref="FormularioDaFicha"/>), que ela passa à aba e grava. Toda mutação que dá certo termina em <see cref="NotificarAlteracao"/>: as seções filhas da aba sem
/// parâmetros (AfeicoesNaLinha, EspoliosSection) só re-renderizam pela cascata da página.
/// </summary>
public sealed partial class FichaEstado
{
    public List<InventoryItemResponse> Inventario { get; set; } = new();

    public InventoryFormModel FormularioDeInventario { get; } = new();

    public List<CreatureSpoilResponse> Espolios { get; set; } = new();

    public SpoilFormModel FormularioDeEspolio { get; } = new();

    public List<ArtifactResponse> Artefatos { get; set; } = new();

    public ArtifactFormModel FormularioDeArtefato { get; } = new();

    public List<AffectionResponse> Afeicoes { get; set; } = new();

    public AffectionFormModel FormularioDeAfeicao { get; } = new();

    /// <summary>
    /// O Inventário (o Peso muda a Carga) e os Artefatos (mudam Atributos e Sub-Atributos) fazem a página reler as
    /// abas 2 e 3, que ainda são dela. Toda página o liga (FichaEstadoNaPaginaTests confere).
    /// </summary>
    public Func<Task>? RecarregarAtributosECombate { get; set; }

    // ---------------- Inventário ----------------

    public async Task CarregarInventarioAsync() =>
        Inventario = await Http.GetFromJsonAsync<List<InventoryItemResponse>>(Tipo.Url(SheetId, "inventory")) ?? new();

    public async Task AdicionarItemAoInventarioAsync()
    {
        var form = FormularioDeInventario;
        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "inventory"), new AddInventoryItemRequest(form.ItemId, form.Qtd));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível adicionar o item.");
            return;
        }

        form.ItemId = "";
        form.Qtd = 1;
        await RecarregarInventarioEAbasAsync();
    }

    public async Task AtualizarQtdDoItemAsync(string id, object? value)
    {
        if (int.TryParse(value?.ToString(), out var qtd))
            await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"inventory/{id}/qtd"), qtd);
        await RecarregarInventarioEAbasAsync();
    }

    public async Task RemoverItemDoInventarioAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"inventory/{id}"));
        await RecarregarInventarioEAbasAsync();
    }

    private async Task RecarregarInventarioEAbasAsync()
    {
        await CarregarInventarioAsync();
        await RecarregarAtributosECombateAsync();
        NotificarAlteracao();
    }

    // ---------------- Espólios ----------------

    public async Task CarregarEspoliosAsync() =>
        Espolios = await Http.GetFromJsonAsync<List<CreatureSpoilResponse>>(Tipo.Url(SheetId, "spoils")) ?? new();

    public async Task AdicionarEspolioAsync()
    {
        var form = FormularioDeEspolio;
        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "spoils"), new AddCreatureSpoilRequest(form.ItemId, form.Qtd, form.DT));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível adicionar o espólio.");
            return;
        }

        form.ItemId = "";
        form.Qtd = 1;
        form.DT = 0;
        await RecarregarEspoliosAsync();
    }

    public async Task AtualizarQtdDoEspolioAsync(string id, object? value)
    {
        if (int.TryParse(value?.ToString(), out var qtd))
            await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"spoils/{id}/qtd"), qtd);
        await RecarregarEspoliosAsync();
    }

    public async Task RemoverEspolioAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"spoils/{id}"));
        await RecarregarEspoliosAsync();
    }

    private async Task RecarregarEspoliosAsync()
    {
        await CarregarEspoliosAsync();
        NotificarAlteracao();
    }

    // ---------------- Artefatos ----------------

    public async Task CarregarArtefatosAsync() =>
        Artefatos = await Http.GetFromJsonAsync<List<ArtifactResponse>>(Tipo.Url(SheetId, "artifacts")) ?? new();

    public async Task AdicionarArtefatoAsync()
    {
        var form = FormularioDeArtefato;
        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "artifacts"), new AddArtifactRequest(form.ArtifactItemId));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível adicionar o artefato.");
            return;
        }

        form.ArtifactItemId = "";
        await RecarregarArtefatosEAbasAsync();
    }

    public async Task RemoverArtefatoAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"artifacts/{id}"));
        await RecarregarArtefatosEAbasAsync();
    }

    private async Task RecarregarArtefatosEAbasAsync()
    {
        await CarregarArtefatosAsync();
        await RecarregarAtributosECombateAsync();
        PenalidadesVersao++;
        NotificarAlteracao();
    }

    // ---------------- Afeições ----------------

    public async Task CarregarAfeicoesAsync() =>
        Afeicoes = await Http.GetFromJsonAsync<List<AffectionResponse>>(Tipo.Url(SheetId, "affections")) ?? new();

    public async Task AdicionarAfeicaoAsync()
    {
        var form = FormularioDeAfeicao;
        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "affections"), new AddAffectionRequest(form.Nome, form.Favorabilidade));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível adicionar a afeição.");
            return;
        }

        form.Nome = "";
        form.Favorabilidade = 0;
        await RecarregarAfeicoesAsync();
    }

    public async Task RemoverAfeicaoAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"affections/{id}"));
        await RecarregarAfeicoesAsync();
    }

    /// <summary>A edição na linha (só onde <see cref="TipoDeFichaCliente.EditaAfeicaoNaLinha"/>): grava a linha com o campo mudado.</summary>
    public async Task AtualizarAfeicaoAsync(string id, string field, object? value)
    {
        var current = Afeicoes.Single(a => a.Id == id);
        var nome = field == "Nome" ? value?.ToString() ?? current.Nome : current.Nome;
        var favorabilidade = field == "Favorabilidade" ? (int.TryParse(value?.ToString(), out var f) ? f : current.Favorabilidade) : current.Favorabilidade;

        await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"affections/{id}"), new UpdateCharacterAffectionRequest(nome, favorabilidade));
        await RecarregarAfeicoesAsync();
    }

    private async Task RecarregarAfeicoesAsync()
    {
        await CarregarAfeicoesAsync();
        NotificarAlteracao();
    }

    // ---------------- Busca no Catálogo de Itens ----------------

    /// <summary>Os itens de um Tipo do Catálogo: o do GM, ou os que a campanha liberou para o jogador.</summary>
    public Task<List<PickerOption>> BuscarItensPorTipoAsync(string query, string tipo) =>
        BuscarItensAsync($"?nome={Uri.EscapeDataString(query)}&tipo={tipo}");

    /// <summary>
    /// Itens de qualquer Tipo: os Espólios não têm restrição no servidor (AddSpoil aceita qualquer Item do Catálogo),
    /// ao contrário de Inventário, Arma, Armadura e Artefato, cada um de um Tipo só.
    /// </summary>
    public Task<List<PickerOption>> BuscarTodosOsItensAsync(string query) =>
        BuscarItensAsync($"?nome={Uri.EscapeDataString(query)}");

    private async Task<List<PickerOption>> BuscarItensAsync(string consulta)
    {
        var url = (IsGmCaller ? "items" : $"campaigns/{CampaignId}/available-items") + consulta;
        var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new();
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        return items.Select(i => new PickerOption(i.Id, i.Nome)).ToList();
    }

    // ---------------- comum ----------------

    private async Task RecarregarAtributosECombateAsync()
    {
        if (RecarregarAtributosECombate is not null)
            await RecarregarAtributosECombate();
    }

    private void MostrarErro(string erro)
    {
        Erro = erro;
        NotificarAlteracao();
    }
}
