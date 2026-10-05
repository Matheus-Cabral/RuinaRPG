using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class ItemFieldsEditorTests : MudBunitContext
{
    private void RegisterStubs() => Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) }));

    private IRenderedComponent<ItemFieldsEditor> RenderEditor(ItemFormModel model, bool tipoBloqueado = false,
        bool mostrarImagem = true, EventCallback? onChanged = null)
    {
        RegisterStubs();
        return Render<ItemFieldsEditor>(p =>
        {
            p.Add(x => x.Model, model);
            p.Add(x => x.TipoBloqueado, tipoBloqueado);
            p.Add(x => x.MostrarImagem, mostrarImagem);
            if (onChanged is { } cb)
                p.Add(x => x.OnChanged, cb);
        });
    }

    [Theory]
    [InlineData("ItemGeral", "Capacidade Extra")]
    [InlineData("Arma", "Dano")]
    [InlineData("Armadura", "Defesa")]
    [InlineData("Escudo", "Bônus de Defesa")]
    [InlineData("Artefato", "Valor")]
    public void Renders_the_fields_of_the_models_tipo(string tipo, string campo)
    {
        var cut = RenderEditor(new ItemFormModel { Tipo = tipo });

        cut.Markup.Should().Contain(campo);
        cut.Markup.Should().Contain("Nome");
    }

    [Fact]
    public void TipoBloqueado_disables_the_tipo_select()
    {
        var cut = RenderEditor(new ItemFormModel(), tipoBloqueado: true);

        cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Tipo").Instance.Disabled.Should().BeTrue();
    }

    [Fact]
    public void Tipo_select_is_enabled_when_not_blocked()
    {
        var cut = RenderEditor(new ItemFormModel());

        cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Tipo").Instance.Disabled.Should().BeFalse();
    }

    [Fact]
    public void MostrarImagem_false_hides_the_image_field()
    {
        RenderEditor(new ItemFormModel(), mostrarImagem: false).FindComponents<ImageAttachmentField>().Should().BeEmpty();
    }

    [Fact]
    public void MostrarImagem_true_shows_the_image_field()
    {
        RenderEditor(new ItemFormModel(), mostrarImagem: true).FindComponents<ImageAttachmentField>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Changing_a_field_raises_OnChanged()
    {
        var changed = 0;
        var model = new ItemFormModel();
        var cut = RenderEditor(model, onChanged: EventCallback.Factory.Create(this, () => changed++));

        var nome = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Poção"));

        model.Nome.Should().Be("Poção");
        changed.Should().BeGreaterThan(0);
    }
}
