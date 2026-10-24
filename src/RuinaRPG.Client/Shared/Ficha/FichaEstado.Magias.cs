using System.Net.Http.Json;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.Runes;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A aba Magias &amp; Habilidades: as Magias/Habilidades da ficha e as Runas (só onde <see cref="TipoDeFichaCliente.TemRunas"/>),
/// cada uma com o Banco de onde se escolhe (o do GM, ou o que a campanha liberou para o jogador). As ações avisam a
/// página (<see cref="FichaEstado.NotificarAlteracao"/>) para o alerta de erro e o saldo de PI re-renderizarem.
/// </summary>
public sealed partial class FichaEstado
{
    public List<SpellAbilityResponse> Magias { get; set; } = new();

    public List<SpellAbilityEntryResponse> BancoDeMagias { get; set; } = new();

    public SpellAbilityFormModel FormularioDeMagia { get; } = new();

    public List<RuneResponse> Runas { get; set; } = new();

    public List<RuneBankEntryResponse> BancoDeRunas { get; set; } = new();

    public RuneOrigemModel FormularioDeRuna { get; } = new();

    /// <summary>
    /// A API mexe no saldo de PI ao adicionar, editar e remover uma Magia/Habilidade (4.b); o saldo vive no formulário
    /// da página (um campo diferente por tipo), então é a página quem o relê. Sem gancho, nada é relido.
    /// </summary>
    public Func<Task>? RecarregarPontosDeIgnicao { get; set; }

    public string UrlDasMagias => Tipo.Url(SheetId, "spell-abilities");

    public string UrlDaMagia(string id) => Tipo.Url(SheetId, $"spell-abilities/{id}");

    public async Task CarregarMagiasAsync()
    {
        Magias = await Http.GetFromJsonAsync<List<SpellAbilityResponse>>(UrlDasMagias) ?? new();
        var bankUrl = IsGmCaller ? "spell-ability-bank" : $"campaigns/{CampaignId}/available-spell-abilities";
        BancoDeMagias = await Http.GetFromJsonAsync<List<SpellAbilityEntryResponse>>(bankUrl) ?? new();
    }

    public IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentesParaPicker() =>
        FormularioDeMagia.Efeitos.Select(e => (e.EfeitoNome, e.Quantidade)).ToList();

    public void AdicionarEfeitos(List<EfeitoAdicionadoResult> resultados) =>
        FormularioDeMagia.Efeitos.AddRange(resultados.Select(r => new EfeitoLinha(r.EfeitoNome, r.Quantidade, r.CustoPI)));

    public async Task AdicionarMagiaAsync()
    {
        var form = FormularioDeMagia;
        var request = form.Origem == "Banco"
            ? new AddSpellAbilityRequest(form.SourceBankEntryId, null, null, null, null, null)
            : new AddSpellAbilityRequest(null, form.Nome, form.Tipo, form.Grau, form.Descricao,
                form.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList());

        var response = await Http.PostAsJsonAsync(UrlDasMagias, request);
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro(await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a magia/habilidade.");
            return;
        }

        form.SourceBankEntryId = "";
        form.Nome = "";
        form.Tipo = "Magia";
        form.Grau = 0;
        form.Descricao = "";
        form.Efeitos.Clear();
        await RecarregarMagiasEPontosDeIgnicaoAsync();
    }

    public async Task RemoverMagiaAsync(string id)
    {
        await Http.DeleteAsync(UrlDaMagia(id));
        await RecarregarMagiasEPontosDeIgnicaoAsync();
    }

    /// <summary>Depois de mexer numa Magia/Habilidade (incluindo salvar o diálogo de edição): a lista, o Banco e o saldo de PI.</summary>
    public async Task RecarregarMagiasEPontosDeIgnicaoAsync()
    {
        await CarregarMagiasAsync();
        if (RecarregarPontosDeIgnicao is not null)
            await RecarregarPontosDeIgnicao();
        NotificarAlteracao();
    }

    public async Task CarregarRunasAsync()
    {
        Runas = await Http.GetFromJsonAsync<List<RuneResponse>>(Tipo.Url(SheetId, "runes")) ?? new();
        var bankUrl = IsGmCaller ? "rune-bank" : $"campaigns/{CampaignId}/available-runes";
        BancoDeRunas = await Http.GetFromJsonAsync<List<RuneBankEntryResponse>>(bankUrl) ?? new();
    }

    public async Task AdicionarRunaAsync()
    {
        var form = FormularioDeRuna;
        var erro = form.Validar();
        if (erro is not null)
        {
            MostrarErro(erro);
            return;
        }

        var request = form.DoBanco
            ? new AddRuneRequest(null, null, null, form.SourceBankEntryId)
            : new AddRuneRequest(form.Nome, form.Descricao, form.Grau, ImageId: string.IsNullOrEmpty(form.ImageId) ? null : form.ImageId, Tipo: form.TipoOuNulo, Disciplina: form.Disciplina);

        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "runes"), request);
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro(await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a runa.");
            return;
        }

        form.Limpar();
        await CarregarRunasAsync();
    }

    public async Task RemoverRunaAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"runes/{id}"));
        await CarregarRunasAsync();
    }
}
