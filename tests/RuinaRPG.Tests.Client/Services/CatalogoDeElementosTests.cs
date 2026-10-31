using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class CatalogoDeElementosTests
{
    [Fact]
    public async Task RecarregarAsync_le_de_novo_o_catalogo_para_os_nomes_nao_ficarem_velhos()
    {
        var elementos = new List<ElementoDeAfinidadeResponse> { new(0, "Terra", "Base", null) };
        var chamadas = 0;
        var catalogo = new CatalogoDeElementos(FakeHttpMessageHandler.CreateClient(_ =>
        {
            chamadas++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(elementos) };
        }));
        await catalogo.CarregarAsync();
        catalogo.Nome(0).Should().Be("Terra");

        elementos[0] = new(0, "Terra Viva", "Base", null);
        await catalogo.CarregarAsync(); // já carregado: não pede de novo
        catalogo.Nome(0).Should().Be("Terra");

        await catalogo.RecarregarAsync();

        chamadas.Should().Be(2);
        catalogo.Nome(0).Should().Be("Terra Viva");
    }
}
