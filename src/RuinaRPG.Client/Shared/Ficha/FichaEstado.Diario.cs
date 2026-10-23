using System.Net.Http.Json;
using RuinaRPG.Contracts.Diary;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A aba Diário (onde <see cref="TipoDeFichaCliente.TemDiario"/>): as entradas, o rascunho da entrada nova e a edição em
/// curso. O rascunho e a edição moram aqui, e não na aba, para sobreviver à troca de aba (a aba inativa sai da árvore),
/// como quando eram campos da página. Toda ação termina em um <see cref="NotificarAlteracao"/>.
/// </summary>
public sealed partial class FichaEstado
{
    public List<DiaryEntryResponse> EntradasDoDiario { get; set; } = new();

    /// <summary>O texto da entrada nova.</summary>
    public DiaryFormModel FormularioDoDiario { get; } = new();

    /// <summary>As imagens anexadas à entrada nova.</summary>
    public List<string> ImagensDaNovaEntrada { get; set; } = new();

    /// <summary>A entrada em edição; <c>null</c> quando nenhuma está.</summary>
    public string? EntradaEmEdicao { get; private set; }

    public string TextoEmEdicao { get; set; } = "";

    public List<string> ImagensEmEdicao { get; set; } = new();

    private string UrlDoDiario(string sufixo = "") => Tipo.Url(SheetId, sufixo == "" ? "diary" : $"diary/{sufixo}");

    public async Task CarregarDiarioAsync()
    {
        EntradasDoDiario = await Http.GetFromJsonAsync<List<DiaryEntryResponse>>(UrlDoDiario()) ?? new();
    }

    public async Task AdicionarEntradaAsync()
    {
        var response = await Http.PostAsJsonAsync(UrlDoDiario(), new CreateDiaryEntryRequest(FormularioDoDiario.Texto, ImagensDaNovaEntrada.ToList()));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível adicionar a entrada.");
            return;
        }

        FormularioDoDiario.Texto = "";
        ImagensDaNovaEntrada.Clear();
        await CarregarDiarioAsync();
        NotificarAlteracao();
    }

    /// <summary>Começa a editar com o texto da entrada e as imagens dela que estão na galeria (casadas pela URL).</summary>
    public void ComecarEdicao(DiaryEntryResponse entrada)
    {
        EntradaEmEdicao = entrada.Id;
        TextoEmEdicao = entrada.Texto;
        ImagensEmEdicao.Clear();
        foreach (var url in entrada.ImageUrls)
        {
            var imagem = MinhasImagens.FirstOrDefault(i => i.Url == url);
            if (imagem is not null)
                ImagensEmEdicao.Add(imagem.Id);
        }
        NotificarAlteracao();
    }

    public void CancelarEdicao()
    {
        EncerrarEdicao();
        NotificarAlteracao();
    }

    public async Task SalvarEntradaAsync(string id)
    {
        var response = await Http.PutAsJsonAsync(UrlDoDiario(id), new UpdateDiaryEntryRequest(TextoEmEdicao, ImagensEmEdicao.ToList()));
        if (!response.IsSuccessStatusCode)
        {
            MostrarErro("Não foi possível salvar a entrada.");
            return;
        }

        EncerrarEdicao();
        await CarregarDiarioAsync();
        NotificarAlteracao();
    }

    /// <summary>Como sempre: o DELETE não olha a resposta, e o Diário é relido de qualquer jeito.</summary>
    public async Task ExcluirEntradaAsync(string id)
    {
        await Http.DeleteAsync(UrlDoDiario(id));
        await CarregarDiarioAsync();
        NotificarAlteracao();
    }

    private void EncerrarEdicao()
    {
        EntradaEmEdicao = null;
        TextoEmEdicao = "";
        ImagensEmEdicao.Clear();
    }
}

/// <summary>O texto da entrada nova do Diário.</summary>
public sealed class DiaryFormModel
{
    public string Texto { get; set; } = "";
}
