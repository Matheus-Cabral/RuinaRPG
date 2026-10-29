using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.Players;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class GmJogadoresTests : MudBunitContext
{
    private readonly List<PlayerSearchResultResponse> _jogadores = new();
    private readonly List<(HttpMethod Method, string Path, string? Body)> _chamadas = new();

    private IRenderedComponent<ContainerFragment> Renderizar(params PlayerSearchResultResponse[] jogadores)
    {
        _jogadores.AddRange(jogadores);
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("gm");
        authContext.SetRoles("GM");
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_jogadores.ToList()) };

            _chamadas.Add((request.Method, path, request.Content?.ReadAsStringAsync().Result));
            var id = path.Split('/')[^2];
            var acao = path.Split('/')[^1];
            var i = _jogadores.FindIndex(j => j.Id == id);
            if (acao == "desativar") _jogadores[i] = _jogadores[i] with { Estado = "Desativada" };
            if (acao == "reativar") _jogadores[i] = _jogadores[i] with { Estado = "Ativa" };
            if (acao == "excluir") _jogadores.RemoveAt(i);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<GmJogadores>(1);
            builder.CloseComponent();
        });
    }

    private static PlayerSearchResultResponse Ativa(string id, string nick) => new(id, nick, $"{nick}@x.com", "Ativa");
    private static PlayerSearchResultResponse Desativada(string id, string nick) => new(id, nick, $"{nick}@x.com", "Desativada");

    [Fact]
    public void Ativos_e_desativados_em_secoes_separadas()
    {
        var cut = Renderizar(Ativa("1", "Ana"), Desativada("2", "Beto"));

        cut.WaitForAssertion(() =>
        {
            var ativos = cut.Find("[data-testid=jogadores-ativos]");
            ativos.TextContent.Should().Contain("Ana").And.NotContain("Beto");
            ativos.TextContent.Should().Contain("Desativar").And.Contain("Excluir").And.Contain("Redefinir senha");
            var desativados = cut.Find("[data-testid=jogadores-desativados]");
            desativados.TextContent.Should().Contain("Desativados").And.Contain("Beto").And.NotContain("Ana");
            desativados.TextContent.Should().Contain("Reativar").And.Contain("Excluir").And.Contain("Redefinir senha");
        });
    }

    [Fact]
    public void Secao_desativados_some_sem_desativados()
    {
        var cut = Renderizar(Ativa("1", "Ana"));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Ana"));
        cut.FindAll("[data-testid=jogadores-desativados]").Should().BeEmpty();
        cut.Markup.Should().NotContain("Desativados");
    }

    [Fact]
    public void Desativar_chama_o_endpoint_e_move_o_jogador()
    {
        var cut = Renderizar(Ativa("1", "Ana"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Ana"));

        cut.Find("[data-testid=jogadores-ativos] [data-testid=desativar-1]").Click();

        cut.WaitForAssertion(() =>
        {
            _chamadas.Should().ContainSingle(c => c.Path.EndsWith("players/1/desativar") && c.Method == HttpMethod.Post);
            cut.Find("[data-testid=jogadores-desativados]").TextContent.Should().Contain("Ana");
            cut.Find("[data-testid=jogadores-ativos]").TextContent.Should().NotContain("Ana");
        });
    }

    [Fact]
    public void Reativar_chama_o_endpoint_e_move_o_jogador()
    {
        var cut = Renderizar(Desativada("2", "Beto"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Beto"));

        cut.Find("[data-testid=reativar-2]").Click();

        cut.WaitForAssertion(() =>
        {
            _chamadas.Should().ContainSingle(c => c.Path.EndsWith("players/2/reativar"));
            cut.Find("[data-testid=jogadores-ativos]").TextContent.Should().Contain("Beto");
            cut.FindAll("[data-testid=jogadores-desativados]").Should().BeEmpty();
        });
    }

    [Fact]
    public void Excluir_so_habilita_com_o_apelido_certo()
    {
        var cut = Renderizar(Ativa("1", "Ana"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Ana"));
        cut.Find("[data-testid=excluir-1]").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid=confirmar-exclusao]").HasAttribute("disabled").Should().BeTrue());

        cut.Find("input[data-testid=apelido-exclusao]").Input("ana");
        cut.Find("[data-testid=confirmar-exclusao]").HasAttribute("disabled").Should().BeTrue();

        cut.Find("input[data-testid=apelido-exclusao]").Input("Ana");
        cut.Find("[data-testid=confirmar-exclusao]").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Excluir_envia_o_apelido()
    {
        var cut = Renderizar(Ativa("1", "Ana"));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Ana"));
        cut.Find("[data-testid=excluir-1]").Click();
        cut.WaitForAssertion(() => cut.Find("input[data-testid=apelido-exclusao]"));
        cut.Find("input[data-testid=apelido-exclusao]").Input("Ana");

        cut.Find("[data-testid=confirmar-exclusao]").Click();

        cut.WaitForAssertion(() =>
        {
            var chamada = _chamadas.Should().ContainSingle(c => c.Path.EndsWith("players/1/excluir")).Subject;
            chamada.Body.Should().Contain("\"nickname\":\"Ana\"");
            cut.Find("[data-testid=jogadores-ativos]").TextContent.Should().NotContain("Ana");
        });
    }
}
