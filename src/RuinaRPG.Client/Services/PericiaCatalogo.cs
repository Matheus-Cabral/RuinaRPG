using System.Net.Http.Json;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Client.Services;

/// <summary>
/// Perícias ativas (GET api/pericias), carregadas uma vez por sessão do app. Substitui o antigo
/// PericiaDisplay estático: nomes e descrições agora vêm da Auditoria de Perícias. A Chave continua
/// sendo o valor que os endpoints de ficha recebem e devolvem.
/// </summary>
public class PericiaCatalogo(HttpClient http)
{
    private List<PericiaResponse>? _ativas;

    public IReadOnlyList<PericiaResponse> Ativas => _ativas ?? (IReadOnlyList<PericiaResponse>)Array.Empty<PericiaResponse>();

    /// <summary>
    /// Não faz nada se já carregou. Uma resposta de erro não derruba a página que chamou: o catálogo
    /// fica vazio (os rótulos caem na própria Chave) e a próxima chamada tenta de novo.
    /// </summary>
    public async Task CarregarAsync()
    {
        if (_ativas is not null)
            return;

        var response = await http.GetAsync("pericias");
        if (response.IsSuccessStatusCode)
            _ativas = await response.Content.ReadFromJsonAsync<List<PericiaResponse>>() ?? new();
    }

    /// <summary>Recarrega após uma edição na Auditoria.</summary>
    public async Task RecarregarAsync()
    {
        _ativas = null;
        await CarregarAsync();
    }

    public string Label(string chave) => _ativas?.FirstOrDefault(p => p.Chave == chave)?.Nome ?? chave;

    public string? Descricao(string chave) => _ativas?.FirstOrDefault(p => p.Chave == chave)?.Descricao;
}
