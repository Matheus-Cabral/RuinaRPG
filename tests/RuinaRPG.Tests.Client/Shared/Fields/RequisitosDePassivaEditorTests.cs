using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class RequisitosDePassivaEditorTests : MudBunitContext
{
    // EstrelaSelect e HistoricoSelect (campos reaproveitados dentro do editor) buscam seus próprios
    // catálogos ao montar — mesmo stub usado em BancoDeMagiasFormTests para essas duas rotas.
    private void RegisterCatalogStubs() => Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("historicos"))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<HistoricoResponse>()) };
        if (request.RequestUri!.AbsolutePath.EndsWith("estrelas-alkerianas"))
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new RulebookDocumentResponse("estrelas-alkerianas", "As Estrelas", null, [])),
            };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
    }));

    // MudDialogProvider idiom: EstrelaSelect/HistoricoSelect nest an InfoPopup/MudDialog that only
    // renders through a provider elsewhere in the tree — same pattern as BancoDeMagiasFormTests.
    private IRenderedComponent<ContainerFragment> RenderEditor(RequisitosFormModel model, EventCallback onChanged) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<RequisitosDePassivaEditor>(1);
            builder.AddAttribute(2, nameof(RequisitosDePassivaEditor.Model), model);
            builder.AddAttribute(3, nameof(RequisitosDePassivaEditor.OnChanged), onChanged);
            builder.CloseComponent();
        });

    [Fact]
    public async Task Adicionar_appends_a_blank_row_to_Atributos_and_notifies_OnChanged()
    {
        RegisterCatalogStubs();
        var model = new RequisitosFormModel();
        var changed = false;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed = true));
        await Task.Delay(50);

        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar Atributo")).Click();

        model.Atributos.Should().ContainSingle();
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task Adicionar_appends_a_blank_row_to_Pericias_and_notifies_OnChanged()
    {
        RegisterCatalogStubs();
        var model = new RequisitosFormModel();
        var changed = false;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed = true));
        await Task.Delay(50);

        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar Perícia")).Click();

        model.Pericias.Should().ContainSingle();
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task Remover_removes_the_row_from_Atributos_and_notifies_OnChanged()
    {
        RegisterCatalogStubs();
        var model = new RequisitosFormModel { Atributos = [new RequisitoMinimoLinha { Alvo = "Forca", Minimo = 3 }] };
        var changed = false;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed = true));
        await Task.Delay(50);

        cut.Find("button[title='Remover Atributo']").Click();

        model.Atributos.Should().BeEmpty();
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task An_Alvo_chosen_on_one_row_is_disabled_on_another_rows_options_and_re_enabled_after_removal()
    {
        RegisterCatalogStubs();
        var model = new RequisitosFormModel
        {
            Atributos = [new RequisitoMinimoLinha { Alvo = "Forca", Minimo = 1 }, new RequisitoMinimoLinha()],
        };

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => { }));
        await Task.Delay(50);

        var rowSelects = cut.FindComponents<MudSelect<string>>().Where(s => s.Instance.Label is null).ToList();
        rowSelects.Should().HaveCount(2);
        var row1 = rowSelects[0];
        var row2 = rowSelects[1];

        // "Força" já escolhida na linha 1 não pode ser escolhida de novo na linha 2...
        row2.FindComponents<MudSelectItem<string>>().Single(i => i.Instance.Value == "Forca").Instance.Disabled.Should().BeTrue();
        // ...mas continua habilitada na própria linha 1 (senão a linha não conseguiria manter seu próprio valor).
        row1.FindComponents<MudSelectItem<string>>().Single(i => i.Instance.Value == "Forca").Instance.Disabled.Should().BeFalse();

        cut.Find("button[title='Remover Atributo']").Click();

        var remainingSelect = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label is null);
        remainingSelect.FindComponents<MudSelectItem<string>>().Single(i => i.Instance.Value == "Forca").Instance.Disabled.Should().BeFalse();
    }

    [Fact]
    public async Task Limpar_Vocacao_Classe_clears_both_fields_and_notifies_OnChanged()
    {
        RegisterCatalogStubs();
        var model = new RequisitosFormModel { Vocacao = "Bruxo", Classe = "Ocultista" };
        var changed = false;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed = true));
        await Task.Delay(50);

        cut.Find("button[title='Limpar Vocação/Classe']").Click();

        model.Vocacao.Should().BeNull();
        model.Classe.Should().BeNull();
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task Limpar_Afinidade_clears_the_field_and_notifies_OnChanged()
    {
        RegisterCatalogStubs();
        var model = new RequisitosFormModel { Afinidade = "Fogo" };
        var changed = false;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed = true));
        await Task.Delay(50);

        cut.Find("button[title='Limpar Afinidade']").Click();

        model.Afinidade.Should().BeNull();
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task Limpar_Historico_clears_the_field_and_notifies_OnChanged()
    {
        RegisterCatalogStubs();
        var model = new RequisitosFormModel { HistoricoId = "hist-1" };
        var changed = false;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed = true));
        await Task.Delay(50);

        cut.Find("button[title='Limpar Histórico']").Click();

        model.HistoricoId.Should().BeNull();
        changed.Should().BeTrue();
    }
}
