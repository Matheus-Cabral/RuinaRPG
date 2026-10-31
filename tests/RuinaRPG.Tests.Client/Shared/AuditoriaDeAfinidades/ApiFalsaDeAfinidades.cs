using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Client.Shared.AuditoriaDeAfinidades;

/// <summary>
/// A API da Auditoria de Afinidades em memória, para as seções: o catálogo (GET afinidades/catalogo) e as listas
/// mudam com as escritas, como na API real. <see cref="Recusa"/> responde a próxima escrita com esse status e texto.
/// </summary>
public sealed class ApiFalsaDeAfinidades
{
    public sealed record Pedido(string Metodo, string Caminho, string? Corpo)
    {
        public T Ler<T>() => JsonSerializer.Deserialize<T>(Corpo!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    public List<ElementoDeAfinidadeResponse> Elementos { get; } =
    [
        new(1, "Água", "Base", null), new(3, "Ar", "Base", null), new(4, "Gelo", "SubElemento", 5),
        new(7, "Raio", "SubElemento", null), new(9, "Alma", "Caminho", null),
    ];

    public List<TotalDeAfinidadePorCirculoResponse> Totais { get; } = [new(0, 3), new(1, 6), new(2, 9)];

    public List<CruzamentoDeAfinidadeResponse> Cruzamentos { get; } =
        [new(Guid.Parse("00000000-0000-0000-0000-0000000000a1"), 1, 1, 3, 2, 4)];

    public List<VisibilidadeDeElementoResponse> Visibilidade { get; } =
        [new("Vocacao", "Feiticeiro", 9), new("Classe", "Aeromante", 7)];

    public List<Pedido> Escritas { get; } = [];
    public int LeiturasDoCatalogo { get; private set; }
    public (HttpStatusCode Status, string Texto)? Recusa { get; set; }

    public HttpClient Cliente() => FakeHttpMessageHandler.CreateClient(Responder);

    private HttpResponseMessage Responder(HttpRequestMessage request)
    {
        var caminho = request.RequestUri!.AbsolutePath.Replace("/api/", "", StringComparison.Ordinal);
        if (request.Method == HttpMethod.Get)
        {
            if (caminho == "afinidades/catalogo") LeiturasDoCatalogo++;
            return caminho switch
            {
                "afinidades/catalogo" => Ok(Elementos.OrderBy(e => e.Id).ToList()),
                "afinidades/totais-por-circulo" => Ok(Totais.OrderBy(t => t.Circulo).ToList()),
                "afinidades/cruzamentos" => Ok(Cruzamentos.ToList()),
                "afinidades/visibilidade" => Ok(Visibilidade.ToList()),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        var pedido = new Pedido(request.Method.Method, caminho, request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
        Escritas.Add(pedido);
        if (Recusa is { } recusa)
        {
            Recusa = null;
            return new HttpResponseMessage(recusa.Status) { Content = new StringContent(recusa.Texto) };
        }

        var partes = caminho.Split('/');
        switch (partes[1], request.Method.Method)
        {
            case ("totais-por-circulo", "PUT"):
                var circulo = int.Parse(partes[2]);
                Totais[Totais.FindIndex(t => t.Circulo == circulo)] = new(circulo, pedido.Ler<SalvarTotalDeAfinidadePorCirculoRequest>().Total);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("elementos", "POST"):
                var novo = pedido.Ler<SalvarElementoDeAfinidadeRequest>();
                var criado = new ElementoDeAfinidadeResponse(Elementos.Max(e => e.Id) + 1, novo.Nome, novo.Tipo, novo.Teto);
                Elementos.Add(criado);
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(criado) };
            case ("elementos", "PUT"):
                var id = int.Parse(partes[2]);
                var editado = pedido.Ler<SalvarElementoDeAfinidadeRequest>();
                Elementos[Elementos.FindIndex(e => e.Id == id)] = new(id, editado.Nome, editado.Tipo, editado.Teto);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("elementos", "DELETE"):
                Elementos.RemoveAll(e => e.Id == int.Parse(partes[2]));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("cruzamentos", "POST"):
                var c = pedido.Ler<SalvarCruzamentoDeAfinidadeRequest>();
                var cruzamento = new CruzamentoDeAfinidadeResponse(Guid.NewGuid(), c.ElementoAId, c.MinimoA, c.ElementoBId, c.MinimoB, c.SubElementoId);
                Cruzamentos.Add(cruzamento);
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(cruzamento) };
            case ("cruzamentos", "PUT"):
                var cid = Guid.Parse(partes[2]);
                var e2 = pedido.Ler<SalvarCruzamentoDeAfinidadeRequest>();
                Cruzamentos[Cruzamentos.FindIndex(x => x.Id == cid)] = new(cid, e2.ElementoAId, e2.MinimoA, e2.ElementoBId, e2.MinimoB, e2.SubElementoId);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("cruzamentos", "DELETE"):
                Cruzamentos.RemoveAll(x => x.Id == Guid.Parse(partes[2]));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case ("visibilidade", "PUT"):
                var v = pedido.Ler<AlterarVisibilidadeDeElementoRequest>();
                var ligacao = new VisibilidadeDeElementoResponse(v.Origem, v.Chave, v.ElementoId);
                Visibilidade.Remove(ligacao);
                if (v.Visivel) Visibilidade.Add(ligacao);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static HttpResponseMessage Ok<T>(T corpo) => new(HttpStatusCode.OK) { Content = JsonContent.Create(corpo) };
}
