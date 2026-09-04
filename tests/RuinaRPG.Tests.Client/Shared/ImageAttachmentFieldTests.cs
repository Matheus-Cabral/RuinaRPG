using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Images;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ImageAttachmentFieldTests : MudBunitContext
{
    private static readonly List<ImageSummaryResponse> TwoImages =
    [
        new("img-1", "https://cdn.example/1.png", DateTime.UtcNow),
        new("img-2", "https://cdn.example/2.png", DateTime.UtcNow),
    ];

    [Fact]
    public void The_upload_and_choose_buttons_render_side_by_side_in_one_flex_row()
    {
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string>()));

        var row = cut.Find(".raf-image-attachment-buttons");
        row.GetAttribute("style").Should().Contain("display:flex");
        row.TextContent.Should().Contain("Enviar nova imagem").And.Contain("Escolher imagens já enviadas");
    }

    [Fact]
    public void Multiple_false_uses_the_singular_choose_label()
    {
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string>())
            .Add(x => x.Multiple, false));

        cut.Markup.Should().Contain("Escolher imagem já enviada");
        cut.Markup.Should().NotContain("Escolher imagens já enviadas");
    }

    [Fact]
    public void Selected_images_render_as_thumbnails_in_a_flex_start_grid_below_the_buttons()
    {
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string> { "img-1", "img-2" }));

        var grid = cut.Find(".raf-image-attachment-grid");
        grid.GetAttribute("style").Should().Contain("display:flex").And.Contain("justify-content:flex-start");
        grid.QuerySelectorAll("img").Should().HaveCount(2);
    }

    [Fact]
    public void No_grid_is_rendered_when_nothing_is_selected()
    {
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string>()));

        cut.FindAll(".raf-image-attachment-grid").Should().BeEmpty();
    }

    [Fact]
    public async Task Multiple_mode_picking_an_unselected_image_adds_it_without_dropping_the_existing_selection()
    {
        List<string>? changed = null;
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string> { "img-1" })
            .Add(x => x.SelectedIdsChanged, v => changed = v));

        await cut.InvokeAsync(() => cut.Instance.PickForTests("img-2"));

        changed.Should().BeEquivalentTo(new[] { "img-1", "img-2" });
    }

    [Fact]
    public async Task Multiple_mode_picking_an_already_selected_image_toggles_it_off()
    {
        List<string>? changed = null;
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string> { "img-1", "img-2" })
            .Add(x => x.SelectedIdsChanged, v => changed = v));

        await cut.InvokeAsync(() => cut.Instance.PickForTests("img-1"));

        changed.Should().BeEquivalentTo(new[] { "img-2" });
    }

    [Fact]
    public async Task Single_select_mode_picking_an_image_replaces_the_current_selection()
    {
        List<string>? changed = null;
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string> { "img-1" })
            .Add(x => x.SelectedIdsChanged, v => changed = v)
            .Add(x => x.Multiple, false));

        await cut.InvokeAsync(() => cut.Instance.PickForTests("img-2"));

        changed.Should().BeEquivalentTo(new[] { "img-2" });
    }

    [Fact]
    public async Task Single_select_mode_picking_null_clears_the_selection()
    {
        List<string>? changed = null;
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string> { "img-1" })
            .Add(x => x.SelectedIdsChanged, v => changed = v)
            .Add(x => x.Multiple, false));

        await cut.InvokeAsync(() => cut.Instance.PickForTests(null));

        changed.Should().BeEmpty();
    }

    [Fact]
    public async Task Removing_a_thumbnail_drops_only_that_image_from_the_selection()
    {
        List<string>? changed = null;
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, TwoImages)
            .Add(x => x.SelectedIds, new List<string> { "img-1", "img-2" })
            .Add(x => x.SelectedIdsChanged, v => changed = v));

        await cut.InvokeAsync(() => cut.Instance.RemoveForTests("img-1"));

        changed.Should().BeEquivalentTo(new[] { "img-2" });
    }

    [Fact]
    public async Task Uploading_a_file_posts_it_to_images_adds_it_to_AvailableImages_and_selects_it()
    {
        HttpRequestMessage? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ImageUploadResponse("new-id", "https://cdn.example/new.png")),
            };
        });
        Services.AddScoped(_ => http);

        var availableImages = new List<ImageSummaryResponse>(TwoImages);
        List<string>? changed = null;
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, availableImages)
            .Add(x => x.SelectedIds, new List<string>())
            .Add(x => x.SelectedIdsChanged, v => changed = v)
            .Add(x => x.Multiple, false));

        await cut.InvokeAsync(() => cut.Instance.UploadForTests(new FakeBrowserFile("foto.png")));

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.ToString().Should().EndWith("images");
        availableImages.Should().ContainSingle(i => i.Id == "new-id" && i.Url == "https://cdn.example/new.png");
        changed.Should().BeEquivalentTo(new[] { "new-id" });
    }

    [Fact]
    public async Task A_failed_upload_reports_the_error_and_does_not_change_the_selection()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Services.AddScoped(_ => http);

        string? error = null;
        List<string>? changed = null;
        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, new List<ImageSummaryResponse>(TwoImages))
            .Add(x => x.SelectedIds, new List<string>())
            .Add(x => x.SelectedIdsChanged, v => changed = v)
            .Add(x => x.OnError, v => error = v));

        await cut.InvokeAsync(() => cut.Instance.UploadForTests(new FakeBrowserFile("foto.png")));

        error.Should().Be("Não foi possível enviar a imagem.");
        changed.Should().BeNull();
    }
}
