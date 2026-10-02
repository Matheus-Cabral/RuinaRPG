using System.Net;
using System.Text;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class HistoriaEditorTests : MudBunitContext
{
    private readonly List<(string Method, string Path, string Body)> _requests = new();

    private void RegisterApi(HttpStatusCode status, string responseBody = "") =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            lock (_requests) _requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body));
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "text/plain") };
        }));

    [Fact]
    public async Task A_change_in_the_editor_is_saved_to_SaveUrl()
    {
        RegisterApi(HttpStatusCode.NoContent);
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.Html, "<p>a</p>"));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>Nasceu em Alkeria</p>"));

        cut.WaitForAssertion(() =>
        {
            lock (_requests) _requests.Should().ContainSingle(r => r.Method == "PUT");
        }, TimeSpan.FromSeconds(3));
        var request = _requests.Single(r => r.Method == "PUT");
        request.Path.Should().Be("/api/character-sheets/abc/historia");
        request.Body.Should().Contain("Nasceu em Alkeria");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Salvo às"), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task A_change_updates_the_bound_Html_immediately()
    {
        RegisterApi(HttpStatusCode.NoContent);
        string? bound = null;
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "npc-sheets/abc/historia")
            .Add(x => x.HtmlChanged, (string? v) => bound = v));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>novo</p>"));

        bound.Should().Be("<p>novo</p>");
    }

    [Fact]
    public async Task A_400_from_the_api_is_reported_through_OnError_and_the_indicator_shows_the_error()
    {
        RegisterApi(HttpStatusCode.BadRequest, "A História pode ter no máximo 200.000 caracteres.");
        string? error = null;
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.OnError, (string e) => error = e));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>longo demais</p>"));

        cut.WaitForAssertion(() => error.Should().Be("A História pode ter no máximo 200.000 caracteres."), TimeSpan.FromSeconds(3));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Erro ao salvar"), TimeSpan.FromSeconds(3));
    }

    // ---------------------------------------------------------------- ajuda, galeria e imagens no texto

    private const string GalleryPath = "/api/character-sheets/abc/historia/imagens";

    /// <summary>API de mentira: GET/PUT da galeria e POST images; tudo o mais responde 204.</summary>
    private void RegisterGalleryApi(List<HistoriaImagemResponse> gallery, HttpStatusCode putStatus = HttpStatusCode.OK, string putError = "",
        HttpStatusCode uploadStatus = HttpStatusCode.Created, string uploadError = "") =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            lock (_requests) _requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body));
            if (request.RequestUri.AbsolutePath == GalleryPath && request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(gallery) };
            if (request.RequestUri.AbsolutePath == GalleryPath && request.Method == HttpMethod.Put)
                return putStatus == HttpStatusCode.OK
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(gallery) }
                    : new HttpResponseMessage(putStatus) { Content = new StringContent(putError, Encoding.UTF8, "text/plain") };
            if (request.RequestUri.AbsolutePath == "/api/images")
                return uploadStatus == HttpStatusCode.Created
                    ? new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new ImageUploadResponse("nova", "/images/nova.png")) }
                    : new HttpResponseMessage(uploadStatus) { Content = new StringContent(uploadError, Encoding.UTF8, "text/plain") };
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

    private static List<ImageSummaryResponse> MyImages() =>
    [
        new("minha-1", "/images/minha-1.png", DateTime.UtcNow),
        new("minha-2", "/images/minha-2.png", DateTime.UtcNow),
    ];

    private List<(string Method, string Path, string Body)> Requests(string method, string path)
    {
        lock (_requests) return _requests.Where(r => r.Method == method && r.Path == path).ToList();
    }

    private sealed class FakeJsStream(byte[] bytes) : IJSStreamReference
    {
        public long Length => bytes.Length;
        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            bytes.Length > maxAllowedSize ? throw new ArgumentOutOfRangeException(nameof(maxAllowedSize)) : ValueTask.FromResult<Stream>(new MemoryStream(bytes));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void An_info_popup_next_to_the_title_explains_how_images_work()
    {
        RegisterGalleryApi([]);
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<HistoriaEditor>(1);
            builder.AddAttribute(2, nameof(HistoriaEditor.SaveUrl), "character-sheets/abc/historia");
            builder.CloseComponent();
        });

        var popup = cut.FindComponent<InfoPopup>();
        popup.Find("button").Click();

        cut.Markup.Should().Contain("Você pode inserir imagens dentro do texto (botão Inserir imagem) ou anexá-las na galeria abaixo. " +
            "Só são aceitas imagens enviadas para o app; imagens coladas de outros sites são removidas ao salvar.");
    }

    [Fact]
    public void The_gallery_is_loaded_from_the_api_and_shows_even_images_that_are_not_in_the_callers_pool()
    {
        RegisterGalleryApi([new("minha-1", "/images/minha-1.png"), new("do-gm", "/images/do-gm.png")]);

        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.AvailableImages, MyImages()));

        cut.WaitForAssertion(() =>
        {
            var sources = cut.Find(".raf-image-attachment-grid").QuerySelectorAll("img").Select(i => i.GetAttribute("src"));
            sources.Should().Equal("/images/minha-1.png", "/images/do-gm.png");
        });
        cut.FindComponent<ImageAttachmentField>().Instance.Multiple.Should().BeTrue();
    }

    [Fact]
    public async Task Picking_an_image_saves_the_whole_gallery_in_order()
    {
        RegisterGalleryApi([new("do-gm", "/images/do-gm.png")]);
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.AvailableImages, MyImages()));
        cut.WaitForAssertion(() => cut.FindAll(".raf-image-attachment-grid img").Should().HaveCount(1));

        await cut.InvokeAsync(() => cut.FindComponent<ImageAttachmentField>().Instance.PickForTests("minha-2"));

        cut.WaitForAssertion(() => Requests("PUT", GalleryPath).Should().ContainSingle());
        Requests("PUT", GalleryPath).Single().Body.Should().Be("{\"imageIds\":[\"do-gm\",\"minha-2\"]}");
        cut.FindAll(".raf-image-attachment-grid img").Should().HaveCount(2);
    }

    [Fact]
    public async Task Removing_a_thumbnail_saves_the_gallery_without_it()
    {
        RegisterGalleryApi([new("minha-1", "/images/minha-1.png"), new("minha-2", "/images/minha-2.png")]);
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.AvailableImages, MyImages()));
        cut.WaitForAssertion(() => cut.FindAll(".raf-image-attachment-grid img").Should().HaveCount(2));

        await cut.InvokeAsync(() => cut.FindComponent<ImageAttachmentField>().Instance.RemoveForTests("minha-1"));

        cut.WaitForAssertion(() => Requests("PUT", GalleryPath).Should().ContainSingle());
        Requests("PUT", GalleryPath).Single().Body.Should().Be("{\"imageIds\":[\"minha-2\"]}");
    }

    [Fact]
    public async Task Uploading_through_the_gallery_sends_the_campaign_id_and_attaches_the_new_image()
    {
        RegisterGalleryApi([]);
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.AvailableImages, MyImages())
            .Add(x => x.CampaignId, "camp-1"));
        cut.WaitForAssertion(() => Requests("GET", GalleryPath).Should().ContainSingle());

        await cut.InvokeAsync(() => cut.FindComponent<ImageAttachmentField>().Instance.UploadForTests(new FakeBrowserFile("foto.png")));

        Requests("POST", "/api/images").Single().Body.Should().Contain("camp-1");
        cut.WaitForAssertion(() => Requests("PUT", GalleryPath).Should().ContainSingle());
        Requests("PUT", GalleryPath).Single().Body.Should().Be("{\"imageIds\":[\"nova\"]}");
    }

    [Fact]
    public async Task A_rejected_gallery_save_reports_the_api_message_and_restores_what_is_stored()
    {
        RegisterGalleryApi([new("minha-1", "/images/minha-1.png")], HttpStatusCode.BadRequest, "Imagem não encontrada.");
        string? error = null;
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.AvailableImages, MyImages())
            .Add(x => x.OnError, (string e) => error = e));
        cut.WaitForAssertion(() => cut.FindAll(".raf-image-attachment-grid img").Should().HaveCount(1));

        await cut.InvokeAsync(() => cut.FindComponent<ImageAttachmentField>().Instance.PickForTests("minha-2"));

        cut.WaitForAssertion(() => error.Should().Be("Imagem não encontrada."));
        cut.WaitForAssertion(() => cut.FindAll(".raf-image-attachment-grid img").Select(i => i.GetAttribute("src")).Should().Equal("/images/minha-1.png"));
    }

    [Fact]
    public async Task An_image_picked_in_the_editor_is_uploaded_and_its_app_url_goes_back_to_the_editor()
    {
        RegisterGalleryApi([]);
        var mine = MyImages();
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.AvailableImages, mine)
            .Add(x => x.CampaignId, "camp-1"));

        var url = await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.UploadImageFromJs(new FakeJsStream([1, 2, 3]), "retrato.png"));

        url.Should().Be("/images/nova.png");
        var upload = Requests("POST", "/api/images").Single();
        upload.Body.Should().Contain("retrato.png").And.Contain("camp-1");
        mine.Should().Contain(i => i.Id == "nova", "o upload entra no acervo da página, como os outros uploads da ficha");
        Requests("PUT", GalleryPath).Should().BeEmpty("uma imagem do texto não entra na galeria");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Formato de imagem não suportado. Use WebP, JPEG, JPG, PNG ou GIF.", "Formato de imagem não suportado. Use WebP, JPEG, JPG, PNG ou GIF.")]
    [InlineData(HttpStatusCode.BadRequest, "A imagem excede o tamanho máximo de 10MB.", "A imagem excede o tamanho máximo de 10MB.")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "<html>nginx</html>", "A imagem excede o tamanho máximo permitido.")]
    [InlineData(HttpStatusCode.InternalServerError, "boom", "Não foi possível enviar a imagem.")]
    public async Task A_failed_upload_from_the_editor_reports_a_clear_error_and_inserts_nothing(HttpStatusCode status, string apiBody, string expected)
    {
        RegisterGalleryApi([], uploadStatus: status, uploadError: apiBody);
        string? error = null;
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.OnError, (string e) => error = e));

        var url = await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.UploadImageFromJs(new FakeJsStream([1, 2, 3]), "retrato.png"));

        url.Should().BeNull();
        error.Should().Be(expected);
    }

    [Fact]
    public void The_editor_is_created_with_the_insert_image_button_enabled()
    {
        RegisterGalleryApi([]);

        Render<HistoriaEditor>(p => p.Add(x => x.SaveUrl, "character-sheets/abc/historia"));

        JSInterop.VerifyInvoke("ruinaRichText.create").Arguments[3].Should().BeEquivalentTo(new { imagens = true });
    }
}
