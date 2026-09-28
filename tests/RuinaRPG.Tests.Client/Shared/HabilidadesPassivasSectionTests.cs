using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.SpellsAndAbilities;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class HabilidadesPassivasSectionTests : MudBunitContext
{
    private const string Url = "character-sheets/s1/spell-abilities";

    private static object Entrada(string id, string nome, string tipo, string? categoria = null, List<string>? pendentes = null) =>
        new { Id = id, Nome = nome, Tipo = tipo, Grau = 0, GastoEmPI = 0, Custo = 0, Descricao = $"Desc {nome}",
              Efeitos = new List<object>(), Categoria = categoria, RequisitosPendentes = pendentes };

    private static PassivaDisponivelResponse Disponivel(string id, string nome, params string[] pendencias) =>
        new(new SpellAbilityEntryResponse(id, nome, "Passiva", 0, 0, 0, "d", [], false, "Livre"), pendencias.ToList());

    /// <summary>
    /// MudSelect's dropdown is a real portal: its items render only inside a MudPopoverProvider
    /// (absent by default outside MainLayout) and only once the select is actually open — see
    /// AddEfeitoFormTests.RenderOpenAsync for the same limitation on a sibling component. Renders
    /// HabilidadesPassivasSection alongside a MudPopoverProvider in one composite fragment (so both
    /// land in the one returned markup) and opens the dropdown via the same MouseDown MudSelect's
    /// own input listens for.
    /// </summary>
    private async Task<IRenderedComponent<ContainerFragment>> RenderOpenAsync(string url)
    {
        var root = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<HabilidadesPassivasSection>(1);
            builder.AddAttribute(2, nameof(HabilidadesPassivasSection.SpellAbilitiesUrl), url);
            builder.CloseComponent();
        });
        await Task.Delay(50); // let the fake HTTP fetch + OnParametersSetAsync settle before opening
        root.Find(".mud-input-control").MouseDown();
        return root;
    }

    [Fact]
    public async Task Lists_only_passivas_with_categoria_and_the_warning_when_requisitos_are_unmet()
    {
        var http = FakeHttpMessageHandler.CreateClient(request => request.RequestUri!.AbsolutePath.EndsWith("passivas-disponiveis")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
            {
                Entrada("1", "Bola de Fogo", "Magia"),
                Entrada("2", "Pele de Pedra", "Passiva", "Vocacional", []),
                Entrada("3", "Força Bruta", "Passiva", "Livre", ["Força ≥ 4"]),
            }) });
        Services.AddScoped(_ => http);

        var cut = Render<HabilidadesPassivasSection>(p => p.Add(x => x.SpellAbilitiesUrl, Url));
        await Task.Delay(50);

        cut.Markup.Should().NotContain("Bola de Fogo");
        cut.Markup.Should().Contain("Pele de Pedra").And.Contain("Passiva Vocacional");
        cut.Markup.Should().Contain("Requisitos não cumpridos").And.Contain("Força ≥ 4");
    }

    [Fact]
    public async Task The_add_picker_disables_passivas_whose_requisitos_are_unmet_and_shows_why()
    {
        var http = FakeHttpMessageHandler.CreateClient(request => request.RequestUri!.AbsolutePath.EndsWith("passivas-disponiveis")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Disponivel("a", "Livre"), Disponivel("b", "Alta", "Nível 20") }) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
        Services.AddScoped(_ => http);

        var cut = await RenderOpenAsync(Url);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Alta — falta: Nível 20"));
        // MudSelect registers each MudSelectItem twice (once to track the option, once for the
        // popover's own rendered copy) — both instances share the same Disabled binding either way.
        // the option for "Alta" is disabled, "Livre" is not.
        cut.FindComponents<MudBlazor.MudSelectItem<string>>().Where(i => i.Instance.Value == "b").Should().OnlyContain(i => i.Instance.Disabled);
        cut.FindComponents<MudBlazor.MudSelectItem<string>>().Where(i => i.Instance.Value == "a").Should().OnlyContain(i => !i.Instance.Disabled);
    }

    [Fact]
    public async Task Adding_posts_the_bank_entry_id_and_reloads()
    {
        string? posted = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                posted = request.Content!.ReadAsStringAsync().Result;
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { Id = "n" }) };
            }
            return request.RequestUri!.AbsolutePath.EndsWith("passivas-disponiveis")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Disponivel("a", "Livre") }) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
        });
        Services.AddScoped(_ => http);

        var cut = Render<HabilidadesPassivasSection>(p => p.Add(x => x.SpellAbilitiesUrl, Url));
        await Task.Delay(50);
        await cut.InvokeAsync(() => cut.FindComponent<MudBlazor.MudSelect<string>>().Instance.ValueChanged.InvokeAsync("a"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar Passiva")).Click();
        await Task.Delay(50);

        posted.Should().Contain("\"sourceBankEntryId\":\"a\"");
    }

    /// <summary>
    /// Finding 3 of the final whole-branch review: the server (not the client) is the one that
    /// refuses adding the same Passiva twice — this only checks the section surfaces that 400
    /// verbatim, the same way it already does for any other server rejection.
    /// </summary>
    [Fact]
    public async Task Adding_a_duplicate_passiva_shows_the_servers_error_message()
    {
        const string serverMessage = "Esta Passiva já está na ficha.";
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(serverMessage) };
            return request.RequestUri!.AbsolutePath.EndsWith("passivas-disponiveis")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Disponivel("a", "Livre") }) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
        });
        Services.AddScoped(_ => http);

        var cut = Render<HabilidadesPassivasSection>(p => p.Add(x => x.SpellAbilitiesUrl, Url));
        await Task.Delay(50);
        await cut.InvokeAsync(() => cut.FindComponent<MudBlazor.MudSelect<string>>().Instance.ValueChanged.InvokeAsync("a"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar Passiva")).Click();
        await Task.Delay(50);

        cut.Markup.Should().Contain(serverMessage);
    }
}
