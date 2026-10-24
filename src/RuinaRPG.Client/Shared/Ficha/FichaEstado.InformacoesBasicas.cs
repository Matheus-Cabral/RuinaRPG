using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A aba Informações Básicas: as Características (lista, busca no catálogo do tipo, adicionar e remover), quem é Auditor
/// de Regras (cria característica no seletor, onde <see cref="TipoDeFichaCliente.CriaCaracteristicaNoPicker"/>), as
/// Variantes liberadas e a Habilidade Racial (onde o tipo tem Linhagem) e o envio de uma imagem nova. Os campos
/// auto-salvos (nome, nível, Âmbares…) moram no <see cref="FormularioDaFicha"/> da página, que os grava. Toda mutação
/// termina em <see cref="NotificarAlteracao"/>, como nas outras abas.
/// </summary>
public sealed partial class FichaEstado
{
    public TraitsListResponse? Caracteristicas { get; set; }

    public TraitFormModel FormularioDeCaracteristica { get; } = new();

    /// <summary>Quem abriu a ficha é Auditor de Regras (só onde o tipo cria característica no seletor; a página o lê).</summary>
    public bool EhAuditorDeRegras { get; set; }

    public List<VarianteLiberadaResponse> VariantesLiberadas { get; set; } = new();

    /// <summary>A Habilidade Racial (no topo de Magias &amp; Habilidades, onde o tipo tem Linhagem). As Evoluções de Arca dependem do Nível: a página a relê depois de salvar.</summary>
    public RacialAbilityResponse? HabilidadeRacial { get; set; }

    /// <summary>
    /// O XP e o PI (bônus manual) digitados e ainda não aplicados (onde o Nível vem da Experiência). Moram aqui, e não na
    /// seção, para sobreviver à troca de aba (a aba inativa sai da árvore), como quando eram campos da página.
    /// </summary>
    public int? DeltaDeExperiencia { get; set; }

    /// <inheritdoc cref="DeltaDeExperiencia"/>
    public int? DeltaDeBonusManualDePi { get; set; }

    // ---------------- Características ----------------

    /// <summary>A Criatura não tem Característica Racial: a lista dela vem sem a marca e passa pelo adaptador.</summary>
    public async Task CarregarCaracteristicasAsync()
    {
        var url = Tipo.Url(SheetId, "traits");
        if (Tipo.TemTracosRaciais)
            Caracteristicas = await Http.GetFromJsonAsync<TraitsListResponse>(url);
        else
            Caracteristicas = await Http.GetFromJsonAsync<CreatureTraitsListResponse>(url) is { } lista ? AdaptadorDeCriatura.ParaCaracteristicas(lista) : null;
    }

    public async Task AdicionarCaracteristicaAsync()
    {
        var form = FormularioDeCaracteristica;
        var response = await Http.PostAsJsonAsync(Tipo.Url(SheetId, "traits"), new AddTraitRequest(form.TraitId, form.Especificacao));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro(await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a característica.");
            return;
        }

        form.TraitId = "";
        form.Especificacao = "";
        await RecarregarCaracteristicasAsync();
    }

    public async Task RemoverCaracteristicaAsync(string id)
    {
        await Http.DeleteAsync(Tipo.Url(SheetId, $"traits/{id}"));
        await RecarregarCaracteristicasAsync();
    }

    private async Task RecarregarCaracteristicasAsync()
    {
        await CarregarCaracteristicasAsync();
        NotificarAlteracao();
    }

    /// <summary>Busca no catálogo do tipo (<see cref="TipoDeFichaCliente.UrlDeBuscaDeCaracteristicas"/>); se falhar, nada.</summary>
    public async Task<List<PickerOption>> BuscarCaracteristicasAsync(string consulta)
    {
        var response = await Http.GetAsync($"{Tipo.UrlDeBuscaDeCaracteristicas}?nome={Uri.EscapeDataString(consulta ?? "")}");
        if (!response.IsSuccessStatusCode)
            return new();
        var caracteristicas = await response.Content.ReadFromJsonAsync<List<TraitResponse>>() ?? new();
        return caracteristicas.Select(t => new PickerOption(t.Id, $"{t.Nome} ({t.Custo}, {t.Polaridade})")).ToList();
    }

    public async Task CarregarAuditorDeRegrasAsync()
    {
        var response = await Http.GetAsync("auth/me");
        if (response.IsSuccessStatusCode)
            EhAuditorDeRegras = (await response.Content.ReadFromJsonAsync<MeResponse>())?.IsRulesAuditor ?? false;
    }

    // ---------------- Linhagem ----------------

    public async Task CarregarVariantesLiberadasAsync() =>
        VariantesLiberadas = await Http.GetFromJsonAsync<List<VarianteLiberadaResponse>>(Tipo.Url(SheetId, "variantes-liberadas")) ?? new();

    public async Task CarregarHabilidadeRacialAsync() =>
        HabilidadeRacial = await Http.GetFromJsonAsync<RacialAbilityResponse>(Tipo.Url(SheetId, "racial-ability"));

    // ---------------- imagem ----------------

    /// <summary>
    /// Sobe uma imagem nova (o jogador, na campanha da ficha) e a põe no topo da galeria. Devolve a imagem enviada, ou
    /// <c>null</c> com a mensagem no alerta se o envio falhar; quem chama a põe no formulário e salva.
    /// </summary>
    public async Task<ImageUploadResponse?> EnviarImagemAsync(IBrowserFile arquivo)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(arquivo.OpenReadStream(maxAllowedSize: 20_000_000));
        content.Add(fileContent, "file", arquivo.Name);
        if (!IsGmCaller && CampaignId is not null)
            content.Add(new StringContent(CampaignId), "campaignId");

        var response = await Http.PostAsync("images", content);
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível enviar a imagem.");
            return null;
        }

        var enviada = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        MinhasImagens.Insert(0, new ImageSummaryResponse(enviada!.Id, enviada.Url, DateTime.UtcNow));
        return enviada;
    }
}
