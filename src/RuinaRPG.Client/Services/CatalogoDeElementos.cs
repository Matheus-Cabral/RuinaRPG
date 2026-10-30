using System.Net.Http.Json;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Afinidades;

namespace RuinaRPG.Client.Services;

/// <summary>
/// O catálogo único de elementos de Afinidade do Auditor (GET api/afinidades/catalogo), carregado uma vez por sessão
/// do app, como <see cref="PericiaCatalogo"/>. Fichas, Criaturas, Condições e Passivas guardam o id do elemento; o
/// nome exibido sai daqui (pelas mesmas regras do Domain, <see cref="CatalogoDeAfinidades"/>), então renomear um
/// elemento na Auditoria muda o texto em todo lugar.
/// </summary>
public class CatalogoDeElementos(HttpClient http)
{
    private static readonly CatalogoDeAfinidades Vazio = new([], [], new Dictionary<int, int>(), []);

    private CatalogoDeAfinidades? _catalogo;
    private Task? _carregando;

    public CatalogoDeAfinidades Catalogo => _catalogo ?? Vazio;

    /// <summary>Os elementos que podem ser uma Afinidade (campo da ficha, alvo de Condição, requisito): todos menos os Caminhos.</summary>
    public IReadOnlyList<ElementoDoCatalogo> Afinidades => Catalogo.Afinidades;

    /// <summary>
    /// Não faz nada se já carregou. Uma resposta de erro não derruba a página que chamou: o catálogo fica vazio (os
    /// nomes caem no próprio id) e a próxima chamada tenta de novo.
    /// </summary>
    public Task CarregarAsync()
    {
        if (_catalogo is not null)
            return Task.CompletedTask;
        // Vários componentes inicializam juntos: quem chega durante a requisição espera a mesma.
        if (_carregando is not null)
            return _carregando;

        var tarefa = BuscarAsync();
        _carregando = tarefa.IsCompleted ? null : tarefa;
        return tarefa;
    }

    private async Task BuscarAsync()
    {
        try
        {
            var response = await http.GetAsync("afinidades/catalogo");
            if (!response.IsSuccessStatusCode)
                return;
            var elementos = await response.Content.ReadFromJsonAsync<List<ElementoDeAfinidadeResponse>>() ?? new();
            _catalogo = Vazio with
            {
                Elementos = elementos
                    .Select(e => new ElementoDoCatalogo(e.Id, e.Nome, Enum.TryParse<TipoDeElemento>(e.Tipo, out var t) ? t : TipoDeElemento.SubElemento, e.Teto))
                    .ToList()
            };
        }
        finally
        {
            _carregando = null;
        }
    }

    public string Nome(int id) => Catalogo.NomeDe(id);

    /// <summary>O nome do alvo específico de uma linha de Condição (o id em texto); um valor que não é id aparece como está.</summary>
    public string NomeDoAlvo(string valor) => Catalogo.NomeDoAlvo(valor);
}
