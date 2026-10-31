using System.Net.Http.Json;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A aba Combate: as Armas (com a arma manual onde <see cref="TipoDeFichaCliente.TemArmaManual"/>), as Armaduras, os
/// Escudos, os Sub-Atributos e o Modificador de Dano, que vêm no fim da recarga das abas 2 e 3
/// (<see cref="CarregarAtributosECombateAsync"/>), e as Maestrias, que têm carga própria. Os recursos, a Sina, a
/// Cobertura e a Afinidade são do formulário auto-salvo da página, que ela passa à aba e grava. Toda ação termina em
/// <see cref="NotificarAlteracao"/>: a página mostra o erro e re-renderiza a aba (e o alerta das penalidades, quando a
/// versão sobe) com os dados novos.
/// </summary>
public sealed partial class FichaEstado
{
    public List<LinhaDeArma> Armas { get; set; } = new();

    public WeaponFormModel FormularioDeArma { get; } = new();

    public List<ArmorSlotResponse> SlotsDeArmadura { get; set; } = new();

    /// <summary>O item escolhido no seletor de cada slot vazio; já com os 3 slots fixos, para o seletor nunca ler uma chave ausente.</summary>
    public Dictionary<string, string> FormularioDosSlots { get; } = new() { ["Capacete"] = "", ["Superior"] = "", ["Inferior"] = "" };

    public List<ShieldResponse> Escudos { get; set; } = new();

    public ShieldFormModel FormularioDeEscudo { get; } = new();

    public ModificadorDeDanoResponse? ModificadorDeDano { get; set; }

    public List<MasteryResponse> Maestrias { get; set; } = new();

    public MasteryFormModel FormularioDeMaestria { get; } = new();

    // O fim da recarga das abas 2 e 3, em sequência, na ordem de hoje. A Criatura tem os seus records no fio (arma com
    // ItemId e Durabilidade anuláveis, DurabilidadeMaximo): o tipo com esse formato os lê e passa pelo adaptador (ver
    // TipoDeFichaCliente.Formato).
    private async Task CarregarCombateAsync()
    {
        if (Tipo.Formato == FormatoDosDados.Criatura)
        {
            var armas = await Http.GetFromJsonAsync<List<CreatureWeaponResponse>>(Tipo.Url(SheetId, "weapons")) ?? new();
            Armas = armas.Select(LinhaDeArma.De).ToList();
            var slots = await Http.GetFromJsonAsync<List<CreatureArmorSlotResponse>>(Tipo.Url(SheetId, "armor-slots")) ?? new();
            SlotsDeArmadura = slots.Select(AdaptadorDeCriatura.ParaSlotDeArmadura).ToList();
        }
        else
        {
            var armas = await Http.GetFromJsonAsync<List<WeaponResponse>>(Tipo.Url(SheetId, "weapons")) ?? new();
            Armas = armas.Select(LinhaDeArma.De).ToList();
            SlotsDeArmadura = await Http.GetFromJsonAsync<List<ArmorSlotResponse>>(Tipo.Url(SheetId, "armor-slots")) ?? new();
        }
        Escudos = await Http.GetFromJsonAsync<List<ShieldResponse>>(Tipo.Url(SheetId, "shields")) ?? new();
        SubAtributos = await Http.GetFromJsonAsync<SubAttributesResponse>(Tipo.Url(SheetId, "sub-attributes"));
        ModificadorDeDano = await Http.GetFromJsonAsync<ModificadorDeDanoResponse>(Tipo.Url(SheetId, "modificador-de-dano"));
    }

    /// <summary>
    /// O nome da Perícia: o da própria resposta das perícias da ficha (sempre fresca); o catálogo da sessão pode estar
    /// desatualizado se o Auditor adicionou ou renomeou uma perícia depois que ele foi carregado.
    /// </summary>
    public string NomeDaPericia(string chave, PericiaCatalogo catalogo) =>
        Pericias.FirstOrDefault(s => s.Pericia == chave)?.Nome ?? catalogo.Label(chave);

    // ---------------- Armas ----------------

    /// <summary>Do Catálogo (o item escolhido) ou, onde o tipo tem arma manual e a Origem é "Manual", com os campos manuais.</summary>
    public async Task AdicionarArmaAsync()
    {
        var form = FormularioDeArma;
        HttpResponseMessage response;
        if (Tipo.Formato == FormatoDosDados.Comum)
            response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "weapons"), new AddWeaponRequest(form.ItemId));
        else if (form.Origem == "Catalogo")
            response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "weapons"), new AddCreatureWeaponRequest(form.ItemId, null, null, null, null));
        else
            response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "weapons"), new AddCreatureWeaponRequest(null, form.ManualNome, form.ManualTipoDeDano, form.ManualDados, form.ManualDano));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível adicionar a arma.");
            return;
        }

        form.Limpar();
        await RecarregarCombateEPenalidadesAsync();
    }

    public async Task EquiparArmaAsync(string id, bool equipada)
    {
        await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"weapons/{id}"), equipada);
        await RecarregarCombateEPenalidadesAsync();
    }

    public async Task RemoverArmaAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"weapons/{id}"));
        await RecarregarCombateEPenalidadesAsync();
    }

    /// <summary>Um valor que não se lê como número não grava; a recarga vem do mesmo jeito (o campo volta ao valor salvo).</summary>
    public async Task AtualizarDurabilidadeDaArmaAsync(string id, object? valor)
    {
        if (int.TryParse(valor?.ToString(), out var durabilidade))
            await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"weapons/{id}/durabilidade"), durabilidade);
        await RecarregarAbasAsync();
    }

    // ---------------- Armaduras ----------------

    /// <summary>Um item em branco (ou <c>null</c>) tira a armadura do slot. O seletor do slot esvazia.</summary>
    public async Task AtribuirSlotDeArmaduraAsync(string slot, string? itemId)
    {
        await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"armor-slots/{slot}"), new UpdateArmorSlotRequest(string.IsNullOrWhiteSpace(itemId) ? null : itemId));
        FormularioDosSlots[slot] = "";
        await RecarregarCombateEPenalidadesAsync();
    }

    public async Task AtualizarDurabilidadeDoSlotAsync(string slot, object? valor)
    {
        if (int.TryParse(valor?.ToString(), out var durabilidade))
            await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"armor-slots/{slot}/durabilidade"), durabilidade);
        await RecarregarAbasAsync();
    }

    // ---------------- Escudos ----------------

    public async Task AdicionarEscudoAsync()
    {
        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "shields"), new AddShieldRequest(FormularioDeEscudo.ItemId));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível adicionar o escudo.");
            return;
        }

        FormularioDeEscudo.ItemId = "";
        await RecarregarCombateEPenalidadesAsync();
    }

    public async Task EquiparEscudoAsync(string id, bool equipado)
    {
        await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"shields/{id}"), equipado);
        await RecarregarCombateEPenalidadesAsync();
    }

    public async Task RemoverEscudoAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"shields/{id}"));
        await RecarregarCombateEPenalidadesAsync();
    }

    public async Task AtualizarDurabilidadeDoEscudoAsync(string id, object? valor)
    {
        if (int.TryParse(valor?.ToString(), out var durabilidade))
            await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"shields/{id}/durabilidade"), durabilidade);
        await RecarregarAbasAsync();
    }

    // Equipamento mexe nos Sub-Atributos e nas penalidades: depois da recarga, o alerta das penalidades relê.
    private async Task RecarregarCombateEPenalidadesAsync()
    {
        await CarregarAtributosECombateAsync();
        PenalidadesVersao++;
        NotificarAlteracao();
    }

    // ---------------- Maestrias ----------------

    public async Task CarregarMaestriasAsync() =>
        Maestrias = await Http.GetFromJsonAsync<List<MasteryResponse>>(Tipo.Url(SheetId, "masteries")) ?? new();

    /// <summary>O Nome e o Gasto esvaziam; a Perícia e o Atributo escolhidos ficam para a próxima.</summary>
    public async Task AdicionarMaestriaAsync()
    {
        var form = FormularioDeMaestria;
        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "masteries"), new AddMasteryRequest(form.Nome, form.Pericia, form.Atributo, form.GastoMaestria));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível adicionar a maestria.");
            return;
        }

        form.Nome = "";
        form.GastoMaestria = 0;
        await RecarregarMaestriasAsync();
    }

    public async Task RemoverMaestriaAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"masteries/{id}"));
        await RecarregarMaestriasAsync();
    }

    /// <summary>
    /// A edição na linha, nos três tipos (PUT masteries/{id}): grava a linha com o campo mudado. A mensagem de uma recusa vai para o alerta, um salvamento certo o apaga.
    /// </summary>
    public async Task AtualizarMaestriaAsync(string id, string campo, object? valor)
    {
        var atual = Maestrias.Single(m => m.Id == id);
        var nome = campo == "Nome" ? valor?.ToString() ?? atual.Nome : atual.Nome;
        var pericia = campo == "Pericia" ? valor?.ToString() ?? atual.Pericia : atual.Pericia;
        var atributo = campo == "Atributo" ? valor?.ToString() ?? atual.Atributo : atual.Atributo;
        var gastoMaestria = campo == "GastoMaestria" ? (int.TryParse(valor?.ToString(), out var g) ? g : atual.GastoMaestria) : atual.GastoMaestria;

        var response = await Http.PutAsJsonAsync(Tipo.Url(SheetId, $"masteries/{id}"), new UpdateMasteryRequest(nome, pericia, atributo, gastoMaestria));
        Erro = response.IsSuccessStatusCode ? null : await response.Content.ReadErrorMessageAsync();
        await RecarregarMaestriasAsync();
    }

    private async Task RecarregarMaestriasAsync()
    {
        await CarregarMaestriasAsync();
        NotificarAlteracao();
    }
}
