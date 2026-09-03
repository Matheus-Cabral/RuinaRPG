using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class HttpContentExtensionsTests
{
    [Fact]
    public async Task Reads_a_plain_text_error_message_as_is()
    {
        // What BadRequest(string) actually comes back as when the client sends no Accept header
        // that prefers JSON — confirmed against the running API (Content-Type: text/plain).
        using var content = new StringContent("Gasto excede os 9 pontos de Atributo disponíveis.", Encoding.UTF8, "text/plain");

        var message = await content.ReadErrorMessageAsync();

        message.Should().Be("Gasto excede os 9 pontos de Atributo disponíveis.");
    }

    [Fact]
    public async Task Unwraps_a_JSON_quoted_string_message()
    {
        using var content = JsonContent.Create("Nickname já está em uso.");

        var message = await content.ReadErrorMessageAsync();

        message.Should().Be("Nickname já está em uso.");
    }

    [Fact]
    public async Task Returns_null_for_empty_content()
    {
        using var content = new StringContent("");

        var message = await content.ReadErrorMessageAsync();

        message.Should().BeNull();
    }

    [Fact]
    public async Task Falls_back_to_the_raw_text_when_it_looks_quoted_but_is_not_valid_JSON()
    {
        using var content = new StringContent("\"unterminated", Encoding.UTF8, "text/plain");

        var message = await content.ReadErrorMessageAsync();

        message.Should().Be("\"unterminated");
    }
}
