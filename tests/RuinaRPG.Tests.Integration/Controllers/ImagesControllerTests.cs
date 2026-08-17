using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Images;

namespace RuinaRPG.Tests.Integration.Controllers;

public class ImagesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    public ImagesControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private static MultipartFormDataContent BuildUpload(byte[] bytes, string fileName = "test.png")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/images", BuildUpload(PngBytes));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_a_valid_png_returns_201_with_a_usable_url()
    {
        var token = await RegisterGmAndGetTokenAsync("ImageGm1", "image1@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        body!.Url.Should().EndWith(".png");
    }

    [Fact]
    public async Task Upload_bytes_that_are_not_a_real_image_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ImageGm2", "image2@teste.com");
        var fakeBytes = "not an image"u8.ToArray();
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(fakeBytes, "fake.png") };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mine_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/images/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_returns_only_images_uploaded_by_the_caller_R0010()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("ImageGm3", "image3@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("ImageGm4", "image4@teste.com");
        var uploadA = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        uploadA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        await _client.SendAsync(uploadA);
        var uploadB = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        uploadB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await _client.SendAsync(uploadB);

        var mineMessage = new HttpRequestMessage(HttpMethod.Get, "/api/images/mine");
        mineMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(mineMessage);

        var body = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        body.Should().ContainSingle();
    }
}
