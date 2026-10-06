using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class PenalidadeDeEquipamentoEditorTests : MudBunitContext
{
    // PericiaCatalogo (injetado pelo editor) busca "pericias" ao montar.
    private void RegisterStubs() => Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) }));

    private IRenderedComponent<ContainerFragment> RenderEditor(PenalidadeFormModel model, EventCallback onChanged) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<PenalidadeDeEquipamentoEditor>(1);
            builder.AddAttribute(2, nameof(PenalidadeDeEquipamentoEditor.Model), model);
            builder.AddAttribute(3, nameof(PenalidadeDeEquipamentoEditor.OnChanged), onChanged);
            builder.CloseComponent();
        });

    [Fact]
    public async Task Adding_an_attribute_line_adds_a_row_with_value_1_and_raises_OnChanged()
    {
        RegisterStubs();
        var model = new PenalidadeFormModel();
        var changed = 0;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed++));
        await Task.Delay(50);

        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar Atributo")).Click();

        model.Atributos.Should().ContainSingle().Which.Valor.Should().Be(1);
        changed.Should().Be(1);
    }

    [Fact]
    public async Task Removing_a_line_removes_it_and_raises_OnChanged()
    {
        RegisterStubs();
        var model = new PenalidadeFormModel { Atributos = [new() { Alvo = "Forca", Valor = 2 }] };
        var changed = 0;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed++));
        await Task.Delay(50);

        cut.Find("button[title='Remover Atributo']").Click();

        model.Atributos.Should().BeEmpty();
        changed.Should().Be(1);
    }

    [Fact]
    public async Task A_target_already_chosen_in_another_line_is_disabled()
    {
        RegisterStubs();
        var model = new PenalidadeFormModel { Atributos = [new() { Alvo = "Forca", Valor = 1 }, new()] };

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => { }));
        await Task.Delay(50);

        var selects = cut.FindComponents<MudSelect<string>>().ToList();
        selects.Should().HaveCount(2);
        selects[1].Find(".mud-input-control").MouseDown();

        var opcoes = cut.FindAll(".mud-list-item");
        opcoes.Single(o => o.TextContent.Trim() == "Força").ClassList.Should().Contain("mud-list-item-disabled");
        // na própria linha a opção escolhida continua habilitada
        selects[1].FindComponents<MudSelectItem<string>>().Single(i => i.Instance.Value == "Forca").Instance.Disabled.Should().BeTrue();
        selects[0].FindComponents<MudSelectItem<string>>().Single(i => i.Instance.Value == "Forca").Instance.Disabled.Should().BeFalse();
    }

    [Fact]
    public async Task The_free_text_field_is_labelled_Outras_penalidades_and_binds_to_Texto()
    {
        RegisterStubs();
        var model = new PenalidadeFormModel();
        var changed = 0;

        var cut = RenderEditor(model, EventCallback.Factory.Create(this, () => changed++));
        await Task.Delay(50);

        var campo = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Outras penalidades");
        await cut.InvokeAsync(() => campo.Instance.ValueChanged.InvokeAsync("Barulhenta"));

        model.Texto.Should().Be("Barulhenta");
        changed.Should().Be(1);
    }
}
